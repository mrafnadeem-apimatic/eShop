using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Models;

namespace eShop.PaymentProcessor.Services;

public sealed class PayPalCaptureService : IPayPalCaptureService
{
    private readonly PaypalServerSdkClient _client;
    private readonly ILogger<PayPalCaptureService> _logger;

    public PayPalCaptureService(PaypalServerSdkClient client, ILogger<PayPalCaptureService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> CaptureOrderAsync(string paypalOrderId, int orderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paypalOrderId))
        {
            _logger.LogWarning("PayPal capture skipped for order {OrderId}: PayPal order ID is missing.", orderId);
            return false;
        }

        try
        {
            var input = new CaptureOrderInput
            {
                Id = paypalOrderId,
                Prefer = "return=minimal",
                PaypalRequestId = $"capture-{orderId}",
            };

            var response = await _client.OrdersController.CaptureOrderAsync(input);
            // Successful capture returns 200 with order details; treat as success.
            if (response?.Data != null)
            {
                _logger.LogInformation("PayPal order {PayPalOrderId} captured successfully for order {OrderId}.", paypalOrderId, orderId);
                return true;
            }

            _logger.LogWarning("PayPal capture for order {OrderId} returned no data.", orderId);
            return false;
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "PayPal capture failed for order {OrderId}, PayPal order {PayPalOrderId}.", orderId, paypalOrderId);
            return false;
        }
    }
}
