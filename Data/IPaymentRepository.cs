using PaymentProcessor.DTOs;
using PaymentProcessor.Models;

namespace PaymentProcessor.Data;

// SOLID (ISP): Interfaz pequeña y específica para operaciones de base de datos de pagos.
// SOLID (DIP): Las capas superiores dependen de esta abstracción, no de EF Core directamente.
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
