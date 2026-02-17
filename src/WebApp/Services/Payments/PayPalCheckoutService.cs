using System.Globalization;
using System.Threading;
using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Http.Response;
using PaypalServerSdk.Standard.Models;
using eShop.WebApp;

namespace eShop.WebApp.Services.Payments;

public sealed record PayPalOrderItem(
    string Name,
    int Quantity,
    decimal UnitPrice,
    string CurrencyCode);

public sealed record PayPalOrderRequest(
    string BasketId,
    string UserId,
    IReadOnlyCollection<PayPalOrderItem> Items,
    decimal Total,
    string CurrencyCode,
    string IdempotencyKey);

public interface IPayPalOrdersClient
{
    Task<PayPalOrderResponse> CreateOrderAsync(PayPalOrderRequest request, CancellationToken cancellationToken = default);
}

public sealed class SdkPayPalOrdersClient : IPayPalOrdersClient
{
    private readonly PaypalServerSdkClient _client;

    public SdkPayPalOrdersClient(PaypalServerSdkClient client)
    {
        _client = client;
    }

    public async Task<PayPalOrderResponse> CreateOrderAsync(PayPalOrderRequest request, CancellationToken cancellationToken = default)
    {
        // Map the domain request to the PayPal SDK request model.
        var itemRequests = new List<ItemRequest>();

        foreach (var item in request.Items)
        {
            itemRequests.Add(new ItemRequest
            {
                Name = item.Name,
                Quantity = item.Quantity.ToString(CultureInfo.InvariantCulture),
                UnitAmount = new Money
                {
                    CurrencyCode = item.CurrencyCode,
                    MValue = item.UnitPrice.ToString("F2", CultureInfo.InvariantCulture),
                },
            });
        }

        var amount = new AmountWithBreakdown
        {
            CurrencyCode = request.CurrencyCode,
            MValue = request.Total.ToString("F2", CultureInfo.InvariantCulture),
        };

        var purchaseUnit = new PurchaseUnitRequest
        {
            ReferenceId = request.BasketId,
            CustomId = request.BasketId,
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
            PaypalRequestId = request.IdempotencyKey,
            Prefer = "return=representation",
        };

        var response = await _client.OrdersController.CreateOrderAsync(input, cancellationToken: cancellationToken);

        var order = response.Data;
        if (order is null || string.IsNullOrWhiteSpace(order.Id))
        {
            throw new InvalidOperationException("PayPal did not return a valid order.");
        }

        var approvalLink = order.Links?
            .FirstOrDefault(link =>
                string.Equals(link.Rel, "approve", StringComparison.OrdinalIgnoreCase));

        if (approvalLink is null || string.IsNullOrWhiteSpace(approvalLink.Href))
        {
            throw new InvalidOperationException("PayPal did not provide an approval link for this order.");
        }

        return new PayPalOrderResponse(order.Id, approvalLink.Href);
    }
}

public interface IPayPalCheckoutService
{
    /// <summary>
    /// Creates a PayPal order for the current basket and user.
    /// The concrete implementation will map basket contents to a PayPal-ready request.
    /// </summary>
    /// <param name="basketId">The identifier of the basket.</param>
    /// <param name="userId">The identifier of the current user/buyer.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The PayPal order identifier and approval URL.</returns>
    Task<PayPalOrderResponse> CreateOrderForBasketAsync(string basketId, string userId, CancellationToken cancellationToken = default);
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

    public async Task<PayPalOrderResponse> CreateOrderForBasketAsync(string basketId, string userId, CancellationToken cancellationToken = default)
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
        var items = new List<PayPalOrderItem>();
        decimal total = 0m;

        foreach (var item in basketItems)
        {
            var lineTotal = item.UnitPrice * item.Quantity;
            total += lineTotal;

            items.Add(new PayPalOrderItem(
                Name: item.ProductName,
                Quantity: item.Quantity,
                UnitPrice: item.UnitPrice,
                CurrencyCode: DefaultCurrencyCode));
        }

        try
        {
            var request = new PayPalOrderRequest(
                BasketId: basketId,
                UserId: userId,
                Items: items,
                Total: total,
                CurrencyCode: DefaultCurrencyCode,
                IdempotencyKey: BuildIdempotencyKey(basketId, userId));

            var response = await _ordersClient.CreateOrderAsync(request, cancellationToken);

            if (!string.IsNullOrWhiteSpace(response.PaypalOrderId))
            {
                var session = new PayPalCheckoutSession(
                    PaypalOrderId: response.PaypalOrderId,
                    BasketId: basketId,
                    UserId: userId,
                    CreatedAtUtc: DateTime.UtcNow);

                await _sessionStore.StoreSessionAsync(session, cancellationToken);
            }

            return response;
        }
        catch (Exception ex)
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

