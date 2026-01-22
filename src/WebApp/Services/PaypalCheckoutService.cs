using System.Net.Http.Json;

namespace eShop.WebApp.Services;

public class PaypalCheckoutService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<CreatePaypalOrderResult?> CreateOrderAsync(
        decimal total,
        string currency,
        string returnUrl,
        string cancelUrl)
    {
        var request = new CreatePaypalOrderRequest
        {
            Total = total,
            Currency = currency,
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl
        };

        using var response = await _httpClient.PostAsJsonAsync("/api/paypal/orders", request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<CreatePaypalOrderResponse>();
        if (payload is null)
        {
            return null;
        }

        return new CreatePaypalOrderResult(payload.PaypalOrderId, payload.ApprovalLink);
    }
}

public sealed class CreatePaypalOrderRequest
{
    public decimal Total { get; set; }

    public string Currency { get; set; } = "USD";

    public string ReturnUrl { get; set; } = string.Empty;

    public string CancelUrl { get; set; } = string.Empty;
}

public sealed class CreatePaypalOrderResponse
{
    public string PaypalOrderId { get; set; } = string.Empty;

    public string ApprovalLink { get; set; } = string.Empty;
}

public readonly record struct CreatePaypalOrderResult(string PaypalOrderId, string ApprovalLink);


