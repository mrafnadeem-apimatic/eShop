using System.Security.Claims;
using eShop.WebApp.Services.Payments;
using Microsoft.AspNetCore.Mvc;

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

    internal static async Task<IResult> CreatePayPalOrderAsync(
        HttpContext httpContext,
        IPayPalCheckoutService payPalCheckoutService,
        [FromServices] ILogger<PayPalCheckoutApiLogCategory> logger)
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
            var response = await payPalCheckoutService.CreateOrderForBasketAsync(
                basketId,
                userId,
                httpContext.RequestAborted);

            return Results.Ok(response);
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

public sealed class PayPalCheckoutApiLogCategory
{
}

