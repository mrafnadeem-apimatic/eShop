using System.Security.Claims;
using eShop.WebApp.Services.Payments;

namespace eShop.WebApp;

public static class PayPalCheckoutApi
{
    public static IEndpointRouteBuilder MapPayPalCheckoutApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("api/paypal")
            .RequireAuthorization();

        api.MapPost("/order", CreatePayPalOrderAsync)
            .WithName("CreatePayPalOrder");

        return app;
    }

    private static async Task<IResult> CreatePayPalOrderAsync(
        HttpContext httpContext,
        IPayPalCheckoutService payPalCheckoutService,
        ILogger logger)
    {
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogWarning("Authenticated user is missing a 'sub' claim.");
            return Results.Problem(
                detail: "User identifier is missing.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // For now, reuse the buyer identifier as the basket identifier for PayPal.
        var basketId = userId;

        try
        {
            var response = await payPalCheckoutService.CreateOrderForBasketAsync(basketId, userId);
            var order = response.Data;

            if (order is null || string.IsNullOrWhiteSpace(order.Id))
            {
                logger.LogError("PayPal did not return a valid order for user {UserId}.", userId);
                return Results.Problem(
                    detail: "PayPal did not return a valid order.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            var approvalLink = order.Links?
                .FirstOrDefault(link =>
                    string.Equals(link.Rel, "approve", StringComparison.OrdinalIgnoreCase));

            if (approvalLink is null || string.IsNullOrWhiteSpace(approvalLink.Href))
            {
                logger.LogError(
                    "PayPal order {OrderId} for user {UserId} did not contain an approval link.",
                    order.Id,
                    userId);

                return Results.Problem(
                    detail: "PayPal did not provide an approval link for this order.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            var payload = new PayPalOrderResponse(
                PaypalOrderId: order.Id,
                ApprovalUrl: approvalLink.Href);

            return Results.Ok(payload);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating PayPal order for user {UserId}.", userId);
            return Results.Problem("An error occurred while creating the PayPal order.");
        }
    }

    private static string? GetUserId(ClaimsPrincipal user)
        => user.FindFirst("sub")?.Value;
}

public sealed record PayPalOrderResponse(string PaypalOrderId, string ApprovalUrl);

