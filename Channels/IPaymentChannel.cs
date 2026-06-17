namespace PaymentProcessor.Channels;

// SOLID (ISP): Interfaz pequeña y específica para el canal de pagos.
// SOLID (DIP): Dependemos de esta abstracción, no de la implementación concreta.
public interface IPaymentChannel
{
    /// <summary>Escribe un TransactionId en el canal (Producer).</summary>
    ValueTask WriteAsync(Guid transactionId, CancellationToken ct = default);

    /// <summary>Lee un TransactionId del canal (Consumer).</summary>
    ValueTask<Guid> ReadAsync(CancellationToken ct = default);

    /// <summary>Marca el canal como completo (no más escrituras).</summary>
    void Complete();
}
