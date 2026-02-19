using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using eShop.Basket.API.Grpc;
using eShop.WebApp.Services;
using eShop.WebAppComponents.Catalog;
using eShop.WebAppComponents.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace eShop.WebApp.UnitTests;

[TestClass]
public class BasketStateCheckoutTests
{
    [TestMethod]
    public async Task CheckoutAsync_UsesPayPalPaymentMethod_And_PassesPayPalOrderIdToOrderingService()
    {
        // Arrange basket items in the underlying gRPC basket service.
        var basketClient = new TestBasketClient();
        basketClient.Items.Add((ProductId: 1, Quantity: 2));

        var basketService = new BasketService(basketClient);

        // Catalog contains a single item matching the basket.
        var catalogItems = new Dictionary<int, CatalogItem>
        {
            [1] = new(
                Id: 1,
                Name: "Item One",
                Description: "Test item",
                Price: 10.00m,
                PictureUrl: "https://example.test/image.png",
                CatalogBrandId: 1,
                CatalogBrand: new CatalogBrand(1, "Brand"),
                CatalogTypeId: 1,
                CatalogType: new CatalogItemType(1, "Type"))
        };

        var catalogHandler = new TestCatalogHttpMessageHandler(catalogItems);
        var catalogHttpClient = new HttpClient(catalogHandler)
        {
            BaseAddress = new Uri("http://catalog-api")
        };
        var catalogService = new CatalogService(catalogHttpClient);

        // Ordering service uses a handler that captures the outgoing HTTP request.
        var orderingHandler = new TestOrderingHttpMessageHandler();
        var orderingHttpClient = new HttpClient(orderingHandler)
        {
            BaseAddress = new Uri("http://ordering-api")
        };
        var orderingService = new OrderingService(orderingHttpClient);

        // Authenticated user with buyer id and name claims.
        var claims = new[]
        {
            new Claim("sub", "buyer-123"),
            new Claim("name", "Test User"),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestAuth"));
        var authProvider = new TestAuthenticationStateProvider(user);

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();

        var basketState = new BasketState(
            basketService,
            catalogService,
            orderingService,
            authProvider,
            httpContextAccessor);

        var checkoutInfo = new BasketCheckoutInfo
        {
            City = "Redmond",
            Street = "1 Main St",
            State = "WA",
            Country = "USA",
            ZipCode = "98052",
            CardTypeId = 1,
            PaymentMethod = "PayPal",
            PayPalOrderId = "PAYPAL-ORDER-123"
        };

        // Act
        await basketState.CheckoutAsync(checkoutInfo);

        // Assert: request id should be generated.
        Assert.AreNotEqual(Guid.Empty, checkoutInfo.RequestId);

        // Assert: ordering service was called once with the expected payload.
        var lastRequest = orderingHandler.LastRequest;
        Assert.IsNotNull(lastRequest);
        Assert.AreEqual(HttpMethod.Post, lastRequest.Method);
        Assert.AreEqual("/api/Orders/", lastRequest.RequestUri!.PathAndQuery);

        Assert.IsTrue(lastRequest.Headers.TryGetValues("x-requestid", out var requestIdHeaders));
        Assert.AreEqual(checkoutInfo.RequestId.ToString(), requestIdHeaders.Single());

        var createdOrder = await lastRequest.Content!.ReadFromJsonAsync<CreateOrderRequest>(TestContext.CancellationToken);
        Assert.IsNotNull(createdOrder);

        // User and payment metadata should flow through to the order.
        Assert.AreEqual("buyer-123", createdOrder.UserId);
        Assert.AreEqual("Test User", createdOrder.UserName);
        Assert.AreEqual("PayPal", createdOrder.PaymentMethod);
        Assert.AreEqual("PAYPAL-ORDER-123", createdOrder.PayPalOrderId);

        // Basket items should be mapped into order items with the same quantity and price.
        Assert.AreEqual(1, createdOrder.Items.Count);
        var orderItem = createdOrder.Items.Single();
        Assert.AreEqual(1, orderItem.ProductId);
        Assert.AreEqual("Item One", orderItem.ProductName);
        Assert.AreEqual(10.00m, orderItem.UnitPrice);
        Assert.AreEqual(2, orderItem.Quantity);

        // Basket should be cleared after checkout.
        Assert.AreEqual(0, basketClient.Items.Count);
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(user));
    }

    private sealed class TestCatalogHttpMessageHandler(
        IReadOnlyDictionary<int, CatalogItem> itemsById) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri is { } uri &&
                uri.AbsolutePath.EndsWith("api/catalog/items/by", StringComparison.OrdinalIgnoreCase))
            {
                var query = HttpUtility.ParseQueryString(uri.Query);
                var idValues = query.GetValues("ids") ?? Array.Empty<string>();

                var resultItems = new List<CatalogItem>();
                foreach (var idValue in idValues)
                {
                    if (int.TryParse(idValue, out var id) &&
                        itemsById.TryGetValue(id, out var item))
                    {
                        resultItems.Add(item);
                    }
                }

                var json = JsonSerializer.Serialize(resultItems);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class TestOrderingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            var response = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(string.Empty)
            };

            return Task.FromResult(response);
        }
    }

    private sealed class TestBasketClient : eShop.Basket.API.Grpc.Basket.BasketClient
    {
        public List<(int ProductId, int Quantity)> Items { get; } = new();

        public TestBasketClient()
        {
        }

        public override AsyncUnaryCall<CustomerBasketResponse> GetBasketAsync(
            GetBasketRequest request,
            Metadata? headers = null,
            DateTime? deadline = null,
            CancellationToken cancellationToken = default)
        {
            var response = new CustomerBasketResponse();
            foreach (var (productId, quantity) in Items)
            {
                response.Items.Add(new eShop.Basket.API.Grpc.BasketItem
                {
                    ProductId = productId,
                    Quantity = quantity
                });
            }

            return CreateAsyncUnaryCall(response);
        }

        public override AsyncUnaryCall<CustomerBasketResponse> UpdateBasketAsync(
            UpdateBasketRequest request,
            Metadata? headers = null,
            DateTime? deadline = null,
            CancellationToken cancellationToken = default)
        {
            Items.Clear();
            foreach (var item in request.Items)
            {
                Items.Add((item.ProductId, item.Quantity));
            }

            var response = new CustomerBasketResponse();
            response.Items.AddRange(request.Items);

            return CreateAsyncUnaryCall(response);
        }

        public override AsyncUnaryCall<DeleteBasketResponse> DeleteBasketAsync(
            DeleteBasketRequest request,
            Metadata? headers = null,
            DateTime? deadline = null,
            CancellationToken cancellationToken = default)
        {
            Items.Clear();
            var response = new DeleteBasketResponse();

            var responseTask = Task.FromResult(response);
            return new AsyncUnaryCall<DeleteBasketResponse>(
                responseTask,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }

        private static AsyncUnaryCall<TResponse> CreateAsyncUnaryCall<TResponse>(TResponse response)
            where TResponse : class
        {
            var responseTask = Task.FromResult(response);

            return new AsyncUnaryCall<TResponse>(
                responseTask,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }
    }

    public TestContext TestContext { get; set; }
}

