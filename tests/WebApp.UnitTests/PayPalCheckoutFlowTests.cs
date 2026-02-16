using System.Collections.Generic;
using System.Security.Claims;
using eShop.WebApp.Services;
using eShop.WebAppComponents.Catalog;
using eShop.WebAppComponents.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace eShop.WebApp.UnitTests;

[TestClass]
public class PayPalCheckoutFlowTests
{
    private const string TestBuyerId = "test-buyer-id";
    private const string TestUserName = "Test User";

    [TestMethod]
    public async Task CheckoutAsync_WithPayPalPaymentMethod_SendsCreateOrderRequestWithPayPalMetadata()
    {
        var orderingService = Substitute.For<IOrderingService>();
        CreateOrderRequest capturedRequest = null!;
        orderingService.CreateOrder(Arg.Any<CreateOrderRequest>(), Arg.Any<Guid>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                capturedRequest = call.ArgAt<CreateOrderRequest>(0);
            });

        var basketState = CreateBasketState(orderingService);

        var checkoutInfo = new BasketCheckoutInfo
        {
            Street = "123 Main St",
            City = "Seattle",
            State = "WA",
            Country = "USA",
            ZipCode = "98101",
            PaymentMethod = "PayPal",
            PayPalOrderId = "PAYPAL-ORDER-123",
            RequestId = Guid.NewGuid()
        };

        await basketState.CheckoutAsync(checkoutInfo);

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("PayPal", capturedRequest.PaymentMethod);
        Assert.AreEqual("PAYPAL-ORDER-123", capturedRequest.PayPalOrderId);
        await orderingService.Received(1).CreateOrder(Arg.Any<CreateOrderRequest>(), Arg.Any<Guid>());
    }

    [TestMethod]
    public async Task CheckoutAsync_WithCardPaymentMethod_SendsCreateOrderRequestWithNullPayPalOrderId()
    {
        var orderingService = Substitute.For<IOrderingService>();
        CreateOrderRequest capturedRequest = null!;
        orderingService.CreateOrder(Arg.Any<CreateOrderRequest>(), Arg.Any<Guid>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                capturedRequest = call.ArgAt<CreateOrderRequest>(0);
            });

        var basketState = CreateBasketState(orderingService);

        var checkoutInfo = new BasketCheckoutInfo
        {
            Street = "123 Main St",
            City = "Seattle",
            State = "WA",
            Country = "USA",
            ZipCode = "98101",
            PaymentMethod = "Card",
            PayPalOrderId = null,
            RequestId = Guid.NewGuid()
        };

        await basketState.CheckoutAsync(checkoutInfo);

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("Card", capturedRequest.PaymentMethod);
        Assert.IsNull(capturedRequest.PayPalOrderId);
        await orderingService.Received(1).CreateOrder(Arg.Any<CreateOrderRequest>(), Arg.Any<Guid>());
    }

    [TestMethod]
    public async Task CheckoutAsync_WithDefaultPaymentMethod_TreatsAsCardAndNullPayPalOrderId()
    {
        var orderingService = Substitute.For<IOrderingService>();
        CreateOrderRequest capturedRequest = null!;
        orderingService.CreateOrder(Arg.Any<CreateOrderRequest>(), Arg.Any<Guid>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                capturedRequest = call.ArgAt<CreateOrderRequest>(0);
            });

        var basketState = CreateBasketState(orderingService);

        var checkoutInfo = new BasketCheckoutInfo
        {
            Street = "123 Main St",
            City = "Seattle",
            State = "WA",
            Country = "USA",
            ZipCode = "98101",
            PaymentMethod = "", // default / empty
            PayPalOrderId = "IGNORED-FOR-NON-PAYPAL",
            RequestId = Guid.NewGuid()
        };

        await basketState.CheckoutAsync(checkoutInfo);

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("Card", capturedRequest.PaymentMethod);
        Assert.IsNull(capturedRequest.PayPalOrderId);
    }

    private static BasketState CreateBasketState(IOrderingService orderingService)
    {
        var basketService = Substitute.For<IBasketService>();
        basketService.GetBasketAsync()
            .Returns(new List<BasketQuantity> { new(ProductId: 1, Quantity: 1) });
        basketService.DeleteBasketAsync().Returns(Task.CompletedTask);
        basketService.UpdateBasketAsync(Arg.Any<IReadOnlyCollection<BasketQuantity>>()).Returns(Task.CompletedTask);

        var catalogService = Substitute.For<ICatalogService>();
        var catalogItem = new CatalogItem(
            Id: 1,
            Name: "Test Product",
            Description: "Description",
            Price: 9.99m,
            PictureUrl: "",
            CatalogBrandId: 1,
            CatalogBrand: new CatalogBrand(1, "Brand"),
            CatalogTypeId: 1,
            CatalogType: new CatalogItemType(1, "Type"));
        catalogService.GetCatalogItems(Arg.Any<IEnumerable<int>>())
            .Returns(new List<CatalogItem> { catalogItem });

        var authProvider = new FakeAuthenticationStateProvider(TestBuyerId, TestUserName);

        return new BasketState(basketService, catalogService, orderingService, authProvider);
    }

    private sealed class FakeAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly string _buyerId;
        private readonly string _userName;

        public FakeAuthenticationStateProvider(string buyerId, string userName)
        {
            _buyerId = buyerId;
            _userName = userName;
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity();
            identity.AddClaim(new Claim("sub", _buyerId));
            identity.AddClaim(new Claim("name", _userName));
            var user = new ClaimsPrincipal(identity);
            return Task.FromResult(new AuthenticationState(user));
        }
    }
}
