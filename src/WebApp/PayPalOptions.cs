namespace eShop.WebApp;

public class PayPalOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>
    /// PayPal environment name, e.g. "Sandbox" or "Production".
    /// Values other than "Production" are treated as "Sandbox" by the SDK client.
    /// </summary>
    public string Environment { get; set; } = "Sandbox";
}

