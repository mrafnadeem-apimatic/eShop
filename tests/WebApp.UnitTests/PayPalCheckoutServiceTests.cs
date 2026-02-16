using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        try
        {
            await service.CreateOrderForBasketAsync(string.Empty, "user-1");
            Assert.Fail("Expected ArgumentException to be thrown for missing basketId.");
        }
        catch (ArgumentException ex)
        {
            Assert.AreEqual("basketId", ex.ParamName);
        }
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
        try
        {
            await service.CreateOrderForBasketAsync("basket-1", " ");
            Assert.Fail("Expected ArgumentException to be thrown for missing userId.");
        }
        catch (ArgumentException ex)
        {
            Assert.AreEqual("userId", ex.ParamName);
        }
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
        try
        {
            await service.CreateOrderForBasketAsync("basket-1", "user-1");
            Assert.Fail("Expected InvalidOperationException to be thrown for empty basket.");
        }
        catch (InvalidOperationException)
        {
        }
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

        var expectedResult = new CreatePayPalOrderResult("PAYPAL-ORDER-ID", "https://example.com/approve");
        var ordersClient = Substitute.For<IPayPalOrdersClient>();
        ordersClient
            .CreateOrderAsync(Arg.Any<CreatePayPalOrderRequest>())
            .Returns(Task.FromResult<CreatePayPalOrderResult?>(expectedResult));

        var sessionStore = Substitute.For<IPayPalCheckoutSessionStore>();

        var service = CreateService(
            ordersClient: ordersClient,
            basketState: basketState,
            sessionStore: sessionStore);

        var basketId = "basket-123";
        var userId = "user-456";

        // Act
        var result = await service.CreateOrderForBasketAsync(basketId, userId);

        // Assert: result is the one returned by the client.
        Assert.AreSame(expectedResult, result);
        Assert.AreEqual("PAYPAL-ORDER-ID", result.PaypalOrderId);
        Assert.AreEqual("https://example.com/approve", result.ApprovalUrl);

        // Assert: the client was called with a request built from the basket.
        await ordersClient.Received(1).CreateOrderAsync(
            Arg.Is<CreatePayPalOrderRequest>(req =>
                req.BasketId == basketId &&
                req.UserId == userId &&
                req.Items.Count == 2 &&
                req.Items[0].Name == "Item One" && req.Items[0].Quantity == 2 && req.Items[0].UnitPrice == 10.00m &&
                req.Items[1].Name == "Item Two" && req.Items[1].Quantity == 1 && req.Items[1].UnitPrice == 5.50m));

        // Assert: a checkout session was stored with the expected identifiers.
        await sessionStore.Received(1).StoreSessionAsync(
            Arg.Is<PayPalCheckoutSession>(s =>
                s.PaypalOrderId == "PAYPAL-ORDER-ID" &&
                s.BasketId == basketId &&
                s.UserId == userId), Arg.Any<CancellationToken>());
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

