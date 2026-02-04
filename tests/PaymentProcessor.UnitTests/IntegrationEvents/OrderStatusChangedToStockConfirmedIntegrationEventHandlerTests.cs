namespace eShop.PaymentProcessor.UnitTests.IntegrationEvents;

[TestClass]
public sealed class OrderStatusChangedToStockConfirmedIntegrationEventHandlerTests
{
    private readonly IEventBus _eventBus;
    private readonly IOptionsMonitor<PaymentOptions> _paymentOptions;
    private readonly IPayPalClient _payPalClient;
    private readonly IOrderingApiClient _orderingApiClient;
    private readonly ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler> _logger;

    public OrderStatusChangedToStockConfirmedIntegrationEventHandlerTests()
    {
        _eventBus = Substitute.For<IEventBus>();
        _paymentOptions = Substitute.For<IOptionsMonitor<PaymentOptions>>();
        _payPalClient = Substitute.For<IPayPalClient>();
        _orderingApiClient = Substitute.For<IOrderingApiClient>();
        _logger = Substitute.For<ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler>>();
    }

    [TestMethod]
    public async Task Handle_DoesNotPublishEvent_WhenOrderAlreadyPaid()
    {
        // Arrange
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(
            OrderId: 42,
            ExternalPaymentId: "PAYPAL-ORDER-ID");

        _orderingApiClient
            .IsOrderAlreadyPaidAsync(@event.OrderId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var handler = CreateHandler();

        // Act
        await handler.Handle(@event);

        // Assert
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<IntegrationEvent>());
        await _payPalClient.DidNotReceive().CaptureOrderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Handle_PublishesSucceededEvent_WhenPayPalCaptureSucceeds()
    {
        // Arrange
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(
            OrderId: 10,
            ExternalPaymentId: "PAYPAL-ORDER-ID");

        _orderingApiClient
            .IsOrderAlreadyPaidAsync(@event.OrderId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _payPalClient
            .CaptureOrderAsync(@event.ExternalPaymentId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PayPalCaptureResult(
                Success: true,
                PayPalOrderId: @event.ExternalPaymentId,
                CaptureId: "CAPTURE-ID",
                CapturedAmount: 10m,
                CurrencyCode: "USD",
                Status: "COMPLETED")));

        var handler = CreateHandler();

        // Act
        await handler.Handle(@event);

        // Assert
        await _eventBus
            .Received(1)
            .PublishAsync(Arg.Is<OrderPaymentSucceededIntegrationEvent>(e => e.OrderId == @event.OrderId));
    }

    [TestMethod]
    public async Task Handle_PublishesFailedEvent_WhenPayPalCaptureFails()
    {
        // Arrange
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(
            OrderId: 20,
            ExternalPaymentId: "PAYPAL-ORDER-ID");

        _orderingApiClient
            .IsOrderAlreadyPaidAsync(@event.OrderId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _payPalClient
            .CaptureOrderAsync(@event.ExternalPaymentId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PayPalCaptureResult(
                Success: false,
                PayPalOrderId: @event.ExternalPaymentId,
                CaptureId: "CAPTURE-ID",
                CapturedAmount: 10m,
                CurrencyCode: "USD",
                Status: "FAILED")));

        var handler = CreateHandler();

        // Act
        await handler.Handle(@event);

        // Assert
        await _eventBus
            .Received(1)
            .PublishAsync(Arg.Is<OrderPaymentFailedIntegrationEvent>(e => e.OrderId == @event.OrderId));
    }

    [TestMethod]
    public async Task Handle_PublishesSucceededEvent_ForSimulatedPayment_WhenPaymentSucceededOptionTrue()
    {
        // Arrange
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(
            OrderId: 30,
            ExternalPaymentId: string.Empty);

        _orderingApiClient
            .IsOrderAlreadyPaidAsync(@event.OrderId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _paymentOptions.CurrentValue.Returns(new PaymentOptions
        {
            PaymentSucceeded = true
        });

        var handler = CreateHandler();

        // Act
        await handler.Handle(@event);

        // Assert
        await _eventBus
            .Received(1)
            .PublishAsync(Arg.Is<OrderPaymentSucceededIntegrationEvent>(e => e.OrderId == @event.OrderId));

        await _payPalClient.DidNotReceive().CaptureOrderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Handle_PublishesFailedEvent_ForSimulatedPayment_WhenPaymentSucceededOptionFalse()
    {
        // Arrange
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(
            OrderId: 40,
            ExternalPaymentId: null!);

        _orderingApiClient
            .IsOrderAlreadyPaidAsync(@event.OrderId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _paymentOptions.CurrentValue.Returns(new PaymentOptions
        {
            PaymentSucceeded = false
        });

        var handler = CreateHandler();

        // Act
        await handler.Handle(@event);

        // Assert
        await _eventBus
            .Received(1)
            .PublishAsync(Arg.Is<OrderPaymentFailedIntegrationEvent>(e => e.OrderId == @event.OrderId));

        await _payPalClient.DidNotReceive().CaptureOrderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private OrderStatusChangedToStockConfirmedIntegrationEventHandler CreateHandler()
    {
        return new OrderStatusChangedToStockConfirmedIntegrationEventHandler(
            _eventBus,
            _paymentOptions,
            _payPalClient,
            _orderingApiClient,
            _logger);
    }
}

