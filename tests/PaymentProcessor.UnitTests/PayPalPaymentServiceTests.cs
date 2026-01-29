#nullable enable
using System.Text.Json;

namespace eShop.PaymentProcessor.UnitTests;

/// <summary>
/// Unit tests for <see cref="PayPalPaymentService.ProcessPaymentAsync"/>.
/// Each test keeps dependencies fixed and varies only the PayPal capture response; the assertion verifies
/// that the service maps that response correctly to true (success) or false (failure).
/// </summary>
[TestClass]
public sealed class PayPalPaymentServiceTests
{
    [TestMethod]
    public async Task ProcessPaymentAsync_WhenPayPalCaptureReturnsCompleted_ReturnsTrue()
    {
        // --- UNDER TEST: PayPal capture returns 200 + status "COMPLETED" → service must return true. ---
        var handler = new MockPayPalHttpHandler(tokenSuccess: true, captureStatus: "COMPLETED");

        // --- FIXED DEPENDENCIES (same across tests; not what we are asserting on). ---
        var orderId = 42;
        var orderDto = new OrderDto { OrderNumber = orderId, Total = 99.99m, PayPalOrderId = "test-order-123" };
        var orderingApiClient = Substitute.For<IOrderingApiClient>();
        orderingApiClient.GetOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(orderDto);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com/") };
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("paypal").Returns(httpClient);

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

        var sut = new PayPalPaymentService(orderingApiClient, httpClientFactory, optionsMonitor, logger);

        // --- ACT & ASSERT. ---
        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ProcessPaymentAsync_WhenPayPalCaptureReturnsNonCompleted_ReturnsFalse()
    {
        // --- UNDER TEST: PayPal capture returns 200 but status is not "COMPLETED" (e.g. "OTHER") → service must return false. ---
        var handler = new MockPayPalHttpHandler(tokenSuccess: true, captureStatus: "OTHER");

        // --- FIXED DEPENDENCIES. ---
        var orderId = 42;
        var orderDto = new OrderDto { OrderNumber = orderId, Total = 49.99m, PayPalOrderId = "test-order-456" };
        var orderingApiClient = Substitute.For<IOrderingApiClient>();
        orderingApiClient.GetOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(orderDto);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com/") };
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("paypal").Returns(httpClient);

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

        var sut = new PayPalPaymentService(orderingApiClient, httpClientFactory, optionsMonitor, logger);

        // --- ACT & ASSERT. ---
        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ProcessPaymentAsync_WhenPayPalCaptureReturnsFailureStatusCode_ReturnsFalse()
    {
        // --- UNDER TEST: PayPal capture returns a non-success HTTP status (e.g. 404) → service must return false. ---
        var handler = new MockPayPalHttpHandler(tokenSuccess: true, captureHttpStatus: HttpStatusCode.NotFound);

        // --- FIXED DEPENDENCIES. ---
        var orderId = 42;
        var orderDto = new OrderDto { OrderNumber = orderId, Total = 29.99m, PayPalOrderId = "test-order-789" };
        var orderingApiClient = Substitute.For<IOrderingApiClient>();
        orderingApiClient.GetOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(orderDto);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com/") };
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("paypal").Returns(httpClient);

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

        var sut = new PayPalPaymentService(orderingApiClient, httpClientFactory, optionsMonitor, logger);

        // --- ACT & ASSERT. ---
        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsFalse(result);
    }

    /// <summary>
    /// Mocks the PayPal API: token endpoint and capture endpoint. Used to control the *only* varying input
    /// in these tests (capture response status/body). Token is always successful in the current tests.
    /// </summary>
    private sealed class MockPayPalHttpHandler : HttpMessageHandler
    {
        private readonly bool _tokenSuccess;
        private readonly string? _captureStatus;
        private readonly HttpStatusCode _captureHttpStatus;

        public MockPayPalHttpHandler(bool tokenSuccess = true, string? captureStatus = "COMPLETED", HttpStatusCode captureHttpStatus = HttpStatusCode.OK)
        {
            _tokenSuccess = tokenSuccess;
            _captureStatus = captureStatus;
            _captureHttpStatus = captureHttpStatus;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.Contains("oauth2/token") == true)
            {
                return Task.FromResult(new HttpResponseMessage(_tokenSuccess ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)
                {
                    Content = _tokenSuccess ? new StringContent("""{"access_token":"test-token"}""") : null
                });
            }

            if (request.RequestUri?.AbsolutePath.Contains("/capture") == true)
            {
                var body = _captureStatus is not null
                    ? JsonSerializer.Serialize(new { status = _captureStatus })
                    : """{"status":null}""";
                return Task.FromResult(new HttpResponseMessage(_captureHttpStatus)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
