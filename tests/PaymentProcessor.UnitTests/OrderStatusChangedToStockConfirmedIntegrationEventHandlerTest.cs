namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class OrderStatusChangedToStockConfirmedIntegrationEventHandlerTest
{
    private readonly IEventBus _eventBus;
    private readonly IOptionsMonitor<PaymentOptions> _options;
    private readonly IPayPalCaptureService _payPalCaptureService;
    private readonly ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler> _logger;

    public OrderStatusChangedToStockConfirmedIntegrationEventHandlerTest()
    {
        _eventBus = Substitute.For<IEventBus>();
        _options = Substitute.For<IOptionsMonitor<PaymentOptions>>();
        _payPalCaptureService = Substitute.For<IPayPalCaptureService>();
        _logger = Substitute.For<ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler>>();
    }

    private OrderStatusChangedToStockConfirmedIntegrationEventHandler CreateHandler()
    {
        return new OrderStatusChangedToStockConfirmedIntegrationEventHandler(
            _eventBus,
            _options,
            _payPalCaptureService,
            _logger);
    }

    [TestMethod]
    public async Task Handle_WhenPayPal_AndCaptureSucceeds_PublishesOrderPaymentSucceeded()
    {
        var orderId = 42;
        var paypalOrderId = "PAYPAL-ORDER-123";
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(orderId, "PayPal", paypalOrderId);

        _payPalCaptureService.CaptureOrderAsync(paypalOrderId, orderId, default)
            .Returns(Task.FromResult(true));

        var handler = CreateHandler();
        await handler.Handle(@event);

        await _payPalCaptureService.Received(1).CaptureOrderAsync(paypalOrderId, orderId, default);
        await _eventBus.Received(1).PublishAsync(Arg.Is<OrderPaymentSucceededIntegrationEvent>(e => e.OrderId == orderId));
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OrderPaymentFailedIntegrationEvent>());
    }

    [TestMethod]
    public async Task Handle_WhenPayPal_AndCaptureFails_PublishesOrderPaymentFailed()
    {
        var orderId = 99;
        var paypalOrderId = "PAYPAL-ORDER-456";
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(orderId, "PayPal", paypalOrderId);

        _payPalCaptureService.CaptureOrderAsync(paypalOrderId, orderId, default)
            .Returns(Task.FromResult(false));

        var handler = CreateHandler();
        await handler.Handle(@event);

        await _payPalCaptureService.Received(1).CaptureOrderAsync(paypalOrderId, orderId, default);
        await _eventBus.Received(1).PublishAsync(Arg.Is<OrderPaymentFailedIntegrationEvent>(e => e.OrderId == orderId));
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OrderPaymentSucceededIntegrationEvent>());
    }

    [TestMethod]
    public async Task Handle_WhenPayPalMethodCaseInsensitive_AndCaptureSucceeds_PublishesSuccess()
    {
        var orderId = 1;
        var paypalOrderId = "PAYPAL-ORDER-789";
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(orderId, "paypal", paypalOrderId);

        _payPalCaptureService.CaptureOrderAsync(paypalOrderId, orderId, default)
            .Returns(Task.FromResult(true));

        var handler = CreateHandler();
        await handler.Handle(@event);

        await _payPalCaptureService.Received(1).CaptureOrderAsync(paypalOrderId, orderId, default);
        await _eventBus.Received(1).PublishAsync(Arg.Is<OrderPaymentSucceededIntegrationEvent>(e => e.OrderId == orderId));
    }

    [TestMethod]
    public async Task Handle_WhenCard_AndPaymentSucceededTrue_PublishesOrderPaymentSucceeded()
    {
        var orderId = 10;
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(orderId, "Card", PayPalOrderId: "");

        _options.CurrentValue.Returns(new PaymentOptions { PaymentSucceeded = true });

        var handler = CreateHandler();
        await handler.Handle(@event);

        await _payPalCaptureService.DidNotReceive().CaptureOrderAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(Arg.Is<OrderPaymentSucceededIntegrationEvent>(e => e.OrderId == orderId));
    }

    [TestMethod]
    public async Task Handle_WhenCard_AndPaymentSucceededFalse_PublishesOrderPaymentFailed()
    {
        var orderId = 20;
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(orderId, "Card", PayPalOrderId: "");

        _options.CurrentValue.Returns(new PaymentOptions { PaymentSucceeded = false });

        var handler = CreateHandler();
        await handler.Handle(@event);

        await _payPalCaptureService.DidNotReceive().CaptureOrderAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(Arg.Is<OrderPaymentFailedIntegrationEvent>(e => e.OrderId == orderId));
    }

    [TestMethod]
    public async Task Handle_WhenPayPalButEmptyPayPalOrderId_UsesCardFlow_AndDoesNotCallPayPalCapture()
    {
        var orderId = 30;
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(orderId, "PayPal", PayPalOrderId: "");

        _options.CurrentValue.Returns(new PaymentOptions { PaymentSucceeded = true });

        var handler = CreateHandler();
        await handler.Handle(@event);

        await _payPalCaptureService.DidNotReceive().CaptureOrderAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(Arg.Is<OrderPaymentSucceededIntegrationEvent>(e => e.OrderId == orderId));
    }

    [TestMethod]
    public async Task Handle_WhenPayPalButWhitespacePayPalOrderId_UsesCardFlow_AndDoesNotCallPayPalCapture()
    {
        var orderId = 31;
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(orderId, "PayPal", PayPalOrderId: "   ");

        _options.CurrentValue.Returns(new PaymentOptions { PaymentSucceeded = false });

        var handler = CreateHandler();
        await handler.Handle(@event);

        await _payPalCaptureService.DidNotReceive().CaptureOrderAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(Arg.Is<OrderPaymentFailedIntegrationEvent>(e => e.OrderId == orderId));
    }
}
