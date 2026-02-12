using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Models;

namespace eShop.PaymentProcessor;

public interface IPayPalCaptureService
{
    /// <summary>
    /// Captures a previously approved PayPal order.
    /// </summary>
    /// <param name="paypalOrderId">The PayPal order identifier to capture.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the capture completes with status COMPLETED; otherwise <c>false</c>.</returns>
    Task<bool> CaptureOrderAsync(string paypalOrderId, CancellationToken cancellationToken = default);
}

/// <summary>
/// SDK-backed implementation that calls the PayPal Orders capture API using the
/// official PayPal .NET Server SDK.
/// </summary>
public sealed class PayPalCaptureService : IPayPalCaptureService
{
    private readonly PaypalServerSdkClient _client;
    private readonly ILogger<PayPalCaptureService> _logger;

    public PayPalCaptureService(PaypalServerSdkClient client, ILogger<PayPalCaptureService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> CaptureOrderAsync(string paypalOrderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paypalOrderId))
        {
            throw new ArgumentException("PayPal order id must be provided.", nameof(paypalOrderId));
        }

        var captureInput = new CaptureOrderInput
        {
            Id = paypalOrderId,
            Prefer = "return=minimal"
        };

        try
        {
            // The SDK honours the configured HTTP timeout; we do not pass an
            // explicit CancellationToken here because undoing payment capture is not guaranteed to succeed.
            var response = await _client.OrdersController.CaptureOrderAsync(captureInput, CancellationToken.None);

            var order = response.Data;
            var status = order?.Status;
            var completed = status == OrderStatus.Completed;

            if (!completed)
            {
                _logger.LogWarning(
                    "PayPal capture for order {PayPalOrderId} returned non-completed status {Status}.",
                    paypalOrderId,
                    status?.ToString());
            }

            return completed;
        }
        catch (ApiException ex)
        {
            _logger.LogError(
                ex,
                "PayPal API error while capturing order {PayPalOrderId}. Status code: {StatusCode}",
                paypalOrderId,
                ex.ResponseCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while capturing PayPal order {PayPalOrderId}.",
                paypalOrderId);
            return false;
        }
    }
}

