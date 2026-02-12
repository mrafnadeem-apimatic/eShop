using System.Globalization;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;
using PaypalServerSdk.Standard.Models;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

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

        try
        {
            var environment = env.Equals("Live", StringComparison.OrdinalIgnoreCase)
                ? PaypalServerSdk.Standard.Environment.Production
                : PaypalServerSdk.Standard.Environment.Sandbox;

            var client = new PaypalServerSdkClient.Builder()
                .ClientCredentialsAuth(
                    new ClientCredentialsAuthModel.Builder(clientId, clientSecret).Build())
                .Environment(environment)
                .Build();

            var amount = new AmountWithBreakdown
            {
                CurrencyCode = currency,
                MValue = total.ToString("F2", CultureInfo.InvariantCulture)
            };

            var purchaseUnit = new PurchaseUnitRequest
            {
                Amount = amount
            };

            var appContext = new OrderApplicationContext
            {
                ReturnUrl = returnUrl,
                CancelUrl = cancelUrl
            };

            var orderRequest = new OrderRequest
            {
                Intent = CheckoutPaymentIntent.Capture,
                PurchaseUnits = new List<PurchaseUnitRequest> { purchaseUnit },
                ApplicationContext = appContext
            };

            var createInput = new CreateOrderInput
            {
                Body = orderRequest,
                ContentType = "application/json",
                Prefer = "return=representation"
            };

            var apiResponse = await client.OrdersController.CreateOrderAsync(createInput, httpContext.RequestAborted);

            if (apiResponse.StatusCode is < 200 or >= 300)
            {
                logger.LogError("Error creating PayPal order: {Status}", apiResponse.StatusCode);
                return Results.Problem("Unable to start PayPal payment.");
            }

            var order = apiResponse.Data;
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating PayPal order via PayPalServerSDK.");
            return Results.Problem("Unable to start PayPal payment.");
        }
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
}
