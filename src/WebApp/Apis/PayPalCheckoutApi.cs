using System.Security.Claims;
using Microsoft.Extensions.Options;
using eShop.WebApp.Services.Payments;

namespace eShop.WebApp;

public static class PayPalCheckoutApi
{
    public static IEndpointRouteBuilder MapPayPalCheckoutApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("api/paypal")
            .RequireAuthorization();

        api.MapGet("/config", GetPayPalConfigAsync)
            .WithName("GetPayPalConfig");
        api.MapPost("/order", CreatePayPalOrderAsync)
            .WithName("CreatePayPalOrder");

        return app;
    }

    private static IResult GetPayPalConfigAsync(
        HttpContext httpContext,
        IOptions<PayPalOptions> options)
    {
        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var opts = options.Value;
        var environment = string.IsNullOrWhiteSpace(opts.Environment)
            ? "sandbox"
            : opts.Environment.Equals("Live", StringComparison.OrdinalIgnoreCase)
                ? "live"
                : "sandbox";

        return Results.Ok(new PayPalConfigResponse(opts.ClientId, environment));
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
            var result = await payPalCheckoutService.CreateOrderForBasketAsync(basketId, userId);
            var payload = new PayPalOrderResponse(
                PaypalOrderId: result.PaypalOrderId,
                ApprovalUrl: result.ApprovalUrl);
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

public sealed record PayPalConfigResponse(string ClientId, string Environment);

