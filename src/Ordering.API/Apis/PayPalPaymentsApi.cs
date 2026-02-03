using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using eShop.Ordering.API.Infrastructure.PayPal;

public static class PayPalPaymentsApi
{
    public static RouteGroupBuilder MapPayPalPaymentsApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("api/payments/paypal")
                     .HasApiVersion(1.0);

        api.MapPost("/create-order", CreatePayPalOrderAsync);

        return api;
    }

    public static async Task<Results<Ok<CreatePayPalOrderResponse>, BadRequest<string>, ProblemHttpResult>> CreatePayPalOrderAsync(
        CreatePayPalOrderRequest request,
        IPayPalClient payPalClient,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return TypedResults.BadRequest("Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            return TypedResults.BadRequest("Currency is required.");
        }

        var logger = loggerFactory.CreateLogger("PayPalPaymentsApi");

        try
        {
            var paypalOrderId = await payPalClient.CreateOrderAsync(
                request.Amount,
                request.Currency,
                request.BasketId,
                cancellationToken);

            logger.LogInformation(
                "Created PayPal order {PayPalOrderId} for basket {BasketId}.",
                paypalOrderId,
                request.BasketId);

            return TypedResults.Ok(new CreatePayPalOrderResponse(paypalOrderId));
        }
        catch (OperationCanceledException)
        {
            // Respect cancellation and surface it as a problem so the client gets a clear signal.
            return TypedResults.Problem(detail: "PayPal order creation was cancelled.", statusCode: StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to create PayPal order for basket {BasketId}.",
                request.BasketId);

            return TypedResults.Problem(detail: "Failed to create PayPal order.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}

public sealed record CreatePayPalOrderRequest(
    decimal Amount,
    string Currency,
    string BasketId = null);

public sealed record CreatePayPalOrderResponse(string PayPalOrderId);

