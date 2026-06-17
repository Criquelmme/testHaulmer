using PaymentProcessor.DTOs;

namespace PaymentProcessor.Services;
public interface IPaymentQueryService
{
    Task<PaymentDetailResponse?> GetById(string transactionId);

    Task<PaymentListResponse> GetFiltered(string merchantId, string? status);
}
