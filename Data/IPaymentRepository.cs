using PaymentProcessor.DTOs;
using PaymentProcessor.Models;

namespace PaymentProcessor.Data;
public interface IPaymentRepository
{
    
    Task<IdempotencyRecord?> GetIdempotencyRecordAsync(string key);


    Task<CreatePaymentResponse> CreateTransactionWithIdempotencyAsync(
        Transaction transaction, IdempotencyRecord idempotencyRecord);
}
