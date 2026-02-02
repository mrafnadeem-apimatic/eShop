#nullable enable
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;
using PaypalServerSdk.Standard.Models;

namespace eShop.PaymentProcessor;

/// <summary>
/// Production implementation of <see cref="IPayPalOrdersApi"/> that talks to PayPal
/// using the official PayPal .NET Server SDK.
/// </summary>
public sealed class SdkPayPalOrdersApi(IOptionsMonitor<PaymentOptions> options) : IPayPalOrdersApi
{
    private readonly IOptionsMonitor<PaymentOptions> _options = options;

    private readonly Lazy<PaypalServerSdkClient> _client =
        new(() => CreatePayPalClient(options.CurrentValue));

    public async Task<bool> CaptureOrderAsync(string paypalOrderId, CancellationToken cancellationToken = default)
    {
        var client = _client.Value;

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
}

