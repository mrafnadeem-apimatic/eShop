using System.Globalization;
using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Http.Response;
using PaypalServerSdk.Standard.Models;

namespace eShop.WebApp.Services.Payments;

public interface IPayPalOrdersClient
{
    Task<ApiResponse<Order>> CreateOrderAsync(CreateOrderInput input);
    string? GetOrderId(ApiResponse<Order> response);
}

public sealed class SdkPayPalOrdersClient : IPayPalOrdersClient
{
    private readonly PaypalServerSdkClient _client;

    public SdkPayPalOrdersClient(PaypalServerSdkClient client)
    {
        _client = client;
    }

    public Task<ApiResponse<Order>> CreateOrderAsync(CreateOrderInput input)
        => _client.OrdersController.CreateOrderAsync(input);

    public string? GetOrderId(ApiResponse<Order> response)
        => response.Data?.Id;
}

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
    private readonly IPayPalOrdersClient _ordersClient;
    private readonly ILogger<PayPalCheckoutService> _logger;
    private readonly IBasketState _basketState;
    private readonly IPayPalCheckoutSessionStore _sessionStore;

    private const string DefaultCurrencyCode = "USD";

    public PayPalCheckoutService(
        IPayPalOrdersClient ordersClient,
        ILogger<PayPalCheckoutService> logger,
        IBasketState basketState,
        IPayPalCheckoutSessionStore sessionStore)
    {
        _ordersClient = ordersClient;
        _logger = logger;
        _basketState = basketState;
        _sessionStore = sessionStore;
    }

    public async Task<ApiResponse<Order>> CreateOrderForBasketAsync(string basketId, string userId)
    {
        if (string.IsNullOrWhiteSpace(basketId))
        {
            throw new ArgumentException("Basket identifier must be provided.", nameof(basketId));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User identifier must be provided.", nameof(userId));
        }

        var basketItems = await _basketState.GetBasketItemsAsync();
        if (basketItems.Count == 0)
        {
            throw new InvalidOperationException("Cannot create a PayPal order for an empty basket.");
        }

        // Map basket items to PayPal line items and compute the order total.
        var itemRequests = new List<ItemRequest>();
        decimal total = 0m;

        foreach (var item in basketItems)
        {
            var lineTotal = item.UnitPrice * item.Quantity;
            total += lineTotal;

            itemRequests.Add(new ItemRequest
            {
                Name = item.ProductName,
                Quantity = item.Quantity.ToString(CultureInfo.InvariantCulture),
                UnitAmount = new Money
                {
                    CurrencyCode = DefaultCurrencyCode,
                    MValue = item.UnitPrice.ToString("F2", CultureInfo.InvariantCulture),
                },
                // Optionally map SKU or URL here in the future.
            });
        }

        var amount = new AmountWithBreakdown
        {
            CurrencyCode = DefaultCurrencyCode,
            MValue = total.ToString("F2", CultureInfo.InvariantCulture),
        };

        var purchaseUnit = new PurchaseUnitRequest
        {
            ReferenceId = basketId,
            CustomId = basketId,
            Amount = amount,
            Items = itemRequests,
        };

        var orderRequest = new OrderRequest
        {
            Intent = CheckoutPaymentIntent.Capture,
            PurchaseUnits = new List<PurchaseUnitRequest> { purchaseUnit },
        };

        var input = new CreateOrderInput
        {
            Body = orderRequest,
            PaypalRequestId = BuildIdempotencyKey(basketId, userId),
            Prefer = "return=representation",
        };

        try
        {
            var response = await _ordersClient.CreateOrderAsync(input);

            var paypalOrderId = _ordersClient.GetOrderId(response);
            if (!string.IsNullOrWhiteSpace(paypalOrderId))
            {
                var session = new PayPalCheckoutSession(
                    PaypalOrderId: paypalOrderId,
                    BasketId: basketId,
                    UserId: userId,
                    CreatedAtUtc: DateTime.UtcNow);

                await _sessionStore.StoreSessionAsync(session);
            }

            return response;
        }
        catch (ApiException ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Error creating PayPal order for basket {BasketId} and user {UserId}.",
                    basketId,
                    userId);
            }

            throw;
        }
    }

    private static string BuildIdempotencyKey(string basketId, string userId)
        => $"create-{userId}-{basketId}";
}

