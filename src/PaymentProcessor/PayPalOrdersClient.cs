#nullable enable
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Models;

namespace eShop.PaymentProcessor;

/// <summary>
/// Result of attempting to capture a PayPal order through the SDK.
/// </summary>
/// <param name="Succeeded">True when the PayPal order reached a COMPLETED state.</param>
/// <param name="Status">The raw PayPal order status returned by the SDK, if available.</param>
public sealed record PayPalCaptureResult(bool Succeeded, string? Status);

/// <summary>
/// Abstraction over the PayPal .NET Server SDK Orders controller used by the payment processor.
/// This allows the domain service to remain focused on business logic and simplifies unit testing.
/// </summary>
public interface IPayPalOrdersClient
{
    Task<PayPalCaptureResult> CaptureOrderAsync(string paypalOrderId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IPayPalOrdersClient"/> backed by <see cref="PaypalServerSdkClient"/>.
/// </summary>
public sealed class PayPalOrdersClient(PaypalServerSdkClient client, ILogger<PayPalOrdersClient> logger)
    : IPayPalOrdersClient
{
    private readonly PaypalServerSdkClient _client = client;
    private readonly ILogger<PayPalOrdersClient> _logger = logger;

    public async Task<PayPalCaptureResult> CaptureOrderAsync(string paypalOrderId, CancellationToken cancellationToken = default)
    {
        var captureInput = new CaptureOrderInput
        {
            Id = paypalOrderId,
            Prefer = "return=minimal",
        };

        try
        {
            var response = await _client.OrdersController.CaptureOrderAsync(captureInput);

            var order = response.Data;
            var statusText = order?.Status?.ToString();
            var completed = string.Equals(statusText, "COMPLETED", StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation(
                "Captured PayPal order {PayPalOrderId} with status {Status} (success: {Success})",
                paypalOrderId,
                statusText,
                completed);

            return new PayPalCaptureResult(completed, statusText);
        }
        catch (ApiException e)
        {
            _logger.LogError(e, "PayPal SDK API exception while capturing order {PayPalOrderId}", paypalOrderId);

            if (e is ErrorException error)
            {
                // Log additional diagnostic details (no credentials).
                _logger.LogError(
                    "PayPal SDK error details for order {PayPalOrderId}. Name={Name}, Message={Message}, DebugId={DebugId}",
                    paypalOrderId,
                    error.Name,
                    error.Message,
                    error.DebugId);
            }

            return new PayPalCaptureResult(false, null);
        }
    }
}

