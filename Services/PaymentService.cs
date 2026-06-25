using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentProcessor.Channels;
using PaymentProcessor.Data;
using PaymentProcessor.DTOs;
using PaymentProcessor.Models;
using PaymentProcessor.Models.Enum;

namespace PaymentProcessor.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _repo;
    private readonly IPaymentChannel _channel;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentRepository repo,
        IPaymentChannel channel,
        ILogger<PaymentService> logger)
    {
        _repo = repo;
        _channel = channel;
        _logger = logger;
    }

    public async Task<CreatePaymentResponse> CreatePayment(CreatePaymentRequest request)
    {
        string metodo = nameof(CreatePayment);

        //Verifica idempotencia en repositorio
        IdempotencyRecord? existing = await _repo.GetIdempotencyRecordAsync(request.IdempotencyKey);

        if (existing != null)
        {
            _logger.LogInformation("Idempotencia existe para key {IdempotencyKey}, retorna transacción {TransactionId} método {Metodo} evento {Evento}",
                request.IdempotencyKey, existing.TransactionId, metodo, "IDEMPOTENCY_HIT");

            return JsonSerializer.Deserialize<CreatePaymentResponse>(existing.ResponseBody)!;

        }

        // Contruye entidades
        Transaction transaction = new Transaction
        {
            MerchantId = request.MerchantId,
            Amount = request.Amount,
            Currency = request.Currency,
            CardLastFour = request.Card.Number.Length >= 4
                ? request.Card.Number[^4..]
                : request.Card.Number,
            Status = TransactionStatusEnum.Pending
        };

        transaction.Events.Add(new TransactionEvent
        {
            TransactionId = transaction.Id,
            EventType = "PENDING",
            PreviousStatus = null,
            NewStatus = TransactionStatusEnum.Pending,
            CreatedAt = DateTime.UtcNow
        });

        CreatePaymentResponse response = new CreatePaymentResponse
        {
            TransactionId = transaction.Id,
            Status = nameof(TransactionStatusEnum.Pending),
            CreatedAt = transaction.CreatedAt
        };

        IdempotencyRecord idempotencyRecord = new IdempotencyRecord
        {
            Key = request.IdempotencyKey,
            TransactionId = transaction.Id,
            ResponseBody = JsonSerializer.Serialize(response),
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            response = await _repo.CreateTransactionWithIdempotencyAsync(transaction, idempotencyRecord);

            _logger.LogInformation("Transacción {TransactionId} creada estado PENDING para Merchant {MerchantId} método {Metodo} evento {Evento}",
                transaction.Id, request.MerchantId, metodo, "PENDING");

            await _channel.Write(transaction.Id);

            return response;
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("PK__Idempote") == true)
        {

            _logger.LogWarning("Race condition detectada para key {IdempotencyKey}, re-consultando registro existente. método {Metodo} evento {Evento}",
                request.IdempotencyKey, metodo, "IDEMPOTENCY_RACE_CONDITION");

            existing = await _repo.GetIdempotencyRecordAsync(request.IdempotencyKey);

            if (existing != null)
            {
                _logger.LogInformation("Idempotencia recuperada tras race condition para key {IdempotencyKey}, transacción {TransactionId} método {Metodo} evento {Evento}",
                    request.IdempotencyKey, existing.TransactionId, metodo, "IDEMPOTENCY_HIT_AFTER_RACE");

                return JsonSerializer.Deserialize<CreatePaymentResponse>(existing.ResponseBody)!;
            }

            _logger.LogError("No se pudo recuperar registro de idempotencia tras race condition para key {IdempotencyKey}", request.IdempotencyKey);
            throw;
        }
    }
}
