namespace eShop.PaymentProcessor;

/// <summary>
/// Configuration options for reaching the Ordering.API from the PaymentProcessor.
/// </summary>
public sealed class OrderingApiOptions
{
    /// <summary>
    /// Base URL for the Ordering.API service (for example, http://ordering-api).
    /// </summary>
    public string BaseUrl { get; set; } = "http://ordering-api";
}

