using PaymentProcessor.DTOs;

namespace PaymentProcessor.Services;

public interface IPaymentService
{
    Task<CreatePaymentResponse> CreatePayment(CreatePaymentRequest request);
}
