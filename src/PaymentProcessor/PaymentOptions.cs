#nullable enable
namespace eShop.PaymentProcessor;

public class PaymentOptions
{
    // Legacy flag used to simulate payment success/failure when PayPal is disabled
    public bool PaymentSucceeded { get; set; } = true;

    // When true and the PayPal credentials are configured, real payments are executed against PayPal.
    public bool UsePayPal { get; set; }

    public string? PayPalClientId { get; set; }

    public string? PayPalClientSecret { get; set; }

    /// <summary>
    /// "Sandbox" or "Live". Defaults to "Sandbox" for development.
    /// </summary>
    public string? PayPalEnvironment { get; set; } = "Sandbox";

    /// <summary>
    /// Three-letter ISO currency code (for example, "USD" or "EUR").
    /// </summary>
    public string CurrencyCode { get; set; } = "USD";
}
