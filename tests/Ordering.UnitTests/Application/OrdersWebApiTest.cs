namespace eShop.Ordering.UnitTests.Application;

using Microsoft.AspNetCore.Http.HttpResults;
using eShop.Ordering.API.Application.Queries;
using Order = eShop.Ordering.API.Application.Queries.Order;
using NSubstitute.ExceptionExtensions;

[TestClass]
public class OrdersWebApiTest
{
    private readonly IMediator _mediatorMock;
    private readonly IOrderQueries _orderQueriesMock;
    private readonly IIdentityService _identityServiceMock;
    private readonly ILogger<OrderServices> _loggerMock;

    public OrdersWebApiTest()
    {
        _mediatorMock = Substitute.For<IMediator>();
        _orderQueriesMock = Substitute.For<IOrderQueries>();
        _identityServiceMock = Substitute.For<IIdentityService>();
        _loggerMock = Substitute.For<ILogger<OrderServices>>();
    }

    [TestMethod]
    public async Task Cancel_order_with_requestId_success()
    {
        // Arrange
        _mediatorMock.Send(Arg.Any<IdentifiedCommand<CancelOrderCommand, bool>>(), default)
            .Returns(Task.FromResult(true));

        // Act
        var orderServices = new OrderServices(_mediatorMock, _orderQueriesMock, _identityServiceMock, _loggerMock);
        var result = await OrdersApi.CancelOrderAsync(Guid.NewGuid(), new CancelOrderCommand(1), orderServices);

        // Assert
        Assert.IsInstanceOfType<Ok>(result.Result);
    }

    [TestMethod]
    public async Task Cancel_order_bad_request()
    {
        // Arrange
        _mediatorMock.Send(Arg.Any<IdentifiedCommand<CancelOrderCommand, bool>>(), default)
            .Returns(Task.FromResult(true));

        // Act
        var orderServices = new OrderServices(_mediatorMock, _orderQueriesMock, _identityServiceMock, _loggerMock);
        var result = await OrdersApi.CancelOrderAsync(Guid.Empty, new CancelOrderCommand(1), orderServices);

        // Assert
        Assert.IsInstanceOfType<BadRequest<string>>(result.Result);
    }

    [TestMethod]
    public async Task Ship_order_with_requestId_success()
    {
        // Arrange
        _mediatorMock.Send(Arg.Any<IdentifiedCommand<ShipOrderCommand, bool>>(), default)
            .Returns(Task.FromResult(true));

        // Act
        var orderServices = new OrderServices(_mediatorMock, _orderQueriesMock, _identityServiceMock, _loggerMock);
        var result = await OrdersApi.ShipOrderAsync(Guid.NewGuid(), new ShipOrderCommand(1), orderServices);

        // Assert
        Assert.IsInstanceOfType<Ok>(result.Result);

    }

    [TestMethod]
    public async Task Ship_order_bad_request()
    {
        // Arrange
        _mediatorMock.Send(Arg.Any<IdentifiedCommand<CreateOrderCommand, bool>>(), default)
            .Returns(Task.FromResult(true));

        // Act
        var orderServices = new OrderServices(_mediatorMock, _orderQueriesMock, _identityServiceMock, _loggerMock);
        var result = await OrdersApi.ShipOrderAsync(Guid.Empty, new ShipOrderCommand(1), orderServices);

        // Assert
        Assert.IsInstanceOfType<BadRequest<string>>(result.Result);
    }

    [TestMethod]
    public async Task Get_orders_success()
    {
        // Arrange
        var fakeDynamicResult = Enumerable.Empty<OrderSummary>();

        _identityServiceMock.GetUserIdentity()
            .Returns(Guid.NewGuid().ToString());

        _orderQueriesMock.GetOrdersFromUserAsync(Guid.NewGuid().ToString())
            .Returns(Task.FromResult(fakeDynamicResult));

        // Act
        var orderServices = new OrderServices(_mediatorMock, _orderQueriesMock, _identityServiceMock, _loggerMock);
        var result = await OrdersApi.GetOrdersByUserAsync(orderServices);

        // Assert
        Assert.IsInstanceOfType<Ok<IEnumerable<OrderSummary>>>(result);
    }

    [TestMethod]
    public async Task Get_order_success()
    {
        // Arrange
        var fakeOrderId = 123;
        var fakeDynamicResult = new Order();
        _orderQueriesMock.GetOrderAsync(Arg.Any<int>())
            .Returns(Task.FromResult(fakeDynamicResult));

        // Act
        var orderServices = new OrderServices(_mediatorMock, _orderQueriesMock, _identityServiceMock, _loggerMock);
        var result = await OrdersApi.GetOrderAsync(fakeOrderId, orderServices);

        // Assert
        Assert.IsInstanceOfType<Ok<Order>>(result.Result);
        Assert.AreSame(fakeDynamicResult, ((Ok<Order>)result.Result).Value);
    }

