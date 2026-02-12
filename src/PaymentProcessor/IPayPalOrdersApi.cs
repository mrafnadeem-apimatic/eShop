#nullable enable
namespace eShop.PaymentProcessor;

/// <summary>
/// Abstraction over the PayPal Orders API used by <see cref="PayPalPaymentService"/>.
/// This makes the payment service easy to unit test without calling the real PayPal endpoints.
/// </summary>
public interface IPayPalOrdersApi
{
    /// <summary>
    /// Captures a PayPal order and returns <c>true</c> when the capture completed successfully.
    /// </summary>
    Task<bool> CaptureOrderAsync(string paypalOrderId, CancellationToken cancellationToken = default);
}

