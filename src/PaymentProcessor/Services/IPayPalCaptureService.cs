namespace eShop.PaymentProcessor.Services;

/// <summary>
/// Captures a previously approved PayPal order. Used by PaymentProcessor after stock confirmation.
/// </summary>
public interface IPayPalCaptureService
{
    /// <summary>
    /// Captures payment for the given PayPal order.
    /// </summary>
    /// <param name="paypalOrderId">The PayPal order ID from the create-order response.</param>
    /// <param name="orderId">The local order ID (used for idempotency key).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if capture succeeded; false otherwise (e.g. not configured, or PayPal API error).</returns>
    Task<bool> CaptureOrderAsync(string paypalOrderId, int orderId, CancellationToken cancellationToken = default);
}
