namespace eShop.WebApp.PayPal;

/// <summary>
/// Strongly-typed options for configuring the PayPal .NET Server SDK in the WebApp.
/// These values are bound from the existing "PayPal" configuration section to
/// preserve the current configuration surface (FR-6, FR-7, FR-8).
/// </summary>
public sealed class PayPalSdkOptions
{
    /// <summary>
    /// OAuth client identifier for the PayPal REST API.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// OAuth client secret for the PayPal REST API.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// "Sandbox" or "Live". Defaults to "Sandbox" for non-production environments.
    /// </summary>
    public string Environment { get; set; } = "Sandbox";

    /// <summary>
    /// Three-letter ISO currency code (for example, "USD" or "EUR").
    /// </summary>
    public string CurrencyCode { get; set; } = "USD";
}

