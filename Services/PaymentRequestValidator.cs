using PaymentProcessor.DTOs;

namespace PaymentProcessor.Services;

public class PaymentRequestValidator
{
    public List<string> Validate(CreatePaymentRequest request)
    {
        List<string> errores = new List<string>();

        if (string.IsNullOrWhiteSpace(request.MerchantId))
            errores.Add("MerchantId es requerido");

        if (request.Amount <= 0)
            errores.Add("Amount debe ser mayor que cero");

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Length != 3)
            errores.Add("Currency debe ser un código válido (ej: CLP, USD)");

        if (request.Card == null)
        {
            errores.Add("Card es requerido");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Card.Number) ||
                request.Card.Number.Length < 13 ||
                request.Card.Number.Length > 19 ||
                !request.Card.Number.All(char.IsDigit))
                errores.Add("Card Number debe tener entre 13 y 19 dígitos numéricos");

            if (string.IsNullOrWhiteSpace(request.Card.Expiry) ||
                !System.Text.RegularExpressions.Regex.IsMatch(request.Card.Expiry, @"^(0[1-9]|1[0-2])\/\d{4}$"))
                errores.Add("Card.Expiry debe tener formato MM/YYYY");
            else
            {
                string[] partes = request.Card.Expiry.Split('/');
                int mes = int.Parse(partes[0]);
                int anio = int.Parse(partes[1]);
                DateTime fechaVencimiento = new DateTime(anio, mes, 1).AddMonths(1).AddDays(-1);
                if (fechaVencimiento < DateTime.UtcNow)
                    errores.Add("La tarjeta está vencida");
            }

            if (string.IsNullOrWhiteSpace(request.Card.Cvv) ||
                request.Card.Cvv.Length < 3 ||
                request.Card.Cvv.Length > 4 ||
                !request.Card.Cvv.All(char.IsDigit))
                errores.Add("Card.Cvv debe tener 3 o 4 dígitos numéricos");
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            errores.Add("IdempotencyKey es requerida");

        return errores;
    }
}
