namespace PaymentProcessor.DTOs;

public class PaymentDetailResponse
{
    public Guid TransactionId { get; set; }
    public string MerchantId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? CardLastFour { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<TransactionEventDto> Events { get; set; } = new();
}

public class TransactionEventDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
