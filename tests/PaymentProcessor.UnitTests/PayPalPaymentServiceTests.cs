#nullable enable

namespace eShop.PaymentProcessor.UnitTests;

/// <summary>
/// Unit tests for <see cref="PayPalPaymentService.ProcessPaymentAsync"/>.
/// These tests now mock <see cref="IPayPalOrdersApi"/> so no real HTTP calls are made.
/// </summary>
[TestClass]
public sealed class PayPalPaymentServiceTests
{
    [TestMethod]
    public async Task ProcessPaymentAsync_WhenPayPalCaptureReturnsTrue_ReturnsTrue()
    {
        // --- FIXED DEPENDENCIES (same across tests; only the capture result varies). ---
        int orderId;
        IOrderingApiClient orderingApiClient;
        IPayPalOrdersApi payPalOrdersApi;
        IOptionsMonitor<PaymentOptions> optionsMonitor;
        ILogger<PayPalPaymentService> logger;
        GetCommonDependencies(out orderId, out orderingApiClient, out payPalOrdersApi, out optionsMonitor, out logger);

        // Simulate a successful capture from PayPal.
        payPalOrdersApi
            .CaptureOrderAsync("test-order-123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var sut = new PayPalPaymentService(orderingApiClient, optionsMonitor, payPalOrdersApi, logger);

        // --- ACT & ASSERT. ---
        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsTrue(result);
    }

    private static void GetCommonDependencies(
        out int orderId,
        out IOrderingApiClient orderingApiClient,
        out IPayPalOrdersApi payPalOrdersApi,
        out IOptionsMonitor<PaymentOptions> optionsMonitor,
        out ILogger<PayPalPaymentService> logger)
    {
        orderId = 42;
        var orderDto = new OrderDto { OrderNumber = orderId, Total = 99.99m, PayPalOrderId = "test-order-123" };
        orderingApiClient = Substitute.For<IOrderingApiClient>();
        orderingApiClient.GetOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(orderDto);

        payPalOrdersApi = Substitute.For<IPayPalOrdersApi>();

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
    public async Task ProcessPaymentAsync_WhenPayPalCaptureReturnsFalse_ReturnsFalse()
    {
        // --- FIXED DEPENDENCIES. ---
        int orderId;
        IOrderingApiClient orderingApiClient;
        IPayPalOrdersApi payPalOrdersApi;
        IOptionsMonitor<PaymentOptions> optionsMonitor;
        ILogger<PayPalPaymentService> logger;
        GetCommonDependencies(out orderId, out orderingApiClient, out payPalOrdersApi, out optionsMonitor, out logger);

        // Simulate an unsuccessful capture from PayPal.
        payPalOrdersApi
            .CaptureOrderAsync("test-order-123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var sut = new PayPalPaymentService(orderingApiClient, optionsMonitor, payPalOrdersApi, logger);

        // --- ACT & ASSERT. ---
        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ProcessPaymentAsync_WhenPayPalOrdersApiThrows_ReturnsFalse()
    {
        // --- FIXED DEPENDENCIES. ---
        int orderId;
        IOrderingApiClient orderingApiClient;
        IPayPalOrdersApi payPalOrdersApi;
        IOptionsMonitor<PaymentOptions> optionsMonitor;
        ILogger<PayPalPaymentService> logger;
        GetCommonDependencies(out orderId, out orderingApiClient, out payPalOrdersApi, out optionsMonitor, out logger);

        // Simulate an exception from the PayPal Orders API client.
        payPalOrdersApi
            .CaptureOrderAsync("test-order-123", Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("Simulated failure"));

        var sut = new PayPalPaymentService(orderingApiClient, optionsMonitor, payPalOrdersApi, logger);

        // --- ACT & ASSERT. ---
        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsFalse(result);
    }
}
