using System.Threading;
using System.Threading.Tasks;
using eShop.Ordering.API.Infrastructure.PayPal;

namespace eShop.Ordering.FunctionalTests;

internal sealed class TestPayPalClient : IPayPalClient
{
    public Task<string> CreateOrderAsync(
        decimal amount,
        string currencyCode,
        string reference = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("TEST_PAYPAL_ORDER_ID");
    }

    public Task<PayPalCaptureResult> CaptureOrderAsync(
        string paypalOrderId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PayPalCaptureResult(
            Success: true,
            PayPalOrderId: paypalOrderId,
            CaptureId: "TEST_CAPTURE_ID",
            CapturedAmount: null,
            CurrencyCode: "USD",
            Status: "COMPLETED"));
    }
}


