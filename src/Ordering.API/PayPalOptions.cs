namespace eShop.Ordering.API;

public class PayPalOptions
{
    /// <summary>
    /// PayPal REST API client identifier.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// PayPal REST API client secret. This should be provided via environment variables or user secrets.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Target PayPal environment, e.g. "Sandbox" or "Live".
    /// </summary>
    public string Environment { get; set; }
}

