using PaymentProcessor.DTOs;
using PaymentProcessor.Models;

namespace PaymentProcessor.Data;
public interface IPaymentRepository
{
    /// <summary>
    /// Busca un registro de idempotencia por Key (ventana de 24h).
    /// </summary>
    Task<IdempotencyRecord?> GetIdempotencyRecordAsync(string key);

    /// <summary>
    /// Crea una transacción y su registro de idempotencia dentro de una transacción SQL.
    /// </summary>
    Task<CreatePaymentResponse> CreateTransactionWithIdempotencyAsync(
        Transaction transaction, IdempotencyRecord idempotencyRecord);
}
