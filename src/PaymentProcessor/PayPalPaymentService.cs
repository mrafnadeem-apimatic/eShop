#nullable enable
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace eShop.PaymentProcessor;

public sealed class PayPalPaymentService(
    OrderingApiClient orderingApiClient,
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<PaymentOptions> options,
    ILogger<PayPalPaymentService> logger) : IPaymentService
{
    private readonly OrderingApiClient _orderingApiClient = orderingApiClient;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IOptionsMonitor<PaymentOptions> _options = options;
    private readonly ILogger<PayPalPaymentService> _logger = logger;

    public async Task<bool> ProcessPaymentAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var settings = _options.CurrentValue;

        // If PayPal is not configured, fall back to the existing simulated payment behavior.
        if (!settings.UsePayPal ||
            string.IsNullOrWhiteSpace(settings.PayPalClientId) ||
            string.IsNullOrWhiteSpace(settings.PayPalClientSecret))
        {
            _logger.LogInformation(
                "PayPal not configured or disabled; falling back to PaymentSucceeded flag for order {OrderId}",
                orderId);

            return settings.PaymentSucceeded;
        }

        var order = await _orderingApiClient.GetOrderAsync(orderId, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("Unable to load order {OrderId} from Ordering.API; marking payment as failed", orderId);
            return false;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("paypal");

            var baseUrl = settings.PayPalEnvironment?.Equals("Live", StringComparison.OrdinalIgnoreCase) == true
                ? "https://api-m.paypal.com"
                : "https://api-m.sandbox.paypal.com";

            client.BaseAddress ??= new Uri(baseUrl);

            var accessToken = await GetAccessTokenAsync(client, settings, cancellationToken);
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Could not obtain PayPal access token for order {OrderId}", orderId);
                return false;
            }

            if (string.IsNullOrWhiteSpace(order.PayPalOrderId))
            {
                _logger.LogWarning(
                    "Order {OrderId} does not have an associated PayPal order ID; cannot capture payment.",
                    orderId);
                return false;
            }

            var captured = await CapturePayPalOrderAsync(client, accessToken, order.PayPalOrderId, cancellationToken);

            _logger.LogInformation("PayPal capture for order {OrderId} completed with result: {Result}", orderId, captured);

            return captured;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing PayPal payment for order {OrderId}", orderId);
            return false;
        }
    }

    private static async Task<string?> GetAccessTokenAsync(
        HttpClient client,
        PaymentOptions options,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{options.PayPalClientId}:{options.PayPalClientSecret}"));

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<PayPalTokenResponse>(cancellationToken: cancellationToken);
        return payload?.AccessToken;
    }

    private static async Task<bool> CapturePayPalOrderAsync(
        HttpClient client,
        string accessToken,
        string paypalOrderId,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v2/checkout/orders/{paypalOrderId}/capture");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // PayPal expects application/json content type, even for an empty body
        request.Content = JsonContent.Create(new { });

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var payload = await response.Content.ReadFromJsonAsync<PayPalCaptureOrderResponse>(cancellationToken: cancellationToken);
        return string.Equals(payload?.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class PayPalTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }

    private sealed class PayPalCaptureOrderResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }
    }
}


