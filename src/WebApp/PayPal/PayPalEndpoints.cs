using PaypalServerSdk.Standard.Exceptions;

namespace eShop.WebApp.PayPal;

public static class PayPalEndpoints
{
    public static void MapPayPalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/paypal/pay", CreateOrderAndRedirectAsync);
        app.MapGet("/paypal/return", CaptureOrder);
        app.MapGet("/paypal/cancel", CancelAsync);
    }

    private static async Task<IResult> CreateOrderAndRedirectAsync(
        HttpContext httpContext,
        IConfiguration configuration,
        IPayPalCheckoutService payPalCheckoutService,
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

        var returnUrl = configuration["PayPal:RedirectUri"];
        var cancelUrl = configuration["PayPal:CancelUrl"];
        var currency = configuration["PayPal:CurrencyCode"] ?? "USD";

        if (string.IsNullOrWhiteSpace(returnUrl) || string.IsNullOrWhiteSpace(cancelUrl))
        {
            return Results.BadRequest("PayPal is not configured.");
        }

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var returnUri) ||
            !Uri.TryCreate(cancelUrl, UriKind.Absolute, out var cancelUri))
        {
            logger.LogError("Invalid PayPal RedirectUri or CancelUrl configuration.");
            return Results.Problem("Unable to start PayPal payment.");
        }

        try
        {
            // Create the PayPal order using the official SDK.
            var (orderId, approveLink) = await payPalCheckoutService.CreateOrderAsync(
                total,
                currency,
                returnUri,
                cancelUri,
                httpContext.RequestAborted);

            // Persist the created PayPal order id in the user's session so that when
            // they return from PayPal we can validate the query token against this
            // server-side value before marking the payment as completed.
            httpContext.Session.SetString(PayPalSessionKeys.OrderId, orderId);

            return Results.Redirect(approveLink.ToString());
        }
        catch (ApiException ex)
        {
            logger.LogError(
                ex,
                "PayPal API error while creating order. Status code: {StatusCode}",
                ex.ResponseCode);
            return Results.Problem("Unable to start PayPal payment.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while creating PayPal order.");
            return Results.Problem("Unable to start PayPal payment.");
        }
    }

    private static IResult CaptureOrder(
        HttpContext httpContext,
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
