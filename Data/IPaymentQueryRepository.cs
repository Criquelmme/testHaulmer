using PaymentProcessor.DTOs;
using PaymentProcessor.Models;

namespace PaymentProcessor.Data;

// SOLID (ISP): Interfaz pequeña y específica SOLO para operaciones de lectura (CQRS).
// SOLID (DIP): Las capas superiores dependen de esta abstracción, no de EF Core directamente.
public interface IPaymentQueryRepository
{
    /// <summary>Obtiene detalle completo de una transacción (con eventos).</summary>
    Task<PaymentDetailResponse?> GetByIdAsync(string transactionId);

    /// <summary>Lista transacciones filtradas por merchant y opcionalmente por estado.</summary>
    Task<PaymentListResponse> GetFilteredAsync(string merchantId, string? status);
}
