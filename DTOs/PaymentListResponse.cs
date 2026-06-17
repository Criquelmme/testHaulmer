namespace PaymentProcessor.DTOs;
public class PaymentListResponse
{
    public List<PaymentSummaryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class PaymentSummaryDto
{
    public Guid TransactionId { get; set; }
    public string MerchantId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
