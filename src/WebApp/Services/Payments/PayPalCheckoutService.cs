using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Http.Response;
using PaypalServerSdk.Standard.Models;

namespace eShop.WebApp.Services.Payments;

public interface IPayPalCheckoutService
{
    /// <summary>
    /// Creates a PayPal order for the current basket and user.
    /// The concrete implementation will map basket contents to PayPal purchase units.
    /// </summary>
    /// <param name="basketId">The identifier of the basket.</param>
    /// <param name="userId">The identifier of the current user/buyer.</param>
    /// <returns>The PayPal order wrapped in an API response.</returns>
    Task<ApiResponse<Order>> CreateOrderForBasketAsync(string basketId, string userId);
}

public class PayPalCheckoutService : IPayPalCheckoutService
{
    private readonly PaypalServerSdkClient _client;
    private readonly ILogger<PayPalCheckoutService> _logger;

    public PayPalCheckoutService(
        PaypalServerSdkClient client,
        ILogger<PayPalCheckoutService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task<ApiResponse<Order>> CreateOrderForBasketAsync(string basketId, string userId)
    {
        // Implementation will be provided in a later step of the integration.
        throw new NotImplementedException();
    }
}

