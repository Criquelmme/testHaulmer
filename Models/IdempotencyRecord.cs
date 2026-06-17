using System.ComponentModel.DataAnnotations;

namespace PaymentProcessor.Models;

public class IdempotencyRecord
{
    public string Key { get; set; } = string.Empty;

    public Guid TransactionId { get; set; }

    public string ResponseBody { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
