#nullable enable
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;
using PaypalServerSdk.Standard.Models;

namespace eShop.PaymentProcessor;

public sealed class PayPalPaymentService(
    IOrderingApiClient orderingApiClient,
    IOptionsMonitor<PaymentOptions> options,
    ILogger<PayPalPaymentService> logger) : IPaymentService
{
    private readonly IOrderingApiClient _orderingApiClient = orderingApiClient;
    private readonly IOptionsMonitor<PaymentOptions> _options = options;
    private readonly ILogger<PayPalPaymentService> _logger = logger;

    private readonly Lazy<PaypalServerSdkClient> _paypalClient =
        new(() => CreatePayPalClient(options.CurrentValue));

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
            var client = _paypalClient.Value;

            var captured = await CapturePayPalOrderAsync(
                client,
                order.PayPalOrderId,
                cancellationToken);

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

    private static PaypalServerSdkClient CreatePayPalClient(PaymentOptions settings)
    {
        var environment = settings.PayPalEnvironment?.Equals("Live", StringComparison.OrdinalIgnoreCase) == true
            ? PaypalServerSdk.Standard.Environment.Production
            : PaypalServerSdk.Standard.Environment.Sandbox;

        return new PaypalServerSdkClient.Builder()
            .ClientCredentialsAuth(
                new ClientCredentialsAuthModel.Builder(
                        settings.PayPalClientId!,
                        settings.PayPalClientSecret!)
                    .Build())
            .Environment(environment)
            .Build();
    }

    private static async Task<bool> CapturePayPalOrderAsync(
        PaypalServerSdkClient client,
        string paypalOrderId,
        CancellationToken cancellationToken)
    {
        var captureInput = new CaptureOrderInput
        {
            Id = paypalOrderId,
            // Ensure PayPal receives JSON, even when the body is effectively empty.
            ContentType = "application/json",
            Body = new OrderCaptureRequest(),
            Prefer = "return=representation",
        };

        var response = await client.OrdersController.CaptureOrderAsync(captureInput, cancellationToken);

        if (response.StatusCode is < 200 or >= 300)
        {
            return false;
        }

        var capturedOrder = response.Data;
        if (capturedOrder is null || capturedOrder.Status is null)
        {
            return false;
        }

        return capturedOrder.Status == OrderStatus.Completed;
    }
}

