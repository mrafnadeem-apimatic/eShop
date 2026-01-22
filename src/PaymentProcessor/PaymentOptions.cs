namespace eShop.PaymentProcessor;

public class PaymentOptions
{
    public bool PaymentSucceeded { get; set; }

    public string PaypalClientId { get; set; }

    public string PaypalClientSecret { get; set; }

    public bool UseSandbox { get; set; } = true;

    public string CurrencyCode { get; set; } = "USD";
}

