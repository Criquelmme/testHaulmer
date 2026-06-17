using PaymentProcessor.Models.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentProcessor.Models;
public class Transaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
     
    public string MerchantId { get; set; } = string.Empty;

    public long Amount { get; set; }

    public string Currency { get; set; } = "CLP";

    public string? CardLastFour { get; set; }

    public TransactionStatusEnum Status { get; set; } = TransactionStatusEnum.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<TransactionEvent> Events { get; set; } = new List<TransactionEvent>();
}
