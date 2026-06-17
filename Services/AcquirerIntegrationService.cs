using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PaymentProcessor.Data;
using PaymentProcessor.Models;
using PaymentProcessor.Models.Enum;

namespace PaymentProcessor.Services;

public class AcquirerIntegrationService : IAcquirerIntegrationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AcquirerIntegrationService> _logger;

    public AcquirerIntegrationService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<AcquirerIntegrationService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task ProcessWithAcquirerAsync(Guid transactionId)
    {
        string metodo = nameof(ProcessWithAcquirerAsync);
        Stopwatch stopwatch = Stopwatch.StartNew();

        using IDisposable? _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["transaction_id"] = transactionId.ToString()
        });

        _logger.LogInformation("Procesando transacción con adquirente método {Metodo}", metodo);

        using IServiceScope scope = _scopeFactory.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            Models.Transaction? transaction = await db.Transactions.FindAsync(new object[] { transactionId });

            if (transaction == null)
            {
                _logger.LogWarning("Transacción {TransactionId} no encontrada", transactionId);
                return;
            }

            TransactionStatusEnum previousStatus = transaction.Status;

            transaction.Status = TransactionStatusEnum.Processing;
            transaction.UpdatedAt = DateTime.UtcNow;

            db.TransactionEvents.Add(new TransactionEvent
            {
                TransactionId = transactionId,
                EventType = "PROCESSING",
                PreviousStatus = previousStatus,
                NewStatus = TransactionStatusEnum.Processing,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            _logger.LogInformation("Transacción {TransactionId} actualizada a PROCESSING método {Metodo} evento {Evento}",
                transactionId, metodo, "PROCESSING");

            // Llamada http al adquirente 
            HttpClient client = _httpClientFactory.CreateClient("AcquirerClient");

            HttpResponseMessage response = await client.PostAsJsonAsync("authorize", new
            {
                transaction_id = transactionId.ToString(),
                merchant_id = transaction.MerchantId,
                amount = transaction.Amount,
                currency = transaction.Currency
            });

            stopwatch.Stop();

            // Procesar respuesta del adquirente
            if (response.IsSuccessStatusCode)
            {
                AcquirerResult? result = await response.Content.ReadFromJsonAsync<AcquirerResult>();

                TransactionStatusEnum newStatus = result?.Status == "APPROVED"
                    ? TransactionStatusEnum.Approved
                    : TransactionStatusEnum.Declined;

                try
                {
                    // Actualizar BD con el resultado
                    previousStatus = transaction.Status;
                    transaction.Status = newStatus;
                    transaction.UpdatedAt = DateTime.UtcNow;

                    db.TransactionEvents.Add(new TransactionEvent
                    {
                        TransactionId = transactionId,
                        EventType = newStatus == TransactionStatusEnum.Approved ? "APPROVED" : "DECLINED",
                        PreviousStatus = previousStatus,
                        NewStatus = newStatus,
                        CreatedAt = DateTime.UtcNow
                    });

                    await db.SaveChangesAsync();

                    _logger.LogInformation("Transacción {TransactionId} completada estado {Estado} en {Duracion}ms método {Metodo} evento {Evento}",
                        transactionId, newStatus, stopwatch.ElapsedMilliseconds, metodo,
                        newStatus == TransactionStatusEnum.Approved ? "APPROVED" : "DECLINED");
                }
                catch (Exception ex)
                {
                    // Caso: adquirente aprobó pero la BD falló
                    _logger.LogCritical(ex, "Error BD: adquirente aprobó transacción {TransactionId} con estado {Estado}, reintentando... método {Metodo}",
                        transactionId, newStatus, metodo);

                    // Reintentar actualización en un nuevo scope
                    try
                    {
                        using IServiceScope scopeReintento = _scopeFactory.CreateScope();
                        ApplicationDbContext dbReintento = scopeReintento.ServiceProvider
                            .GetRequiredService<ApplicationDbContext>();

                        Transaction? txReintento = await dbReintento.Transactions
                            .FindAsync(new object[] { transactionId });

                        if (txReintento != null && txReintento.Status == TransactionStatusEnum.Processing)
                        {
                            TransactionStatusEnum estadoAnterior = txReintento.Status;
                            txReintento.Status = newStatus;
                            txReintento.UpdatedAt = DateTime.UtcNow;

                            dbReintento.TransactionEvents.Add(new TransactionEvent
                            {
                                TransactionId = transactionId,
                                EventType = newStatus == TransactionStatusEnum.Approved ? "APPROVED" : "DECLINED",
                                PreviousStatus = estadoAnterior,
                                NewStatus = newStatus,
                                CreatedAt = DateTime.UtcNow
                            });

                            await dbReintento.SaveChangesAsync();
                            _logger.LogInformation("Transacción {TransactionId} recuperada - estado final {Estado} después de reintento método {Metodo} evento {Evento}",
                                transactionId, newStatus, metodo, "RECOVERED");
                        }
                    }
                    catch (Exception exReintento)
                    {
                        _logger.LogCritical(exReintento, "CRÍTICO: Transacción {TransactionId} quedó atascada en PROCESSING. Adquirente respondió con estado {Estado}. Se requiere intervención manual. método {Metodo}",
                            transactionId, newStatus, metodo);
                    }
                }
            }
            else
            {
                stopwatch.Stop();
                int codigoEstado = (int)response.StatusCode;

                if (codigoEstado >= 500)
                {
                    // Error del servidor del adquirente después de reintentos → FAILED
                    previousStatus = transaction.Status;
                    transaction.Status = TransactionStatusEnum.Failed;
                    transaction.UpdatedAt = DateTime.UtcNow;

                    db.TransactionEvents.Add(new TransactionEvent
                    {
                        TransactionId = transactionId,
                        EventType = "FAILED",
                        PreviousStatus = previousStatus,
                        NewStatus = TransactionStatusEnum.Failed,
                        CreatedAt = DateTime.UtcNow
                    });

                    await db.SaveChangesAsync();
                    _logger.LogError("Transacción {TransactionId} falló después de reintentos (HTTP {StatusCode}) en {Duracion}ms método {Metodo} evento {Evento}",
                        transactionId, codigoEstado, stopwatch.ElapsedMilliseconds, metodo, "FAILED_AFTER_RETRIES");
                }
                else
                {
                    // Error DECLINED (rechazo de negocio)
                    previousStatus = transaction.Status;
                    transaction.Status = TransactionStatusEnum.Declined;
                    transaction.UpdatedAt = DateTime.UtcNow;

                    db.TransactionEvents.Add(new TransactionEvent
                    {
                        TransactionId = transactionId,
                        EventType = "DECLINED_BY_ACQUIRER",
                        PreviousStatus = previousStatus,
                        NewStatus = TransactionStatusEnum.Declined,
                        CreatedAt = DateTime.UtcNow
                    });

                    await db.SaveChangesAsync();
                    _logger.LogWarning("Transacción {TransactionId} rechazada por adquirente (HTTP {StatusCode}) en {Duracion}ms método {Metodo} evento {Evento}",
                        transactionId, codigoEstado, stopwatch.ElapsedMilliseconds, metodo, "DECLINED_BY_ACQUIRER");
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Error despues de 3 reintentos Polly agotados
            using (IServiceScope innerScope = _scopeFactory.CreateScope())
            {
                ApplicationDbContext innerDb = innerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Transaction? tx = await innerDb.Transactions.FindAsync(new object[] { transactionId });
                if (tx != null)
                {
                    TransactionStatusEnum previousStatus = tx.Status;
                    tx.Status = TransactionStatusEnum.Failed;
                    tx.UpdatedAt = DateTime.UtcNow;

                    innerDb.TransactionEvents.Add(new TransactionEvent
                    {
                        TransactionId = transactionId,
                        EventType = "FAILED",
                        PreviousStatus = previousStatus,
                        NewStatus = TransactionStatusEnum.Failed,
                        CreatedAt = DateTime.UtcNow
                    });

                    await innerDb.SaveChangesAsync();
                }
            }

            _logger.LogError(ex, "Transacción {TransactionId} falló después de 3 reintentos en {Duracion}ms método {Metodo} evento {Evento}",
                transactionId, stopwatch.ElapsedMilliseconds, metodo, "FAILED");
        }
    }

    private record AcquirerResult(string Status, string AuthorizationCode);
}
