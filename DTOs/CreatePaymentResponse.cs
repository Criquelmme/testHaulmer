namespace PaymentProcessor.DTOs;

public class CreatePaymentResponse
{
    public Guid TransactionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
