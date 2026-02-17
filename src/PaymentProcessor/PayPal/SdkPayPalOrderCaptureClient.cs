using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Http.Response;
using PaypalServerSdk.Standard.Models;

namespace eShop.PaymentProcessor.PayPal;

/// <summary>
/// PayPal order capture client implemented with the official PayPal Server SDK.
/// </summary>
public sealed class SdkPayPalOrderCaptureClient : IPayPalOrderCaptureClient
{
    private readonly PaypalServerSdkClient _client;
    private readonly ILogger<SdkPayPalOrderCaptureClient> _logger;

    public SdkPayPalOrderCaptureClient(
        PaypalServerSdkClient client,
        ILogger<SdkPayPalOrderCaptureClient> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> CaptureOrderAsync(
        string paypalOrderId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paypalOrderId))
        {
            throw new ArgumentException("PayPal order ID must be provided.", nameof(paypalOrderId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var input = new CaptureOrderInput
        {
            Id = paypalOrderId,
            PaypalRequestId = idempotencyKey,
            Prefer = "return=representation",
        };

        try
        {
            ApiResponse<Order> response = await _client.OrdersController.CaptureOrderAsync(input);
            var order = response.Data;

            if (order is null)
            {
                _logger.LogWarning(
                    "PayPal capture returned no order for PayPalOrderId {PaypalOrderId}.",
                    paypalOrderId);
                return false;
            }

            if (order.Status == OrderStatus.Completed)
            {
                return true;
            }

            _logger.LogWarning(
                "PayPal capture for PayPalOrderId {PaypalOrderId} completed with status {Status}.",
                paypalOrderId,
                order.Status);

            return false;
        }
        catch (ApiException ex)
        {
            _logger.LogError(
                ex,
                "PayPal capture failed for PayPalOrderId {PaypalOrderId}.",
                paypalOrderId);

            return false;
        }
    }
}

