using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using eShop.WebApp.Services;
using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard.Http.Response;
using PaypalServerSdk.Standard.Models;

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

        var ordersClient = Substitute.For<IPayPalOrdersClient>();
        ApiResponse<Order> responseFromClient = default!;

        ordersClient
            .CreateOrderAsync(Arg.Any<CreateOrderInput>())
            .Returns(Task.FromResult(responseFromClient));

        ordersClient
            .GetOrderId(Arg.Any<ApiResponse<Order>>())
            .Returns("PAYPAL-ORDER-ID");

        var sessionStore = Substitute.For<IPayPalCheckoutSessionStore>();

        var service = CreateService(
            ordersClient: ordersClient,
            basketState: basketState,
            sessionStore: sessionStore);

        var basketId = "basket-123";
        var userId = "user-456";

        // Act
        var response = await service.CreateOrderForBasketAsync(basketId, userId);

        // Assert: response is the one returned by the underlying client (reference equality).
        Assert.AreSame(responseFromClient, response);

        // Assert: the CreateOrderInput is built correctly from the basket and identifiers.
        await ordersClient.Received(1).CreateOrderAsync(
            Arg.Is<CreateOrderInput>(input =>
                input.PaypalRequestId == "create-user-456-basket-123" &&
                input.Prefer == "return=representation" &&
                input.Body != null));

        var capturedInput = ordersClient.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IPayPalOrdersClient.CreateOrderAsync))
            .GetArguments()
            .OfType<CreateOrderInput>()
            .Single();

        var orderRequest = capturedInput.Body;
        Assert.IsNotNull(orderRequest);
        Assert.AreEqual(CheckoutPaymentIntent.Capture, orderRequest.Intent);
        Assert.AreEqual(1, orderRequest.PurchaseUnits.Count);

        var purchaseUnit = orderRequest.PurchaseUnits[0];
        Assert.AreEqual(basketId, purchaseUnit.ReferenceId);
        Assert.AreEqual(basketId, purchaseUnit.CustomId);

        // Total should be (10.00 * 2) + (5.50 * 1) = 25.50
        Assert.IsNotNull(purchaseUnit.Amount);
        Assert.AreEqual("USD", purchaseUnit.Amount.CurrencyCode);
        Assert.AreEqual(25.50m.ToString("F2", CultureInfo.InvariantCulture), purchaseUnit.Amount.MValue);

        Assert.AreEqual(basketItems.Length, purchaseUnit.Items.Count);

        for (var i = 0; i < basketItems.Length; i++)
        {
            var expected = basketItems[i];
            var actual = purchaseUnit.Items[i];

            Assert.AreEqual(expected.ProductName, actual.Name);
            Assert.AreEqual(expected.Quantity.ToString(CultureInfo.InvariantCulture), actual.Quantity);

            Assert.IsNotNull(actual.UnitAmount);
            Assert.AreEqual("USD", actual.UnitAmount.CurrencyCode);
            Assert.AreEqual(
                expected.UnitPrice.ToString("F2", CultureInfo.InvariantCulture),
                actual.UnitAmount.MValue);
        }

        // Assert: a checkout session was stored with the expected identifiers.
        await sessionStore.Received(1).StoreSessionAsync(
            Arg.Is<PayPalCheckoutSession>(s =>
                s.PaypalOrderId == "PAYPAL-ORDER-ID" &&
                s.BasketId == basketId &&
                s.UserId == userId));
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

