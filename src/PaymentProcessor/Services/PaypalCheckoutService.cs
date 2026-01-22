using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;
using PaypalServerSdk.Standard.Controllers;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Http.Response;
using PaypalServerSdk.Standard.Models;
using System.Globalization;

namespace eShop.PaymentProcessor.Services;

public class PaypalCheckoutService
{
    private readonly PaymentOptions _options;
    private readonly ILogger<PaypalCheckoutService> _logger;
    private readonly PaypalServerSdkClient _client;
    private readonly OrdersController _ordersController;
    private readonly bool _isConfigured;

    public PaypalCheckoutService(IOptionsMonitor<PaymentOptions> optionsMonitor, ILogger<PaypalCheckoutService> logger)
    {
        _options = optionsMonitor.CurrentValue;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.PaypalClientId) ||
            string.IsNullOrWhiteSpace(_options.PaypalClientSecret))
        {
            _logger.LogWarning("PayPal credentials are not configured. PayPal operations will be skipped.");
            _isConfigured = false;
            return;
        }

        _client = new PaypalServerSdkClient.Builder()
            .ClientCredentialsAuth(
                new ClientCredentialsAuthModel.Builder(
                    _options.PaypalClientId,
                    _options.PaypalClientSecret
                ).Build())
            .Environment(_options.UseSandbox
                ? PaypalServerSdk.Standard.Environment.Sandbox
                : PaypalServerSdk.Standard.Environment.Production)
            .LoggingConfig(config => config
                .LogLevel(LogLevel.Information)
                .RequestConfig(reqConfig => reqConfig.Body(false))
                .ResponseConfig(respConfig => respConfig.Headers(false)))
            .Build();

        _ordersController = _client.OrdersController;
        _isConfigured = true;
    }

    public bool IsConfigured => _isConfigured;

    public async Task<(string PaypalOrderId, string ApprovalLink)> CreateOrderAsync(
        decimal amount,
        string currencyCode,
        string returnUrl = null,
        string cancelUrl = null)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("PayPal client is not configured.");
        }

        var amountValue = amount.ToString("F2", CultureInfo.InvariantCulture);

        var orderRequest = new OrderRequest
        {
            Intent = CheckoutPaymentIntent.Capture,
            PurchaseUnits = new List<PurchaseUnitRequest>
            {
                new PurchaseUnitRequest
                {
                    Amount = new AmountWithBreakdown
                    {
                        CurrencyCode = currencyCode,
                        MValue = amountValue
                    }
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(returnUrl) || !string.IsNullOrWhiteSpace(cancelUrl))
        {
            orderRequest.ApplicationContext = new OrderApplicationContext
            {
                ReturnUrl = returnUrl,
                CancelUrl = cancelUrl
            };
        }

        var createOrderInput = new CreateOrderInput
        {
            Body = orderRequest,
            Prefer = "return=minimal"
        };

        try
        {
            ApiResponse<Order> result = await _ordersController.CreateOrderAsync(createOrderInput);

            var orderId = result.Data.Id;
            var approvalLink = result.Data.Links?
                .FirstOrDefault(l => string.Equals(l.Rel, "approve", StringComparison.OrdinalIgnoreCase))
                ?.Href;

            _logger.LogInformation("Created PayPal order {OrderId} with status {Status}", orderId, result.Data.Status);

            return (orderId, approvalLink);
        }
        catch (ApiException e)
        {
            _logger.LogError(e, "Error creating PayPal order");
            if (e is ErrorException errorException)
            {
                _logger.LogError("PayPal error: {Name} - {Message} ({DebugId})", errorException.Name, errorException.Message, errorException.DebugId);
            }

            throw;
        }
    }

    public async Task<Order> GetOrderAsync(string paypalOrderId)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("PayPal client is not configured.");
        }

        var getOrderInput = new GetOrderInput
        {
            Id = paypalOrderId
        };

        try
        {
            ApiResponse<Order> result = await _ordersController.GetOrderAsync(getOrderInput);
            _logger.LogInformation("Retrieved PayPal order {OrderId} with status {Status}", result.Data.Id, result.Data.Status);
            return result.Data;
        }
        catch (ApiException e)
        {
            _logger.LogError(e, "Error retrieving PayPal order {OrderId}", paypalOrderId);
            if (e is ErrorException errorException)
            {
                _logger.LogError("PayPal error: {Name} - {Message} ({DebugId})", errorException.Name, errorException.Message, errorException.DebugId);
            }

            throw;
        }
    }

    public async Task<Order> CaptureOrderAsync(string paypalOrderId)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("PayPal client is not configured.");
        }

        var captureOrderInput = new CaptureOrderInput
        {
            Id = paypalOrderId,
            Prefer = "return=minimal"
        };

        try
        {
            ApiResponse<Order> result = await _ordersController.CaptureOrderAsync(captureOrderInput);

            _logger.LogInformation("Captured PayPal order {OrderId} with status {Status}", result.Data.Id, result.Data.Status);

            return result.Data;
        }
        catch (ApiException e)
        {
            _logger.LogError(e, "Error capturing PayPal order {OrderId}", paypalOrderId);
            if (e is ErrorException errorException)
            {
                _logger.LogError("PayPal error: {Name} - {Message} ({DebugId})", errorException.Name, errorException.Message, errorException.DebugId);
            }

            throw;
        }
    }
}


