#nullable enable

namespace eShop.PaymentProcessor.UnitTests;

/// <summary>
/// Unit tests for <see cref="PayPalPaymentService.ProcessPaymentAsync"/>.
/// Each test keeps dependencies fixed and varies only the PayPal capture result; the assertion verifies
/// that the service maps that result correctly to true (success) or false (failure).
/// </summary>
[TestClass]
public sealed class PayPalPaymentServiceTests
{
    [TestMethod]
    public async Task ProcessPaymentAsync_WhenPayPalCaptureReturnsCompleted_ReturnsTrue()
    {
        // --- UNDER TEST: PayPal capture result indicates COMPLETED → service must return true. ---
        int orderId;
        IOrderingApiClient orderingApiClient;
        IPayPalOrdersClient payPalOrdersClient;
        IOptionsMonitor<PaymentOptions> optionsMonitor;
        ILogger<PayPalPaymentService> logger;
        GetCommonDependencies(
            new PayPalCaptureResult(true, "COMPLETED"),
            out orderId,
            out orderingApiClient,
            out payPalOrdersClient,
            out optionsMonitor,
            out logger);

        var sut = new PayPalPaymentService(orderingApiClient, payPalOrdersClient, optionsMonitor, logger);

        // --- ACT & ASSERT. ---
        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsTrue(result);
    }

    private static void GetCommonDependencies(
        PayPalCaptureResult captureResult,
        out int orderId,
        out IOrderingApiClient orderingApiClient,
        out IPayPalOrdersClient payPalOrdersClient,
        out IOptionsMonitor<PaymentOptions> optionsMonitor,
        out ILogger<PayPalPaymentService> logger)
    {
        orderId = 42;
        var orderDto = new OrderDto { OrderNumber = orderId, Total = 99.99m, PayPalOrderId = "test-order-123" };
        orderingApiClient = Substitute.For<IOrderingApiClient>();
        orderingApiClient.GetOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(orderDto);

        payPalOrdersClient = Substitute.For<IPayPalOrdersClient>();
        payPalOrdersClient
            .CaptureOrderAsync(orderDto.PayPalOrderId!, Arg.Any<CancellationToken>())
            .Returns(captureResult);

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

    [TestMethod]
    public async Task ProcessPaymentAsync_WhenPayPalCaptureReturnsNonCompleted_ReturnsFalse()
    {
        // --- UNDER TEST: PayPal capture result is not COMPLETED (e.g. "OTHER") → service must return false. ---
        int orderId;
        IOrderingApiClient orderingApiClient;
        IPayPalOrdersClient payPalOrdersClient;
        IOptionsMonitor<PaymentOptions> optionsMonitor;
        ILogger<PayPalPaymentService> logger;
        GetCommonDependencies(
            new PayPalCaptureResult(false, "OTHER"),
            out orderId,
            out orderingApiClient,
            out payPalOrdersClient,
            out optionsMonitor,
            out logger);

        var sut = new PayPalPaymentService(orderingApiClient, payPalOrdersClient, optionsMonitor, logger);

        // --- ACT & ASSERT. ---
        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ProcessPaymentAsync_WhenPayPalCaptureThrows_ReturnsFalse()
    {
        // --- UNDER TEST: PayPal capture throws an exception → service must return false. ---
        int orderId = 42;
        var orderDto = new OrderDto { OrderNumber = orderId, Total = 99.99m, PayPalOrderId = "test-order-123" };

        var orderingApiClient = Substitute.For<IOrderingApiClient>();
        orderingApiClient.GetOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(orderDto);

        var payPalOrdersClient = Substitute.For<IPayPalOrdersClient>();
        payPalOrdersClient
            .CaptureOrderAsync(orderDto.PayPalOrderId!, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PayPalCaptureResult>(new InvalidOperationException("Simulated failure")));

        var paymentOptions = new PaymentOptions
        {
            UsePayPal = true,
            PayPalClientId = "test-client-id",
            PayPalClientSecret = "test-secret",
            PayPalEnvironment = "Sandbox"
        };
        var optionsMonitor = Substitute.For<IOptionsMonitor<PaymentOptions>>();
        optionsMonitor.CurrentValue.Returns(paymentOptions);

        var logger = Substitute.For<ILogger<PayPalPaymentService>>();

        var sut = new PayPalPaymentService(orderingApiClient, payPalOrdersClient, optionsMonitor, logger);

        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsFalse(result);
    }
}
