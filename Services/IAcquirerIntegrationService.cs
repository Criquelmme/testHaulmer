namespace PaymentProcessor.Services;
public interface IAcquirerIntegrationService
{
    Task ProcessWithAcquirerAsync(Guid transactionId);
}
