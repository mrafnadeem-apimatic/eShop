#nullable enable
using System.Text.Json;

namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public sealed class PayPalPaymentServiceTests
{
    [TestMethod]
    public async Task ProcessPaymentAsync_WhenPayPalCaptureReturnsCompleted_ReturnsTrue()
    {
        var orderId = 42;
        var paypalOrderId = "test-order-123";
        var orderDto = new OrderDto { OrderNumber = orderId, Total = 99.99m, PayPalOrderId = paypalOrderId };

        var orderingApiClient = Substitute.For<IOrderingApiClient>();   // dependency 1: IOrderingApiClient
        orderingApiClient.GetOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(orderDto);

        var handler = new MockPayPalHttpHandler(tokenSuccess: true, captureStatus: "COMPLETED");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com/") };
        var httpClientFactory = Substitute.For<IHttpClientFactory>();   // dependency 2: HttpClientFactory
        httpClientFactory.CreateClient("paypal").Returns(httpClient);

        var paymentOptions = new PaymentOptions
        {
            UsePayPal = true,
            PayPalClientId = "test-client-id",
            PayPalClientSecret = "test-secret",
            PayPalEnvironment = "Sandbox"
        };
        var optionsMonitor = Substitute.For<IOptionsMonitor<PaymentOptions>>();   // dependency 3: IOptionsMonitor<PaymentOptions>
        optionsMonitor.CurrentValue.Returns(paymentOptions);
        var logger = Substitute.For<ILogger<PayPalPaymentService>>();   // dependency 4: ILogger<PayPalPaymentService>

        var sut = new PayPalPaymentService(orderingApiClient, httpClientFactory, optionsMonitor, logger);

        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ProcessPaymentAsync_WhenPayPalCaptureReturnsNonCompleted_ReturnsFalse()
    {
        var orderId = 42;
        var paypalOrderId = "test-order-456";
        var orderDto = new OrderDto { OrderNumber = orderId, Total = 49.99m, PayPalOrderId = paypalOrderId };

        var orderingApiClient = Substitute.For<IOrderingApiClient>();
        orderingApiClient.GetOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(orderDto);

        var handler = new MockPayPalHttpHandler(tokenSuccess: true, captureStatus: "OTHER");
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

        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ProcessPaymentAsync_WhenPayPalCaptureReturnsFailureStatusCode_ReturnsFalse()
    {
        var orderId = 42;
        var paypalOrderId = "test-order-789";
        var orderDto = new OrderDto { OrderNumber = orderId, Total = 29.99m, PayPalOrderId = paypalOrderId };

        var orderingApiClient = Substitute.For<IOrderingApiClient>();
        orderingApiClient.GetOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(orderDto);

        var handler = new MockPayPalHttpHandler(tokenSuccess: true, captureHttpStatus: HttpStatusCode.NotFound);
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

        var result = await sut.ProcessPaymentAsync(orderId);

        Assert.IsFalse(result);
    }

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
