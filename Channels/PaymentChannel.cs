using System.Threading.Channels;

namespace PaymentProcessor.Channels;

// SOLID (DIP): Implementación concreta que depende de System.Threading.Channels.
// La abstracción IPaymentChannel permite intercambiar la implementación sin afectar consumidores.
public class PaymentChannel : IPaymentChannel
{
    private readonly Channel<Guid> _channel;

    public PaymentChannel()
    {
        // Unbounded: en producción se debe monitorear el tamaño de la cola para evitar OOM.
        _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = false,  // Permitimos múltiples workers
            SingleWriter = false   // Permitimos múltiples productores
        });
    }

    public async ValueTask WriteAsync(Guid transactionId, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(transactionId, ct);
    }

    public async ValueTask<Guid> ReadAsync(CancellationToken ct = default)
    {
        return await _channel.Reader.ReadAsync(ct);
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }
}
