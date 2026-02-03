namespace eShop.Ordering.UnitTests.Application;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using eShop.Ordering.API.Application.Queries;
using eShop.Ordering.API.Infrastructure.PayPal;
using NSubstitute.ExceptionExtensions;

[TestClass]
public sealed class PayPalPaymentsApiTests
{
    private readonly IPayPalClient _payPalClient;
    private readonly ILoggerFactory _loggerFactory;

    public PayPalPaymentsApiTests()
    {
        _payPalClient = Substitute.For<IPayPalClient>();
        _loggerFactory = LoggerFactory.Create(_ => { });
    }

    [TestMethod]
    public async Task CreatePayPalOrderAsync_returns_bad_request_when_amount_not_greater_than_zero()
    {
        // Arrange
        var request = new CreatePayPalOrderRequest(0m, "USD", "basket-1");

        // Act
        var result = await PayPalPaymentsApi.CreatePayPalOrderAsync(
            request,
            _payPalClient,
            _loggerFactory,
            CancellationToken.None);

        // Assert
        Assert.IsInstanceOfType<BadRequest<string>>(result.Result);
        var badRequest = (BadRequest<string>)result.Result;
        Assert.AreEqual("Amount must be greater than zero.", badRequest.Value);
    }

    [TestMethod]
    public async Task CreatePayPalOrderAsync_returns_bad_request_when_currency_is_missing()
    {
        // Arrange
        var request = new CreatePayPalOrderRequest(10m, " ", "basket-1");

        // Act
        var result = await PayPalPaymentsApi.CreatePayPalOrderAsync(
            request,
            _payPalClient,
            _loggerFactory,
            CancellationToken.None);

        // Assert
        Assert.IsInstanceOfType<BadRequest<string>>(result.Result);
        var badRequest = (BadRequest<string>)result.Result;
        Assert.AreEqual("Currency is required.", badRequest.Value);
    }

    [TestMethod]
    public async Task CreatePayPalOrderAsync_returns_ok_with_order_id_when_successful()
    {
        // Arrange
        var request = new CreatePayPalOrderRequest(10m, "USD", "basket-1");
        _payPalClient
            .CreateOrderAsync(request.Amount, request.Currency, request.BasketId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("paypal-order-id"));

        // Act
        var result = await PayPalPaymentsApi.CreatePayPalOrderAsync(
            request,
            _payPalClient,
            _loggerFactory,
            CancellationToken.None);

        // Assert
        Assert.IsInstanceOfType<Ok<CreatePayPalOrderResponse>>(result.Result);
        var ok = (Ok<CreatePayPalOrderResponse>)result.Result;
        Assert.AreEqual("paypal-order-id", ok.Value.PayPalOrderId);
    }

    [TestMethod]
    public async Task CreatePayPalOrderAsync_returns_problem_499_when_cancelled()
    {
        // Arrange
        var request = new CreatePayPalOrderRequest(10m, "USD", "basket-1");
        _payPalClient
            .CreateOrderAsync(request.Amount, request.Currency, request.BasketId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await PayPalPaymentsApi.CreatePayPalOrderAsync(
            request,
            _payPalClient,
            _loggerFactory,
            new CancellationToken(canceled: true));

        // Assert
        Assert.IsInstanceOfType<ProblemHttpResult>(result.Result);
        var problem = (ProblemHttpResult)result.Result;
        Assert.AreEqual(StatusCodes.Status499ClientClosedRequest, problem.StatusCode);
    }

    [TestMethod]
    public async Task CreatePayPalOrderAsync_returns_problem_500_on_unexpected_error()
    {
        // Arrange
        var request = new CreatePayPalOrderRequest(10m, "USD", "basket-1");
        _payPalClient
            .CreateOrderAsync(request.Amount, request.Currency, request.BasketId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException());

        // Act
        var result = await PayPalPaymentsApi.CreatePayPalOrderAsync(
            request,
            _payPalClient,
            _loggerFactory,
            CancellationToken.None);

        // Assert
        Assert.IsInstanceOfType<ProblemHttpResult>(result.Result);
        var problem = (ProblemHttpResult)result.Result;
        Assert.AreEqual(StatusCodes.Status500InternalServerError, problem.StatusCode);
    }
}

[TestClass]
public sealed class CheckoutWithPayPalApiTests
{
    private readonly IMediator _mediatorMock;
    private readonly IOrderQueries _orderQueriesMock;
    private readonly IIdentityService _identityServiceMock;
    private readonly ILogger<OrderServices> _loggerMock;

