using System.Globalization;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Models;
using PaypalServerSdk.Standard;

namespace eShop.WebApp.PayPal;

public interface IPayPalCheckoutService
{
    /// <summary>
    /// Creates a PayPal checkout order for the specified total and currency,
    /// returning the PayPal order id and the approval URL the shopper should be
    /// redirected to in order to approve the payment.
    /// </summary>
    /// <param name="total">Basket total to charge.</param>
    /// <param name="currency">Three-letter ISO currency code (e.g. "USD").</param>
    /// <param name="returnUrl">Absolute URL that PayPal should redirect back to on approval.</param>
    /// <param name="cancelUrl">Absolute URL that PayPal should redirect back to on cancel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created PayPal order id and approval URL.</returns>
    Task<(string orderId, Uri approveLink)> CreateOrderAsync(
        decimal total,
        string currency,
        Uri returnUrl,
        Uri cancelUrl,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thin wrapper around the PayPal .NET Server SDK OrdersController used by the
/// WebApp's /paypal/pay endpoint to create checkout orders.
/// </summary>
public sealed class PayPalCheckoutService : IPayPalCheckoutService
{
    private readonly PaypalServerSdkClient _client;
    private readonly ILogger<PayPalCheckoutService> _logger;

    public PayPalCheckoutService(PaypalServerSdkClient client, ILogger<PayPalCheckoutService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<(string orderId, Uri approveLink)> CreateOrderAsync(
        decimal total,
        string currency,
        Uri returnUrl,
        Uri cancelUrl,
        CancellationToken cancellationToken = default)
    {
        if (total <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(total), "Total must be greater than zero.");
        }

        var ordersController = _client.OrdersController;

        var createOrderInput = new CreateOrderInput
        {
            Body = new OrderRequest
            {
                Intent = CheckoutPaymentIntent.Capture,
                PurchaseUnits =
                [
                    new PurchaseUnitRequest
                    {
                        Amount = new AmountWithBreakdown
                        {
                            CurrencyCode = currency,
                            MValue = total.ToString("F2", CultureInfo.InvariantCulture)
                        }
                    }
                ],
                ApplicationContext = new OrderApplicationContext
                {
                    ReturnUrl = returnUrl.ToString(),
                    CancelUrl = cancelUrl.ToString()
                }
            },
            // We want enough information back to extract the approval link.
            Prefer = "return=representation"
        };

        try
        {
            // The SDK internally honours the configured HTTP timeout; we rely on that
            // for bounding call duration and do not pass an additional CancellationToken
            // here because undoing order creation is not guaranteed to succeed.
            var response = await ordersController.CreateOrderAsync(createOrderInput, CancellationToken.None);

            var order = response.Data;
            if (order is null || string.IsNullOrWhiteSpace(order.Id))
            {
                _logger.LogError(
                    "PayPal SDK returned an invalid order response when creating order for amount {Total} {Currency}.",
                    total,
                    currency);
                throw new InvalidOperationException("Invalid PayPal order response.");
            }

            var approveHref = order.Links?
                .FirstOrDefault(l => string.Equals(l.Rel, "approve", StringComparison.OrdinalIgnoreCase))
                ?.Href;

            if (string.IsNullOrWhiteSpace(approveHref))
            {
                _logger.LogError(
                    "PayPal order {OrderId} does not contain an approval link.",
                    order.Id);
                throw new InvalidOperationException("No approval link in PayPal order response.");
            }

            Uri approveUri;
            try
            {
                approveUri = new Uri(approveHref, UriKind.Absolute);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Approval link for PayPal order {OrderId} is not a valid absolute URI: {ApproveHref}",
                    order.Id,
                    approveHref);
                throw new InvalidOperationException("Invalid approval link in PayPal order response.");
            }

            return (order.Id, approveUri);
        }
        catch (ApiException ex)
        {
            // Log key diagnostics without exposing sensitive data.
            _logger.LogError(
                ex,
                "PayPal API error while creating order for amount {Total} {Currency}. Status code: {StatusCode}",
                total,
                currency,
                ex.ResponseCode);

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while creating PayPal order for amount {Total} {Currency}.",
                total,
                currency);
            throw;
        }
    }
}

