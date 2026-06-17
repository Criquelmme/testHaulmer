using System.Threading.Channels;

namespace PaymentProcessor.Channels;


public class PaymentChannel : IPaymentChannel
{
    private readonly Channel<Guid> _channel;

    public PaymentChannel()
    {
        _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = false,  
            SingleWriter = false  
        });
    }

    public async ValueTask Write(Guid transactionId, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(transactionId, ct);
    }

    public async ValueTask<Guid> Read(CancellationToken ct = default)
    {
        return await _channel.Reader.ReadAsync(ct);
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }
}