    public CheckoutWithPayPalApiTests()
    {
        _mediatorMock = Substitute.For<IMediator>();
        _orderQueriesMock = Substitute.For<IOrderQueries>();
        _identityServiceMock = Substitute.For<IIdentityService>();
        _loggerMock = Substitute.For<ILogger<OrderServices>>();
    }

    [TestMethod]
    public async Task CheckoutWithPayPal_returns_bad_request_when_request_id_is_empty()
    {
        // Arrange
        var services = new OrderServices(_mediatorMock, _orderQueriesMock, _identityServiceMock, _loggerMock);
        var request = new CheckoutWithPayPalRequest(
            UserId: "user-1",
            UserName: "Test User",
            City: "City",
            Street: "Street",
            State: "State",
            Country: "Country",
            ZipCode: "12345",
            PayPalOrderId: "paypal-order-id",
            Items: new List<BasketItem>());

        // Act
        var result = await OrdersApi.CheckoutWithPayPalAsync(Guid.Empty, request, services);

        // Assert
        Assert.IsInstanceOfType<BadRequest<string>>(result.Result);
    }

    [TestMethod]
    public async Task CheckoutWithPayPal_returns_bad_request_when_paypal_order_id_is_missing()
    {
        // Arrange
        var services = new OrderServices(_mediatorMock, _orderQueriesMock, _identityServiceMock, _loggerMock);
        var request = new CheckoutWithPayPalRequest(
            UserId: "user-1",
            UserName: "Test User",
            City: "City",
            Street: "Street",
            State: "State",
            Country: "Country",
            ZipCode: "12345",
            PayPalOrderId: " ",
            Items: new List<BasketItem>());

        // Act
        var result = await OrdersApi.CheckoutWithPayPalAsync(Guid.NewGuid(), request, services);

        // Assert
        Assert.IsInstanceOfType<BadRequest<string>>(result.Result);
    }

    [TestMethod]
    public async Task CheckoutWithPayPal_links_external_payment_id_and_items_and_returns_ok()
    {
        // Arrange
        IdentifiedCommand<CreateOrderCommand, bool> capturedCommand = null!;

        _mediatorMock
            .Send(Arg.Any<IdentifiedCommand<CreateOrderCommand, bool>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedCommand = callInfo.Arg<IdentifiedCommand<CreateOrderCommand, bool>>();
                return Task.FromResult(true);
            });

        var services = new OrderServices(_mediatorMock, _orderQueriesMock, _identityServiceMock, _loggerMock);

        var items = new List<BasketItem>
        {
            new()
            {
                Id = "1",
                ProductId = 1,
                ProductName = "Item",
                UnitPrice = 10,
                OldUnitPrice = 9,
                Quantity = 1,
                PictureUrl = "pic"
            }
        };

        var paypalOrderId = "PAYPAL-123";
        var request = new CheckoutWithPayPalRequest(
            UserId: "user-1",
            UserName: "Test User",
            City: "City",
            Street: "Street",
            State: "State",
            Country: "Country",
            ZipCode: "12345",
            PayPalOrderId: paypalOrderId,
            Items: items);

        // Act
        var result = await OrdersApi.CheckoutWithPayPalAsync(Guid.NewGuid(), request, services);

        // Assert
        Assert.IsInstanceOfType<Ok>(result.Result);
        Assert.IsNotNull(capturedCommand);
        Assert.AreEqual(paypalOrderId, capturedCommand.Command.ExternalPaymentId);
        Assert.AreEqual(request.UserId, capturedCommand.Command.UserId);
        Assert.AreEqual(request.Items.Count, capturedCommand.Command.OrderItems.Count());
    }
}

