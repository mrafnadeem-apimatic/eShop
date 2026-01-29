using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace eShop.WebApp.PayPal;

public static class PayPalEndpoints
{
    public static void MapPayPalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/paypal/pay", CreateOrderAndRedirectAsync);
        app.MapGet("/paypal/return", CaptureOrderAsync);
        app.MapGet("/paypal/cancel", CancelAsync);
    }

    private static async Task<IResult> CreateOrderAndRedirectAsync(
        HttpContext httpContext,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        BasketPricingService basketPricingService,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("PayPalCreateOrder");
        var total = await basketPricingService.GetBasketTotalAsync(httpContext.RequestAborted);
        if (total <= 0)
        {
            return Results.BadRequest("Basket is empty.");
        }

        // E2E test mode: skip real PayPal API, set session and redirect to /paypal/return.
        if (configuration.GetValue<bool>("PayPal:E2ETestMode"))
        {
            logger.LogInformation("E2E test mode is enabled.");
            var fakeOrderId = "e2e-test-" + Guid.NewGuid().ToString("N");
            httpContext.Session.SetString(PayPalSessionKeys.OrderId, fakeOrderId);
            return Results.Redirect("/paypal/return?token=" + Uri.EscapeDataString(fakeOrderId));
        }

        var env = configuration["PayPal:Environment"] ?? "Sandbox";
        var clientId = configuration["PayPal:ClientId"];
        var clientSecret = configuration["PayPal:ClientSecret"];
        var returnUrl = configuration["PayPal:RedirectUri"];
        var cancelUrl = configuration["PayPal:CancelUrl"];
        var currency = configuration["PayPal:CurrencyCode"] ?? "USD";

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) ||
            string.IsNullOrWhiteSpace(returnUrl) || string.IsNullOrWhiteSpace(cancelUrl))
        {
            return Results.BadRequest("PayPal is not configured.");
        }

        var baseUrl = env.Equals("Live", StringComparison.OrdinalIgnoreCase)
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";

        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl);

        // Get app access token (client_credentials)
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
        using var tokenReq = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            })
        };
        tokenReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        var tokenResp = await client.SendAsync(tokenReq);
        if (!tokenResp.IsSuccessStatusCode)
        {
            var body = await tokenResp.Content.ReadAsStringAsync();
            logger.LogError("Error getting PayPal access token: {Status} {Body}", tokenResp.StatusCode, body);
            return Results.Problem("Unable to start PayPal payment.");
        }

        var token = await tokenResp.Content.ReadFromJsonAsync<PayPalTokenResponse>();
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            return Results.Problem("Invalid PayPal token response.");
        }

        // Create order with approval link
        using var orderReq = new HttpRequestMessage(HttpMethod.Post, "/v2/checkout/orders");
        orderReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var bodyObj = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    amount = new
                    {
                        currency_code = currency,
                        value = total.ToString("F2", CultureInfo.InvariantCulture)
                    }
                }
            },
            application_context = new
            {
                return_url = returnUrl,
                cancel_url = cancelUrl
            }
        };

        orderReq.Content = JsonContent.Create(bodyObj);

        var orderResp = await client.SendAsync(orderReq);
        if (!orderResp.IsSuccessStatusCode)
        {
            var body = await orderResp.Content.ReadAsStringAsync();
            logger.LogError("Error creating PayPal order: {Status} {Body}", orderResp.StatusCode, body);
            return Results.Problem("Unable to start PayPal payment.");
        }

        var order = await orderResp.Content.ReadFromJsonAsync<PayPalOrderResponse>();

        if (order is null || string.IsNullOrWhiteSpace(order.Id))
        {
            logger.LogError("Invalid PayPal order response: missing order id.");
            return Results.Problem("Unable to start PayPal payment.");
        }

        // Persist the created PayPal order id in the user's session so that when
        // they return from PayPal we can validate the query token against this
        // server-side value before marking the payment as completed.
        httpContext.Session.SetString(PayPalSessionKeys.OrderId, order.Id);

        var approveLink = order.Links?.FirstOrDefault(l => l.Rel == "approve")?.Href;
        if (string.IsNullOrWhiteSpace(approveLink))
        {
            logger.LogError("No approval link in PayPal order response.");
            return Results.Problem("Unable to start PayPal payment.");
        }

        return Results.Redirect(approveLink);
    }

    private static async Task<IResult> CaptureOrderAsync(
        HttpContext httpContext,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("PayPalCaptureOrder");
        var orderId = httpContext.Request.Query["token"].ToString();
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return Results.BadRequest("Missing PayPal order token.");
        }
        
        // At this point the payer has approved the PayPal order in the browser.
        // We do NOT capture here. Instead, redirect back to checkout with the
        // approved PayPal order ID so the payment processor can capture it later.
        logger.LogInformation("PayPal order {OrderId} approved, redirecting back to checkout.", orderId);

        var redirectUrl = $"/checkout?paid=1&paypalOrderId={Uri.EscapeDataString(orderId)}";
        return Results.Redirect(redirectUrl);
    }

    private static IResult CancelAsync() => Results.Redirect("/checkout");

    private sealed class PayPalTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }

    private sealed class PayPalOrderResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("links")]
        public List<PayPalLink>? Links { get; init; }
    }

    private sealed class PayPalLink
    {
        [JsonPropertyName("rel")]
        public string Rel { get; init; } = string.Empty;

        [JsonPropertyName("href")]
        public string Href { get; init; } = string.Empty;
    }
}
