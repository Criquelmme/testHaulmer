namespace PaymentProcessor.Channels;

public interface IPaymentChannel
{
    ValueTask Write(Guid transactionId, CancellationToken ct = default);
    ValueTask<Guid> Read(CancellationToken ct = default);
    void Complete();
}
