using eShop.PaymentProcessor.Services;

namespace eShop.PaymentProcessor.IntegrationEvents.EventHandling;

public class OrderStatusChangedToStockConfirmedIntegrationEventHandler(
    IEventBus eventBus,
    IOptionsMonitor<PaymentOptions> options,
    IPayPalCaptureService payPalCaptureService,
    ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler> logger) :
    IIntegrationEventHandler<OrderStatusChangedToStockConfirmedIntegrationEvent>
{
    public async Task Handle(OrderStatusChangedToStockConfirmedIntegrationEvent @event)
    {
        logger.LogInformation("Handling integration event: {IntegrationEventId} - ({@IntegrationEvent})", @event.Id, @event);

        IntegrationEvent orderPaymentIntegrationEvent;
        var isPayPal = string.Equals(@event.PaymentMethod, "PayPal", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(@event.PayPalOrderId);

        if (isPayPal)
        {
            // Capture the PayPal order after stock confirmation; publish success or failure.
            var captureSucceeded = await payPalCaptureService.CaptureOrderAsync(
                @event.PayPalOrderId,
                @event.OrderId);

            orderPaymentIntegrationEvent = captureSucceeded
                ? new OrderPaymentSucceededIntegrationEvent(@event.OrderId)
                : new OrderPaymentFailedIntegrationEvent(@event.OrderId);
        }
        else
        {
            // Simulated payment for card / other methods: use env option to simulate success or failure.
            if (options.CurrentValue.PaymentSucceeded)
            {
                orderPaymentIntegrationEvent = new OrderPaymentSucceededIntegrationEvent(@event.OrderId);
            }
            else
            {
                orderPaymentIntegrationEvent = new OrderPaymentFailedIntegrationEvent(@event.OrderId);
            }
        }

        logger.LogInformation("Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})", orderPaymentIntegrationEvent.Id, orderPaymentIntegrationEvent);

        await eventBus.PublishAsync(orderPaymentIntegrationEvent);
    }
}
