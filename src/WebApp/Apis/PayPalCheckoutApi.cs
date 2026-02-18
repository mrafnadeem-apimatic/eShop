using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using eShop.WebApp.Services;
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
        IBasketState basketState,
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

        var basketItems = await basketState.GetBasketItemsAsync();
        var basketId = ComputeBasketId(basketItems);

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

    private static string ComputeBasketId(IReadOnlyCollection<BasketItem> basketItems)
    {
        var orderedItems = basketItems
            .OrderBy(item => item.ProductId)
            .ThenBy(item => item.Id, StringComparer.Ordinal);

        var builder = new StringBuilder();

        foreach (var item in orderedItems)
        {
            builder
                .Append(item.ProductId)
                .Append(':')
                .Append(item.UnitPrice.ToString("F2", CultureInfo.InvariantCulture))
                .Append(':')
                .Append(item.Quantity)
                .Append(';');
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}

public sealed record PayPalOrderResponse(string PaypalOrderId, string ApprovalUrl);

public sealed class PayPalCheckoutApiLogCategory
{
}

