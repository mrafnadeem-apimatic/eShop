#nullable enable
namespace eShop.PaymentProcessor;

public sealed class PayPalPaymentService(
    IOrderingApiClient orderingApiClient,
    IOptionsMonitor<PaymentOptions> options,
    IPayPalOrdersApi payPalOrdersApi,
    ILogger<PayPalPaymentService> logger) : IPaymentService
{
    private readonly IOrderingApiClient _orderingApiClient = orderingApiClient;
    private readonly IOptionsMonitor<PaymentOptions> _options = options;
    private readonly IPayPalOrdersApi _payPalOrdersApi = payPalOrdersApi;
    private readonly ILogger<PayPalPaymentService> _logger = logger;

    public async Task<bool> ProcessPaymentAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var settings = _options.CurrentValue;

        // If PayPal is not configured, fall back to the existing simulated payment behavior.
        if (!settings.UsePayPal ||
            string.IsNullOrWhiteSpace(settings.PayPalClientId) ||
            string.IsNullOrWhiteSpace(settings.PayPalClientSecret))
        {
            _logger.LogInformation(
                "PayPal not configured or disabled; falling back to PaymentSucceeded flag for order {OrderId}",
                orderId);

            return settings.PaymentSucceeded;
        }

        var order = await _orderingApiClient.GetOrderAsync(orderId, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning(
                "Unable to load order {OrderId} from Ordering.API; marking payment as failed",
                orderId);
            return false;
        }

        if (string.IsNullOrWhiteSpace(order.PayPalOrderId))
        {
            _logger.LogWarning(
                "Order {OrderId} does not have an associated PayPal order ID; cannot capture payment.",
                orderId);
            return false;
        }

        try
        {
            var captured = await _payPalOrdersApi.CaptureOrderAsync(order.PayPalOrderId, cancellationToken);

            _logger.LogInformation(
                "PayPal capture for order {OrderId} completed with result: {Result}",
                orderId,
                captured);

            return captured;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing PayPal payment for order {OrderId}", orderId);
            return false;
        }
    }
}
