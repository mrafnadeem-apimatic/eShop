using System.Threading;
using System.Threading.Tasks;
using eShop.EventBus.Abstractions;
using eShop.EventBus.Events;
using eShop.PaymentProcessor;
using eShop.PaymentProcessor.IntegrationEvents.EventHandling;
using eShop.PaymentProcessor.IntegrationEvents.Events;
using eShop.PaymentProcessor.PayPal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class OrderStatusChangedToStockConfirmedIntegrationEventHandlerTests
{
    private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
    private readonly IOptionsMonitor<PaymentOptions> _paymentOptions = Substitute.For<IOptionsMonitor<PaymentOptions>>();
    private readonly ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler> _logger =
        Substitute.For<ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler>>();
    private readonly IPayPalOrderCaptureClient _payPalOrderCaptureClient = Substitute.For<IPayPalOrderCaptureClient>();

    private OrderStatusChangedToStockConfirmedIntegrationEventHandler CreateHandler()
        => new(_eventBus, _paymentOptions, _logger, _payPalOrderCaptureClient);

    [TestMethod]
    public async Task Handle_PayPalOrder_WithSuccessfulCapture_PublishesSucceededIntegrationEvent()
    {
        // Arrange
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(
            OrderId: 123,
            PaymentMethod: "PayPal",
            PayPalOrderId: "PAYPAL-ORDER-123");

        var expectedIdempotencyKey = "capture-123-PAYPAL-ORDER-123";

        _payPalOrderCaptureClient
            .CaptureOrderAsync(@event.PayPalOrderId, expectedIdempotencyKey, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();

        // Act
        await handler.Handle(@event);

        // Assert capture called with correct arguments
        await _payPalOrderCaptureClient
            .Received(1)
            .CaptureOrderAsync(@event.PayPalOrderId, expectedIdempotencyKey, Arg.Any<CancellationToken>());

        // Assert succeeded event published
        await _eventBus
            .Received(1)
            .PublishAsync(Arg.Is<IntegrationEvent>(e =>
                e is OrderPaymentSucceededIntegrationEvent &&
                ((OrderPaymentSucceededIntegrationEvent)e).OrderId == @event.OrderId));
    }

    [TestMethod]
    public async Task Handle_PayPalOrder_WithFailedCapture_PublishesFailedIntegrationEvent()
    {
        // Arrange
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(
            OrderId: 123,
            PaymentMethod: "PayPal",
            PayPalOrderId: "PAYPAL-ORDER-123");

        var expectedIdempotencyKey = "capture-123-PAYPAL-ORDER-123";

        _payPalOrderCaptureClient
            .CaptureOrderAsync(@event.PayPalOrderId, expectedIdempotencyKey, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = CreateHandler();

        // Act
        await handler.Handle(@event);

        // Assert capture attempted
        await _payPalOrderCaptureClient
            .Received(1)
            .CaptureOrderAsync(@event.PayPalOrderId, expectedIdempotencyKey, Arg.Any<CancellationToken>());

        // Assert failed event published
        await _eventBus
            .Received(1)
            .PublishAsync(Arg.Is<IntegrationEvent>(e =>
                e is OrderPaymentFailedIntegrationEvent &&
                ((OrderPaymentFailedIntegrationEvent)e).OrderId == @event.OrderId));
    }

    [TestMethod]
    public async Task Handle_PayPalOrder_WithMissingPayPalOrderId_PublishesFailedIntegrationEventWithoutCallingCapture()
    {
        // Arrange
        var @event = new OrderStatusChangedToStockConfirmedIntegrationEvent(
            OrderId: 123,
            PaymentMethod: "PayPal",
            PayPalOrderId: null);

        var handler = CreateHandler();

        // Act
        await handler.Handle(@event);

        // Assert no capture attempt was made
        await _payPalOrderCaptureClient
            .DidNotReceive()
            .CaptureOrderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Assert failed event published
        await _eventBus
            .Received(1)
            .PublishAsync(Arg.Is<IntegrationEvent>(e =>
                e is OrderPaymentFailedIntegrationEvent &&
                ((OrderPaymentFailedIntegrationEvent)e).OrderId == @event.OrderId));
    }
}

