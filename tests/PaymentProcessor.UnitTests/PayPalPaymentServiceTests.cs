#nullable enable

namespace eShop.PaymentProcessor.UnitTests;

/// <summary>
/// Unit tests for <see cref="PayPalPaymentService.ProcessPaymentAsync"/>.
/// Each test keeps dependencies fixed and varies only the result from <see cref="IPayPalCaptureService"/>;
/// the assertion verifies that the service maps that result correctly to true (success) or false (failure)
/// and preserves the existing simulated-payment behaviour when PayPal is disabled.
/// </summary>
[TestClass]
public sealed class PayPalPaymentServiceTests
{
    [TestMethod]
    public async Task ProcessPaymentAsync_WhenCaptureServiceReturnsTrue_ReturnsTrue()
    {
        // --- UNDER TEST: capture service reports success → service must return true. ---
        int orderId;
        IOrderingApiClient orderingApiClient;
        IPayPalCaptureService captureService;
        IOptionsMonitor<PaymentOptions> optionsMonitor;
        ILogger<PayPalPaymentService> logger;
        GetCommonDependencies(out orderId, out orderingApiClient, out captureService, out optionsMonitor, out logger);

        captureService.CaptureOrderAsync("test-order-123", Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = new PayPalPaymentService(orderingApiClient, captureService, optionsMonitor, logger);

        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ProcessPaymentAsync_WhenCaptureServiceReturnsFalse_ReturnsFalse()
    {
        // --- UNDER TEST: capture service reports failure → service must return false. ---
        int orderId;
        IOrderingApiClient orderingApiClient;
        IPayPalCaptureService captureService;
        IOptionsMonitor<PaymentOptions> optionsMonitor;
        ILogger<PayPalPaymentService> logger;
        GetCommonDependencies(out orderId, out orderingApiClient, out captureService, out optionsMonitor, out logger);

        captureService.CaptureOrderAsync("test-order-123", Arg.Any<CancellationToken>())
            .Returns(false);

        var sut = new PayPalPaymentService(orderingApiClient, captureService, optionsMonitor, logger);

        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ProcessPaymentAsync_WhenCaptureServiceThrows_ReturnsFalse()
    {
        // --- UNDER TEST: capture service throws → service must catch and return false. ---
        int orderId;
        IOrderingApiClient orderingApiClient;
        IPayPalCaptureService captureService;
        IOptionsMonitor<PaymentOptions> optionsMonitor;
        ILogger<PayPalPaymentService> logger;
        GetCommonDependencies(out orderId, out orderingApiClient, out captureService, out optionsMonitor, out logger);

        captureService
            .CaptureOrderAsync("test-order-123", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new Exception("Capture failed")));

        var sut = new PayPalPaymentService(orderingApiClient, captureService, optionsMonitor, logger);

        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ProcessPaymentAsync_WhenPayPalDisabled_UsesPaymentSucceededFlagAndSkipsCapture()
    {
        // --- UNDER TEST: PayPal disabled or not configured → simulated payment behaviour must be used. ---
        var orderId = 42;
        var orderingApiClient = Substitute.For<IOrderingApiClient>();
        var captureService = Substitute.For<IPayPalCaptureService>();

        var paymentOptions = new PaymentOptions
        {
            UsePayPal = false,
            PaymentSucceeded = true
        };

        var optionsMonitor = Substitute.For<IOptionsMonitor<PaymentOptions>>();
        optionsMonitor.CurrentValue.Returns(paymentOptions);

        var logger = Substitute.For<ILogger<PayPalPaymentService>>();

        var sut = new PayPalPaymentService(orderingApiClient, captureService, optionsMonitor, logger);

        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsTrue(result, "When PayPal is disabled, the service should return PaymentSucceeded.");
        await captureService.DidNotReceiveWithAnyArgs().CaptureOrderAsync(default!, default);
        await orderingApiClient.DidNotReceiveWithAnyArgs().GetOrderAsync(default, default);
    }

    private static void GetCommonDependencies(
        out int orderId,
        out IOrderingApiClient orderingApiClient,
        out IPayPalCaptureService captureService,
        out IOptionsMonitor<PaymentOptions> optionsMonitor,
        out ILogger<PayPalPaymentService> logger)
    {
        orderId = 42;
        var orderDto = new OrderDto { OrderNumber = orderId, Total = 99.99m, PayPalOrderId = "test-order-123" };

        orderingApiClient = Substitute.For<IOrderingApiClient>();
        orderingApiClient.GetOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(orderDto);

        captureService = Substitute.For<IPayPalCaptureService>();

        var paymentOptions = new PaymentOptions
        {
            UsePayPal = true,
            PayPalClientId = "test-client-id",
            PayPalClientSecret = "test-secret",
            PayPalEnvironment = "Sandbox"
        };

        optionsMonitor = Substitute.For<IOptionsMonitor<PaymentOptions>>();
        optionsMonitor.CurrentValue.Returns(paymentOptions);
        logger = Substitute.For<ILogger<PayPalPaymentService>>();
    }
}

