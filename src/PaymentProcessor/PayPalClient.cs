using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;
using PaypalServerSdk.Standard.Controllers;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Models;

namespace eShop.PaymentProcessor;

public interface IPayPalClient
{
    Task<PayPalCaptureResult> CaptureOrderAsync(
        string paypalOrderId,
        CancellationToken cancellationToken = default);
}

public sealed record PayPalCaptureResult(
    bool Success,
    string PayPalOrderId,
    string CaptureId,
    decimal? CapturedAmount,
    string CurrencyCode,
    string Status);

public sealed class PayPalClient : IPayPalClient
{
    private readonly PaypalServerSdkClient _client;
    private readonly ILogger<PayPalClient> _logger;

    public PayPalClient(
        IOptions<PayPalOptions> options,
        ILogger<PayPalClient> logger)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        var payPalOptions = options.Value ?? throw new InvalidOperationException("PayPalOptions are not configured.");

        if (string.IsNullOrWhiteSpace(payPalOptions.ClientId))
        {
            throw new InvalidOperationException("PayPal client id is not configured.");
        }

        if (string.IsNullOrWhiteSpace(payPalOptions.ClientSecret))
        {
            throw new InvalidOperationException("PayPal client secret is not configured.");
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var environment = MapEnvironment(payPalOptions.Environment);

        _client = new PaypalServerSdkClient.Builder()
            .ClientCredentialsAuth(
                new ClientCredentialsAuthModel.Builder(
                        payPalOptions.ClientId,
                        payPalOptions.ClientSecret)
                    .Build())
            .Environment(environment)
            .LoggingConfig(config => config
                // Keep logging fairly light; payloads can contain PII.
                .LogLevel(LogLevel.Information)
                .RequestConfig(reqConfig => reqConfig.Body(false))
                .ResponseConfig(respConfig => respConfig.Headers(false)))
            .Build();
    }

    public async Task<PayPalCaptureResult> CaptureOrderAsync(
        string paypalOrderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paypalOrderId))
        {
            throw new ArgumentException("PayPal order id is required.", nameof(paypalOrderId));
        }

        var ordersController = _client.OrdersController;

        var captureOrderInput = new CaptureOrderInput
        {
            Id = paypalOrderId,
            Prefer = "return=representation",
        };

        try
        {
            var response = await ordersController.CaptureOrderAsync(captureOrderInput, cancellationToken);
            var order = response.Data;

            var purchaseUnit = order.PurchaseUnits?.FirstOrDefault();
            var capture = purchaseUnit?.Payments?.Captures?.FirstOrDefault();

            var statusEnum = (object)capture?.Status ?? order.Status;
            var status = statusEnum?.ToString();
            var success = string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase);

            decimal? capturedAmount = null;
            string currency = null;

            if (capture?.Amount is not null &&
                decimal.TryParse(
                    capture.Amount.MValue,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                capturedAmount = parsed;
                currency = capture.Amount.CurrencyCode;
            }

            return new PayPalCaptureResult(
                Success: success,
                PayPalOrderId: order.Id,
                CaptureId: capture?.Id,
                CapturedAmount: capturedAmount,
                CurrencyCode: currency,
                Status: status);
        }
        catch (ApiException ex)
        {
            _logger.LogError(
                ex,
                "Error capturing PayPal order {PayPalOrderId}.",
                paypalOrderId);

            return new PayPalCaptureResult(
                Success: false,
                PayPalOrderId: paypalOrderId,
                CaptureId: null,
                CapturedAmount: null,
                CurrencyCode: null,
                Status: "ERROR");
        }
    }

    private static PaypalServerSdk.Standard.Environment MapEnvironment(string environment)
    {
        return environment?.Trim().ToLowerInvariant() switch
        {
            "live" or "production" => PaypalServerSdk.Standard.Environment.Production,
            _ => PaypalServerSdk.Standard.Environment.Sandbox,
        };
    }
}