    [TestMethod]
    public async Task Get_order_fails()
    {
        // Arrange
        var fakeOrderId = 123;
#pragma warning disable NS5003
        _orderQueriesMock.GetOrderAsync(Arg.Any<int>())
            .Throws(new KeyNotFoundException());
#pragma warning restore NS5003

        // Act
        var orderServices = new OrderServices(_mediatorMock, _orderQueriesMock, _identityServiceMock, _loggerMock);
        var result = await OrdersApi.GetOrderAsync(fakeOrderId, orderServices);

        // Assert
        Assert.IsInstanceOfType<NotFound>(result.Result);
    }

    [TestMethod]
    public async Task Get_cardTypes_success()
    {
        // Arrange
        var fakeDynamicResult = Enumerable.Empty<CardType>();
        _orderQueriesMock.GetCardTypesAsync()
            .Returns(Task.FromResult(fakeDynamicResult));

        // Act
        var result = await OrdersApi.GetCardTypesAsync(_orderQueriesMock);

        // Assert
        Assert.IsInstanceOfType<Ok<IEnumerable<CardType>>>(result);
        Assert.AreSame(fakeDynamicResult, result.Value);
    }

    [TestMethod]
    public async Task Create_order_with_card_payment_and_invalid_card_number_returns_bad_request()
    {
        // Arrange
        _mediatorMock
            .Send(Arg.Any<IdentifiedCommand<CreateOrderCommand, bool>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var orderServices = new OrderServices(_mediatorMock, _orderQueriesMock, _identityServiceMock, _loggerMock);

        var items = new List<BasketItem>
        {
            new()
            {
                Id = "1",
                ProductId = 1,
                ProductName = "Test product",
                UnitPrice = 10m,
                OldUnitPrice = 10m,
                Quantity = 1,
                PictureUrl = "test"
            }
        };

        // Card number is too short and should be rejected for card payments.
        var request = new CreateOrderRequest(
            UserId: "user-1",
            UserName: "Test User",
            City: "City",
            Street: "Street",
            State: "State",
            Country: "Country",
            ZipCode: "12345",
            CardNumber: "123",
            CardHolderName: "Test User",
            CardExpiration: DateTime.UtcNow.AddYears(1),
            CardSecurityNumber: "123",
            CardTypeId: 1,
            Buyer: "buyer-1",
            Items: items,
            PaymentMethod: "Card");

        // Act
        var result = await OrdersApi.CreateOrderAsync(Guid.NewGuid(), request, orderServices);

        // Assert
        Assert.IsInstanceOfType<BadRequest<string>>(result.Result);
    }

    [TestMethod]
    public async Task Create_order_with_paypal_payment_allows_missing_card_details_and_maps_paypal_metadata()
    {
        // Arrange
        IdentifiedCommand<CreateOrderCommand, bool> sentCommand = null;

        _mediatorMock
            .Send(Arg.Do<IdentifiedCommand<CreateOrderCommand, bool>>(cmd => sentCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var orderServices = new OrderServices(_mediatorMock, _orderQueriesMock, _identityServiceMock, _loggerMock);

        var items = new List<BasketItem>
        {
            new()
            {
                Id = "1",
                ProductId = 1,
                ProductName = "Test product",
                UnitPrice = 10m,
                OldUnitPrice = 10m,
                Quantity = 1,
                PictureUrl = "test"
            }
        };

        var request = new CreateOrderRequest(
            UserId: "user-1",
            UserName: "Test User",
            City: "City",
            Street: "Street",
            State: "State",
            Country: "Country",
            ZipCode: "12345",
            CardNumber: null,
            CardHolderName: null,
            CardExpiration: DateTime.UtcNow.AddYears(1),
            CardSecurityNumber: null,
            CardTypeId: 0,
            Buyer: "buyer-1",
            Items: items,
            PaymentMethod: "PayPal",
            PayPalOrderId: "PAYPAL-123");

        // Act
        var result = await OrdersApi.CreateOrderAsync(Guid.NewGuid(), request, orderServices);

        // Assert
        Assert.IsInstanceOfType<Ok>(result.Result);
        Assert.IsNotNull(sentCommand);
        Assert.AreEqual("PayPal", sentCommand.Command.PaymentMethod);
        Assert.AreEqual("PAYPAL-123", sentCommand.Command.PayPalOrderId);
    }
}
