using System.Net.Http.Json;

namespace eShop.WebApp.Services;

public class PaypalCheckoutService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<CreatePaypalOrderResult> CreateOrderAsync(
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
            var error = await response.Content.ReadAsStringAsync();
            throw new PaypalCheckoutException(
                string.IsNullOrWhiteSpace(error)
                    ? "Could not start PayPal checkout. Please try again."
                    : error);
        }

        var payload = await response.Content.ReadFromJsonAsync<CreatePaypalOrderResponse>();
        if (payload is null)
        {
            throw new PaypalCheckoutException("PayPal returned an empty checkout response.");
        }

        if (string.IsNullOrWhiteSpace(payload.PaypalOrderId) ||
            string.IsNullOrWhiteSpace(payload.ApprovalLink))
        {
            throw new PaypalCheckoutException("PayPal checkout did not include an approval link.");
        }

        return new CreatePaypalOrderResult(payload.PaypalOrderId, payload.ApprovalLink);
    }
}

public sealed class PaypalCheckoutException(string message) : Exception(message);

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


