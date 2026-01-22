using eShop.PaymentProcessor.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace eShop.PaymentProcessor.Apis;

public static class PaypalApi
{
    public static IEndpointRouteBuilder MapPaypalApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/paypal");

        api.MapPost("/orders", CreatePaypalOrderAsync);

        return app;
    }

    public static async Task<Results<Ok<CreatePaypalOrderResponse>, BadRequest<string>>> CreatePaypalOrderAsync(
        [FromBody] CreatePaypalOrderRequest request,
        [FromServices] PaypalCheckoutService paypalCheckoutService,
        [FromServices] IOptionsMonitor<PaymentOptions> options,
        ILoggerFactory loggerFactory)
    {
        if (!paypalCheckoutService.IsConfigured)
        {
            return TypedResults.BadRequest("PayPal is not configured on the server.");
        }

        if (request.Total <= 0)
        {
            return TypedResults.BadRequest("Total amount must be greater than zero.");
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? options.CurrentValue.CurrencyCode
            : request.Currency;

        try
        {
            var (paypalOrderId, approvalLink) = await paypalCheckoutService.CreateOrderAsync(
                request.Total,
                currency,
                request.ReturnUrl,
                request.CancelUrl);

            return TypedResults.Ok(new CreatePaypalOrderResponse
            {
                PaypalOrderId = paypalOrderId,
                ApprovalLink = approvalLink
            });
        }
        catch (Exception ex)
        {
            var logger = loggerFactory.CreateLogger(typeof(PaypalApi));
            logger.LogError(ex, "Error creating PayPal order for total {Total}", request.Total);
            return TypedResults.BadRequest("Error creating PayPal order.");
        }
    }
}

public sealed class CreatePaypalOrderRequest
{
    public decimal Total { get; set; }

    public string Currency { get; set; }

    public string ReturnUrl { get; set; }

    public string CancelUrl { get; set; }
}

public sealed class CreatePaypalOrderResponse
{
    public string PaypalOrderId { get; set; }

    public string ApprovalLink { get; set; }
}


