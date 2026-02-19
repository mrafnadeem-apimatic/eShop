using System.Text.Json.Serialization;

namespace eShop.WebApp;

public class PayPalOptions
{
    /// <summary>PayPal application client ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>PayPal application secret. Treat as a credential; do not log or serialize.</summary>
    [JsonIgnore]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// PayPal environment name, e.g. "Sandbox" or "Production".
    /// Values other than "Production" are treated as "Sandbox" by the SDK client.
    /// </summary>
    public string Environment { get; set; } = "Sandbox";
}

