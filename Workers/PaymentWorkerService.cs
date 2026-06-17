using PaymentProcessor.Channels;
using PaymentProcessor.Services;

// SOLID (SRP): Responsabilidad única → consumir el canal y orquestar el procesamiento.
// SOLID (DIP): Depende de abstracciones (IPaymentChannel, IServiceScopeFactory, ILogger).
public class PaymentWorkerService : BackgroundService
{
    private readonly IPaymentChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentWorkerService> _logger;

    public PaymentWorkerService(
        IPaymentChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentWorkerService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string metodo = nameof(ExecuteAsync);

        _logger.LogInformation("PaymentWorkerService iniciado método {Metodo}", metodo);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Leer del canal de forma asíncrona (Consumer)
                var transactionId = await _channel.ReadAsync(stoppingToken);

                _logger.LogInformation(
                    "Worker recogió transacción {TransactionId} para procesar método {Metodo}",
                    transactionId, metodo);

                // Crear un scope para resolver servicios scoped (DbContext)
                using var scope = _scopeFactory.CreateScope();

                var acquirerService = scope.ServiceProvider
                    .GetRequiredService<IAcquirerIntegrationService>();

                await acquirerService.ProcessWithAcquirerAsync(transactionId);
            }
            catch (OperationCanceledException)
            {
                // Shutdown normal
                _logger.LogInformation("PaymentWorkerService deteniéndose método {Metodo}", metodo);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando pago desde el canal método {Metodo}", metodo);
            }
        }
    }
}
