using PaymentProcessor.DTOs;
using PaymentProcessor.Models;

namespace PaymentProcessor.Data;


public interface IPaymentQueryRepository
{
    Task<PaymentDetailResponse?> GetByIdAsync(string transactionId);

    Task<PaymentListResponse> GetFilteredAsync(string merchantId, string? status);
}
