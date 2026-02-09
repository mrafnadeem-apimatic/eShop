using System.Net;
using eShop.WebApp.Services;

namespace WebApp.UnitTests.Services;

[TestClass]
public sealed class OrderingServiceTests
{
    [TestMethod]
    public async Task CreatePayPalOrderAsync_sends_request_to_expected_endpoint_and_returns_order_id()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"payPalOrderId\":\"PAYPAL-123\"}")
            }
        };

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com/")
        };

        var service = new OrderingService(httpClient);
        var request = new CreatePayPalOrderRequest(Amount: 10m, Currency: "USD");

        // Act
        var result = await service.CreatePayPalOrderAsync(request, CancellationToken.None);

        // Assert
        Assert.AreEqual("PAYPAL-123", result);
        Assert.IsNotNull(handler.LastRequest);
        Assert.AreEqual(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.AreEqual("/api/payments/paypal/create-order", handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    [TestMethod]
    public async Task CreatePayPalOrderAsync_throws_when_response_missing_order_id()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"payPalOrderId\":\"\"}")
            }
        };

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com/")
        };

        var service = new OrderingService(httpClient);
        var request = new CreatePayPalOrderRequest(Amount: 5m, Currency: "USD");

        // Act & Assert
        try
        {
            await service.CreatePayPalOrderAsync(request, CancellationToken.None);
            Assert.Fail("Expected InvalidOperationException when PayPal order id is missing.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [TestMethod]
    public async Task CreatePayPalOrderAsync_throws_when_backend_returns_non_success_status_code()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        };

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com/")
        };

        var service = new OrderingService(httpClient);
        var request = new CreatePayPalOrderRequest(Amount: 5m, Currency: "USD");

        // Act & Assert
        try
        {
            await service.CreatePayPalOrderAsync(request, CancellationToken.None);
            Assert.Fail("Expected HttpRequestException when backend returns non-success status code.");
        }
        catch (HttpRequestException)
        {
        }
    }

    [TestMethod]
    public async Task CheckoutWithPayPalAsync_sends_request_to_expected_endpoint_with_request_id_header()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        };

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com/")
        };

        var service = new OrderingService(httpClient);

        var items = new List<BasketItem>
        {
            new()
            {
                Id = "1",
                ProductId = 1,
                ProductName = "Item",
                UnitPrice = 10,
                OldUnitPrice = 9,
                Quantity = 1
            }
        };

        var request = new CheckoutWithPayPalRequest(
            UserId: "user-1",
            UserName: "Test User",
            City: "City",
            Street: "Street",
            State: "State",
            Country: "Country",
            ZipCode: "12345",
            PayPalOrderId: "PAYPAL-123",
            Items: items);

        var requestId = Guid.NewGuid();

        // Act
        await service.CheckoutWithPayPalAsync(request, requestId, CancellationToken.None);

        // Assert
        Assert.IsNotNull(handler.LastRequest);
        Assert.AreEqual(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.AreEqual("/api/Orders/checkout-paypal", handler.LastRequest!.RequestUri!.PathAndQuery);

        Assert.IsTrue(handler.LastRequest!.Headers.TryGetValues("x-requestid", out var values));
        Assert.AreEqual(requestId.ToString(), values.Single());
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

