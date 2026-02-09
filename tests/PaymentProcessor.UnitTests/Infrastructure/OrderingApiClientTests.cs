namespace eShop.PaymentProcessor.UnitTests.Infrastructure;

[TestClass]
public sealed class OrderingApiClientTests
{
    [TestMethod]
    public async Task IsOrderAlreadyPaidAsync_ReturnsTrue_WhenOrderStatusIsPaid()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"orderNumber": 1, "status": "Paid"}""")
            }
        };

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com/")
        };

        var logger = Substitute.For<ILogger<OrderingApiClient>>();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var optionsMonitor = Substitute.For<IOptionsMonitor<IdentityServiceOptions>>();
        optionsMonitor.CurrentValue.Returns(new IdentityServiceOptions());
        var tokenLogger = Substitute.For<ILogger<ServiceToServiceTokenProvider>>();
        var tokenProvider = new ServiceToServiceTokenProvider(httpClientFactory, optionsMonitor, tokenLogger);
        var client = new OrderingApiClient(httpClient, tokenProvider, logger);

        // Act
        var result = await client.IsOrderAlreadyPaidAsync(1, CancellationToken.None);

        // Assert
        Assert.IsTrue(result);
        Assert.IsNotNull(handler.LastRequest);
        Assert.AreEqual("/api/orders/1?api-version=1.0", handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    [TestMethod]
    public async Task IsOrderAlreadyPaidAsync_ReturnsFalse_WhenOrderStatusIsNotPaid()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"orderNumber": 2, "status": "Cancelled"}""")
            }
        };

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com/")
        };

        var logger = Substitute.For<ILogger<OrderingApiClient>>();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var optionsMonitor = Substitute.For<IOptionsMonitor<IdentityServiceOptions>>();
        optionsMonitor.CurrentValue.Returns(new IdentityServiceOptions());
        var tokenLogger = Substitute.For<ILogger<ServiceToServiceTokenProvider>>();
        var tokenProvider = new ServiceToServiceTokenProvider(httpClientFactory, optionsMonitor, tokenLogger);
        var client = new OrderingApiClient(httpClient, tokenProvider, logger);

        // Act
        var result = await client.IsOrderAlreadyPaidAsync(2, CancellationToken.None);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task IsOrderAlreadyPaidAsync_ReturnsFalse_WhenOrderNotFound()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NotFound)
        };

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com/")
        };

        var logger = Substitute.For<ILogger<OrderingApiClient>>();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var optionsMonitor = Substitute.For<IOptionsMonitor<IdentityServiceOptions>>();
        optionsMonitor.CurrentValue.Returns(new IdentityServiceOptions());
        var tokenLogger = Substitute.For<ILogger<ServiceToServiceTokenProvider>>();
        var tokenProvider = new ServiceToServiceTokenProvider(httpClientFactory, optionsMonitor, tokenLogger);
        var client = new OrderingApiClient(httpClient, tokenProvider, logger);

        // Act
        var result = await client.IsOrderAlreadyPaidAsync(3, CancellationToken.None);

        // Assert
        Assert.IsFalse(result);
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public HttpResponseMessage ResponseToReturn { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(ResponseToReturn);
        }
    }
}

