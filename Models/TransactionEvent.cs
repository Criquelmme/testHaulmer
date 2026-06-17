using PaymentProcessor.Models.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentProcessor.Models;

public class TransactionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public string EventType { get; set; } = string.Empty;

    public TransactionStatusEnum? PreviousStatus { get; set; }

    public TransactionStatusEnum NewStatus { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Transaction Transaction { get; set; } = null!;
}
