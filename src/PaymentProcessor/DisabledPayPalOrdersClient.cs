namespace eShop.PaymentProcessor;

/// <summary>
/// Fallback implementation of <see cref="IPayPalOrdersClient"/> used when PayPal
/// is disabled or not configured. This should never be invoked because the
/// payment service short-circuits to simulated payments in that case.
/// </summary>
public sealed class DisabledPayPalOrdersClient(ILogger<DisabledPayPalOrdersClient> logger)
    : IPayPalOrdersClient
{
    private readonly ILogger<DisabledPayPalOrdersClient> _logger = logger;

    public Task<PayPalCaptureResult> CaptureOrderAsync(string paypalOrderId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "DisabledPayPalOrdersClient was invoked for PayPal order {PayPalOrderId}. " +
            "PayPal should be disabled or not configured; returning unsuccessful result.",
            paypalOrderId);

        return Task.FromResult(new PayPalCaptureResult(false, null));
    }
}

