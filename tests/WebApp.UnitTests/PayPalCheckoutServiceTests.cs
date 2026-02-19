using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using eShop.WebApp;
using eShop.WebApp.Services;
using Microsoft.Extensions.Logging;

namespace eShop.WebApp.UnitTests;

[TestClass]
public class PayPalCheckoutServiceTests
{
    [TestMethod]
    public async Task CreateOrderForBasketAsync_ThrowsArgumentException_WhenBasketIdMissing()
    {
        // Arrange
        var service = CreateService(
            ordersClient: Substitute.For<IPayPalOrdersClient>(),
            basketState: Substitute.For<IBasketState>(),
            sessionStore: Substitute.For<IPayPalCheckoutSessionStore>());

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => service.CreateOrderForBasketAsync(string.Empty, "user-1", TestContext.CancellationToken));

        Assert.AreEqual("basketId", exception.ParamName);
    }

    [TestMethod]
    public async Task CreateOrderForBasketAsync_ThrowsArgumentException_WhenUserIdMissing()
    {
        // Arrange
        var service = CreateService(
            ordersClient: Substitute.For<IPayPalOrdersClient>(),
            basketState: Substitute.For<IBasketState>(),
            sessionStore: Substitute.For<IPayPalCheckoutSessionStore>());

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => service.CreateOrderForBasketAsync("basket-1", " ", TestContext.CancellationToken));

        Assert.AreEqual("userId", exception.ParamName);
    }

    [TestMethod]
    public async Task CreateOrderForBasketAsync_ThrowsInvalidOperationException_WhenBasketIsEmpty()
    {
        // Arrange
        var emptyBasketState = Substitute.For<IBasketState>();
        emptyBasketState
            .GetBasketItemsAsync()
            .Returns(Task.FromResult<IReadOnlyCollection<BasketItem>>([]));

        var service = CreateService(
            ordersClient: Substitute.For<IPayPalOrdersClient>(),
            basketState: emptyBasketState,
            sessionStore: Substitute.For<IPayPalCheckoutSessionStore>());

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.CreateOrderForBasketAsync("basket-1", "user-1", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CreateOrderForBasketAsync_CreatesOrderAndStoresSession_ForValidBasket()
    {
        // Arrange
        var basketItems = new[]
        {
            new BasketItem
            {
                Id = "item-1",
                ProductId = 1,
                ProductName = "Item One",
                UnitPrice = 10.00m,
                OldUnitPrice = 0m,
                Quantity = 2,
            },
            new BasketItem
            {
                Id = "item-2",
                ProductId = 2,
                ProductName = "Item Two",
                UnitPrice = 5.50m,
                OldUnitPrice = 0m,
                Quantity = 1,
            },
        };

        var basketState = Substitute.For<IBasketState>();
        basketState
            .GetBasketItemsAsync()
            .Returns(Task.FromResult<IReadOnlyCollection<BasketItem>>(basketItems));

        var ordersClient = Substitute.For<IPayPalOrdersClient>();
        var responseFromClient = new PayPalOrderResponse(
            PaypalOrderId: "PAYPAL-ORDER-ID",
            ApprovalUrl: "https://example.test/approval");

        ordersClient
            .CreateOrderAsync(Arg.Any<PayPalOrderRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(responseFromClient));

        var sessionStore = Substitute.For<IPayPalCheckoutSessionStore>();

        var service = CreateService(
            ordersClient: ordersClient,
            basketState: basketState,
            sessionStore: sessionStore);

        var basketId = "basket-123";
        var userId = "user-456";

        // Act
        var response = await service.CreateOrderForBasketAsync(basketId, userId, TestContext.CancellationToken);

        // Assert: response is the one returned by the underlying client.
        Assert.IsNotNull(response);
        Assert.AreEqual(responseFromClient, response);

        // Assert: the PayPalOrderRequest is built correctly from the basket and identifiers.
        await ordersClient.Received(1).CreateOrderAsync(
            Arg.Any<PayPalOrderRequest>(),
            Arg.Any<CancellationToken>());

        var capturedRequest = ordersClient.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IPayPalOrdersClient.CreateOrderAsync))
            .GetArguments()
            .OfType<PayPalOrderRequest>()
            .Single();

        Assert.AreEqual(basketId, capturedRequest.BasketId);
        Assert.AreEqual(userId, capturedRequest.UserId);
        Assert.AreEqual("USD", capturedRequest.CurrencyCode);

        Assert.IsFalse(
            string.IsNullOrWhiteSpace(capturedRequest.IdempotencyKey),
            "Idempotency key should be non-empty.");
        Assert.IsTrue(
            Guid.TryParse(capturedRequest.IdempotencyKey, out _),
            $"Expected GUID-format idempotency key but got: {capturedRequest.IdempotencyKey}");

        // Total should be (10.00 * 2) + (5.50 * 1) = 25.50
        Assert.AreEqual(25.50m, capturedRequest.Total);

        Assert.HasCount(basketItems.Length, capturedRequest.Items);

        var requestItems = capturedRequest.Items.ToArray();
        for (var i = 0; i < basketItems.Length; i++)
        {
            var expected = basketItems[i];
            var actual = requestItems[i];

            Assert.AreEqual(expected.ProductName, actual.Name);
            Assert.AreEqual(expected.Quantity, actual.Quantity);
            Assert.AreEqual(expected.UnitPrice, actual.UnitPrice);
            Assert.AreEqual("USD", actual.CurrencyCode);
        }

        // Assert: a checkout session was stored with the expected identifiers.
        await sessionStore.Received(1).StoreSessionAsync(
            Arg.Is<PayPalCheckoutSession>(s =>
                s.PaypalOrderId == "PAYPAL-ORDER-ID" &&
                s.BasketId == basketId &&
                s.UserId == userId), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task CreateOrderForBasketAsync_ProducesDifferentIdempotencyKeys_ForDifferentBasketContents()
    {
        // Arrange
        var firstBasketItems = new[]
        {
            new BasketItem
            {
                Id = "item-1",
                ProductId = 1,
                ProductName = "Item One",
                UnitPrice = 10.00m,
                OldUnitPrice = 0m,
                Quantity = 1,
            },
        };

        var secondBasketItems = new[]
        {
            new BasketItem
            {
                Id = "item-1",
                ProductId = 1,
                ProductName = "Item One",
                UnitPrice = 10.00m,
                OldUnitPrice = 0m,
                Quantity = 2,
            },
        };

        var basketState = Substitute.For<IBasketState>();
        basketState
            .GetBasketItemsAsync()
            .Returns(
                Task.FromResult<IReadOnlyCollection<BasketItem>>(firstBasketItems),
                Task.FromResult<IReadOnlyCollection<BasketItem>>(secondBasketItems));

        var ordersClient = Substitute.For<IPayPalOrdersClient>();
        var responseFromClient = new PayPalOrderResponse(
            PaypalOrderId: "PAYPAL-ORDER-ID",
            ApprovalUrl: "https://example.test/approval");

        ordersClient
            .CreateOrderAsync(Arg.Any<PayPalOrderRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(responseFromClient));

        var sessionStore = Substitute.For<IPayPalCheckoutSessionStore>();

        var service = CreateService(
            ordersClient: ordersClient,
            basketState: basketState,
            sessionStore: sessionStore);

        var basketId = "basket-123";
        var userId = "user-456";

        // Act
        await service.CreateOrderForBasketAsync(basketId, userId, TestContext.CancellationToken);
        await service.CreateOrderForBasketAsync(basketId, userId, TestContext.CancellationToken);

        // Assert
        var capturedRequests = ordersClient.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IPayPalOrdersClient.CreateOrderAsync))
            .Select(call => call.GetArguments().OfType<PayPalOrderRequest>().Single())
            .ToArray();

        Assert.AreEqual(2, capturedRequests.Length);

        var firstKey = capturedRequests[0].IdempotencyKey;
        var secondKey = capturedRequests[1].IdempotencyKey;

        Assert.AreNotEqual(
            firstKey,
            secondKey,
            "Expected different idempotency keys for different basket contents.");
    }

    private static PayPalCheckoutService CreateService(
        IPayPalOrdersClient ordersClient,
        IBasketState basketState,
        IPayPalCheckoutSessionStore sessionStore)
    {
        return new PayPalCheckoutService(
            ordersClient,
            new TestLogger<PayPalCheckoutService>(),
            basketState,
            sessionStore);
    }

    public TestContext TestContext { get; set; }
}

internal sealed class TestLogger<T> : ILogger<T>
{
    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }

    public IDisposable BeginScope<TState>(TState state)
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        // These tests currently do not assert on log output.
    }
}

