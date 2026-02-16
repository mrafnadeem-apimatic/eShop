using System.Globalization;
using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Http.Response;
using PaypalServerSdk.Standard.Models;

namespace eShop.WebApp.Services.Payments;

/// <summary>
/// Application abstraction for creating a PayPal order (no SDK types in interface).
/// </summary>
public interface IPayPalOrdersClient
{
    Task<CreatePayPalOrderResult?> CreateOrderAsync(CreatePayPalOrderRequest request);
}

public sealed class SdkPayPalOrdersClient : IPayPalOrdersClient
{
    private readonly PaypalServerSdkClient _client;

    public SdkPayPalOrdersClient(PaypalServerSdkClient client)
    {
        _client = client;
    }

    public async Task<CreatePayPalOrderResult?> CreateOrderAsync(CreatePayPalOrderRequest request)
    {
        var input = MapToSdkInput(request);
        var response = await _client.OrdersController.CreateOrderAsync(input);
        return MapToResult(response);
    }

    private static CreateOrderInput MapToSdkInput(CreatePayPalOrderRequest request)
    {
        const string currency = "USD";
        decimal total = 0m;
        var itemRequests = new List<ItemRequest>();

        foreach (var item in request.Items)
        {
            var lineTotal = item.UnitPrice * item.Quantity;
            total += lineTotal;
            itemRequests.Add(new ItemRequest
            {
                Name = item.Name,
                Quantity = item.Quantity.ToString(CultureInfo.InvariantCulture),
                UnitAmount = new Money { CurrencyCode = currency, MValue = item.UnitPrice.ToString("F2", CultureInfo.InvariantCulture) },
            });
        }

        var orderRequest = new OrderRequest
        {
            Intent = CheckoutPaymentIntent.Capture,
            PurchaseUnits = new List<PurchaseUnitRequest>
            {
                new()
                {
                    ReferenceId = request.BasketId,
                    CustomId = request.BasketId,
                    Amount = new AmountWithBreakdown { CurrencyCode = currency, MValue = total.ToString("F2", CultureInfo.InvariantCulture) },
                    Items = itemRequests,
                },
            },
        };

        return new CreateOrderInput
        {
            Body = orderRequest,
            PaypalRequestId = $"create-{request.UserId}-{request.BasketId}",
            Prefer = "return=representation",
        };
    }

    private static CreatePayPalOrderResult? MapToResult(ApiResponse<Order> response)
    {
        var order = response.Data;
        if (order is null || string.IsNullOrWhiteSpace(order.Id))
            return null;

        var approveLink = order.Links?.FirstOrDefault(l =>
            string.Equals(l.Rel, "approve", StringComparison.OrdinalIgnoreCase));
        if (approveLink is null || string.IsNullOrWhiteSpace(approveLink.Href))
            return null;

        return new CreatePayPalOrderResult(order.Id, approveLink.Href);
    }
}

/// <summary>
/// Application service for PayPal checkout (no SDK types in interface).
/// </summary>
public interface IPayPalCheckoutService
{
    /// <summary>
    /// Creates a PayPal order for the current basket and user.
    /// </summary>
    /// <param name="basketId">The identifier of the basket.</param>
    /// <param name="userId">The identifier of the current user/buyer.</param>
    /// <returns>The created order result with PayPal order ID and approval URL.</returns>
    Task<CreatePayPalOrderResult> CreateOrderForBasketAsync(string basketId, string userId);
}

public class PayPalCheckoutService : IPayPalCheckoutService
{
    private readonly IPayPalOrdersClient _ordersClient;
    private readonly ILogger<PayPalCheckoutService> _logger;
    private readonly IBasketState _basketState;
    private readonly IPayPalCheckoutSessionStore _sessionStore;

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

    public async Task<CreatePayPalOrderResult> CreateOrderForBasketAsync(string basketId, string userId)
    {
        if (string.IsNullOrWhiteSpace(basketId))
            throw new ArgumentException("Basket identifier must be provided.", nameof(basketId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User identifier must be provided.", nameof(userId));

        var basketItems = await _basketState.GetBasketItemsAsync();
        if (basketItems.Count == 0)
            throw new InvalidOperationException("Cannot create a PayPal order for an empty basket.");

        var lineItems = basketItems.Select(i => new PayPalLineItem(i.ProductName, i.Quantity, i.UnitPrice)).ToList();
        var request = new CreatePayPalOrderRequest(basketId, userId, lineItems);

        try
        {
            var result = await _ordersClient.CreateOrderAsync(request);
            if (result is null)
            {
                _logger.LogError("PayPal did not return a valid order for user {UserId}.", userId);
                throw new InvalidOperationException("PayPal did not return a valid order.");
            }

            var session = new PayPalCheckoutSession(
                PaypalOrderId: result.PaypalOrderId,
                BasketId: basketId,
                UserId: userId,
                CreatedAtUtc: DateTime.UtcNow);
            await _sessionStore.StoreSessionAsync(session);

            return result;
        }
        catch (ApiException ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
                _logger.LogError(ex, "Error creating PayPal order for basket {BasketId} and user {UserId}.", basketId, userId);
            throw;
        }
    }
}

