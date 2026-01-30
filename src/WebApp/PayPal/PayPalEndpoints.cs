using System.Globalization;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Models;

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
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        BasketPricingService basketPricingService,
        PaypalServerSdkClient paypalClient,
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

        var clientId = configuration["PayPal:ClientId"];
        var clientSecret = configuration["PayPal:ClientSecret"];
        var returnUrl = configuration["PayPal:RedirectUri"];
        var cancelUrl = configuration["PayPal:CancelUrl"];
        var currency = configuration["PayPal:CurrencyCode"] ?? "USD";

        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret) ||
            string.IsNullOrWhiteSpace(returnUrl) ||
            string.IsNullOrWhiteSpace(cancelUrl))
        {
            return Results.BadRequest("PayPal is not configured.");
        }

        var amountValue = total.ToString("F2", CultureInfo.InvariantCulture);

        var createOrderInput = new CreateOrderInput
        {
            Body = new OrderRequest
            {
                Intent = CheckoutPaymentIntent.Capture,
                PurchaseUnits = new List<PurchaseUnitRequest>
                {
                    new()
                    {
                        Amount = new AmountWithBreakdown
                        {
                            CurrencyCode = currency,
                            MValue = amountValue,
                        },
                    },
                },
                ApplicationContext = new OrderApplicationContext
                {
                    ReturnUrl = returnUrl,
                    CancelUrl = cancelUrl,
                },
            },
            Prefer = "return=minimal",
        };

        try
        {
            var ordersController = paypalClient.OrdersController;
            var response = await ordersController.CreateOrderAsync(createOrderInput);

            var order = response.Data;
            if (order is null || string.IsNullOrWhiteSpace(order.Id))
            {
                logger.LogError("Invalid PayPal order response from SDK: missing order id.");
                return Results.Problem("Unable to start PayPal payment.");
            }

            // Persist the created PayPal order id in the user's session so that when
            // they return from PayPal we can validate the query token against this
            // server-side value before marking the payment as completed.
            httpContext.Session.SetString(PayPalSessionKeys.OrderId, order.Id);

            var approveLink = order.Links?
                .FirstOrDefault(l => string.Equals(l.Rel, "approve", StringComparison.OrdinalIgnoreCase))
                ?.Href;

            if (string.IsNullOrWhiteSpace(approveLink))
            {
                logger.LogError("No approval link in PayPal order response from SDK.");
                return Results.Problem("Unable to start PayPal payment.");
            }

            logger.LogInformation(
                "Created PayPal order {OrderId} with status {Status}",
                order.Id,
                order.Status);

            return Results.Redirect(approveLink);
        }
        catch (ApiException e)
        {
            logger.LogError(e, "Error creating PayPal order via SDK.");

            if (e is ErrorException error)
            {
                logger.LogError(
                    "PayPal SDK error when creating order. Name={Name}, Message={Message}, DebugId={DebugId}",
                    error.Name,
                    error.Message,
                    error.DebugId);
            }

            return Results.Problem("Unable to start PayPal payment.");
        }
    }

    private static Task<IResult> CaptureOrderAsync(
        HttpContext httpContext,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("PayPalCaptureOrder");
        var orderId = httpContext.Request.Query["token"].ToString();
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return Task.FromResult(Results.BadRequest("Missing PayPal order token."));
        }

        // At this point the payer has approved the PayPal order in the browser.
        // We do NOT capture here. Instead, redirect back to checkout with the
        // approved PayPal order ID so the payment processor can capture it later.
        logger.LogInformation("PayPal order {OrderId} approved, redirecting back to checkout.", orderId);

        var redirectUrl = $"/checkout?paid=1&paypalOrderId={Uri.EscapeDataString(orderId)}";
        return Task.FromResult(Results.Redirect(redirectUrl));
    }

    private static IResult CancelAsync() => Results.Redirect("/checkout");
}
