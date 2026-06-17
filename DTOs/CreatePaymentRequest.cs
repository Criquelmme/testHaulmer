using System.ComponentModel.DataAnnotations;

namespace PaymentProcessor.DTOs;

public class CreatePaymentRequest
{
    public string MerchantId { get; set; } = string.Empty;

    public long Amount { get; set; }

    public string Currency { get; set; } = "CLP";

    public CardInfo Card { get; set; } = new();

    public string IdempotencyKey { get; set; } = string.Empty;
}

public class CardInfo
{
    public string Number { get; set; } = string.Empty;

    public string Expiry { get; set; } = string.Empty; // MM/YYYY

    public string Cvv { get; set; } = string.Empty;
}