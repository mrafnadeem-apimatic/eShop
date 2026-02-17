namespace eShop.PaymentProcessor;

public class PayPalOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// PayPal environment name, e.g. "Sandbox" or "Live".
    /// Currently only the Sandbox environment is used by the SDK client.
    /// </summary>
    public string Environment { get; set; } = "Sandbox";
}

