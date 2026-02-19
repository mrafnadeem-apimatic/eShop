namespace eShop.PaymentProcessor.PayPal;

public interface IPayPalOrderCaptureClient
{
    /// <summary>
    /// Captures a PayPal order and returns whether the capture
    /// completed successfully.
    /// </summary>
    /// <param name="paypalOrderId">The PayPal order identifier.</param>
    /// <param name="idempotencyKey">
    /// A client-provided idempotency key used for safely retrying capture requests.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> when the PayPal order is captured successfully; otherwise <c>false</c>.
    /// </returns>
    Task<bool> CaptureOrderAsync(string paypalOrderId, string idempotencyKey, CancellationToken cancellationToken = default);
}

