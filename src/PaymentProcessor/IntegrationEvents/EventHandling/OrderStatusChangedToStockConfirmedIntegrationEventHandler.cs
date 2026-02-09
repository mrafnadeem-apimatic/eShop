namespace eShop.PaymentProcessor.IntegrationEvents.EventHandling;

public class OrderStatusChangedToStockConfirmedIntegrationEventHandler(
    IEventBus eventBus,
    IOptionsMonitor<PaymentOptions> options,
    IPayPalClient payPalClient,
    IOrderingApiClient orderingApiClient,
    ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler> logger) :
    IIntegrationEventHandler<OrderStatusChangedToStockConfirmedIntegrationEvent>
{
    public async Task Handle(OrderStatusChangedToStockConfirmedIntegrationEvent @event)
    {
        logger.LogInformation("Handling integration event: {IntegrationEventId} - ({@IntegrationEvent})", @event.Id, @event);

        // If the order has already been marked as paid (for example, via a PayPal capture
        // that updated the order status directly in Ordering.API), we skip processing here
        // to avoid double-charging.
        var alreadyPaid = await orderingApiClient.IsOrderAlreadyPaidAsync(@event.OrderId);

        if (alreadyPaid)
        {
            logger.LogInformation(
                "Order {OrderId} is already paid according to Ordering.API. Skipping payment processing.",
                @event.OrderId);
            return;
        }

        IntegrationEvent orderPaymentIntegrationEvent;

        // For PayPal orders (identified by an external payment id), perform a real capture
        // against PayPal and publish the appropriate payment event based on the outcome.
        if (!string.IsNullOrWhiteSpace(@event.ExternalPaymentId))
        {
            var captureResult = await payPalClient.CaptureOrderAsync(@event.ExternalPaymentId);

            orderPaymentIntegrationEvent = captureResult.Success
                ? new OrderPaymentSucceededIntegrationEvent(@event.OrderId)
                : new OrderPaymentFailedIntegrationEvent(@event.OrderId);
        }
        else
        {
            // Business feature comment:
            // When OrderStatusChangedToStockConfirmed Integration Event is handled.
            // Here we're simulating that we'd be performing the payment against any payment gateway.
            // Instead of a real payment we just take the env. var to simulate the payment.
            // The payment can be successful or it can fail.
            if (options.CurrentValue.PaymentSucceeded)
            {
                orderPaymentIntegrationEvent = new OrderPaymentSucceededIntegrationEvent(@event.OrderId);
            }
            else
            {
                orderPaymentIntegrationEvent = new OrderPaymentFailedIntegrationEvent(@event.OrderId);
            }
        }

        logger.LogInformation(
            "Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})",
            orderPaymentIntegrationEvent.Id,
            orderPaymentIntegrationEvent);

        await eventBus.PublishAsync(orderPaymentIntegrationEvent);
    }
}

