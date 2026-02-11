namespace eShop.WebApp.PayPal;

/// <summary>
/// Strongly typed configuration for the WebApp's PayPal integration.
/// Binds to the "PayPal" configuration section.
/// </summary>
public sealed class PayPalOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// "Sandbox" or "Live". Defaults to "Sandbox" for development.
    /// </summary>
    public string Environment { get; set; } = "Sandbox";

    /// <summary>
    /// Absolute URL that PayPal redirects to when the shopper approves the payment.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Absolute URL that PayPal redirects to when the shopper cancels the payment.
    /// </summary>
    public string CancelUrl { get; set; } = string.Empty;

    /// <summary>
    /// Three-letter ISO currency code (for example, "USD" or "EUR").
    /// </summary>
    public string CurrencyCode { get; set; } = "USD";

    /// <summary>
    /// When true, the WebApp skips real PayPal API calls and uses a fake order id
    /// for end-to-end testing.
    /// </summary>
    public bool E2ETestMode { get; set; }
}

