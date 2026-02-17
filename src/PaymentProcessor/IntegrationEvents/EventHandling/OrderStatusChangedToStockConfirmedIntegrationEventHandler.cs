using eShop.PaymentProcessor.PayPal;

namespace eShop.PaymentProcessor.IntegrationEvents.EventHandling;

public class OrderStatusChangedToStockConfirmedIntegrationEventHandler(
    IEventBus eventBus,
    IOptionsMonitor<PaymentOptions> options,
    ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler> logger,
    IPayPalOrderCaptureClient payPalOrderCaptureClient) :
    IIntegrationEventHandler<OrderStatusChangedToStockConfirmedIntegrationEvent>
{
    public async Task Handle(OrderStatusChangedToStockConfirmedIntegrationEvent @event)
    {
        logger.LogInformation(
            "Handling integration event: {IntegrationEventId} - ({@IntegrationEvent})",
            @event.Id,
            @event);

        IntegrationEvent orderPaymentIntegrationEvent;

        // Business feature comment:
        // When OrderStatusChangedToStockConfirmed Integration Event is handled.
        // For legacy card/test flows, we continue to simulate payment via configuration.
        // For PayPal orders, we perform a real capture against the PayPal Orders API.

        if (IsPayPalOrder(@event))
        {
            orderPaymentIntegrationEvent = await HandlePayPalPaymentAsync(@event, payPalOrderCaptureClient, logger);
        }
        else
        {
            orderPaymentIntegrationEvent = HandleCardOrSimulatedPayment(@event, options);
        }

        logger.LogInformation(
            "Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})",
            orderPaymentIntegrationEvent.Id,
            orderPaymentIntegrationEvent);

        await eventBus.PublishAsync(orderPaymentIntegrationEvent);
    }

    private static bool IsPayPalOrder(OrderStatusChangedToStockConfirmedIntegrationEvent @event)
        => string.Equals(@event.PaymentMethod, "PayPal", StringComparison.OrdinalIgnoreCase);

    private static IntegrationEvent HandleCardOrSimulatedPayment(
        OrderStatusChangedToStockConfirmedIntegrationEvent @event,
        IOptionsMonitor<PaymentOptions> options)
    {
        if (options.CurrentValue.PaymentSucceeded)
        {
            return new OrderPaymentSucceededIntegrationEvent(@event.OrderId);
        }

        return new OrderPaymentFailedIntegrationEvent(@event.OrderId);
    }

    private static async Task<IntegrationEvent> HandlePayPalPaymentAsync(
        OrderStatusChangedToStockConfirmedIntegrationEvent @event,
        IPayPalOrderCaptureClient payPalOrderCaptureClient,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(@event.PayPalOrderId))
        {
            logger.LogWarning(
                "Received PayPal payment for OrderId {OrderId} but PayPalOrderId is missing.",
                @event.OrderId);
            return new OrderPaymentFailedIntegrationEvent(@event.OrderId);
        }

        var idempotencyKey = BuildPayPalCaptureIdempotencyKey(@event.OrderId, @event.PayPalOrderId);

        var succeeded = await payPalOrderCaptureClient.CaptureOrderAsync(
            @event.PayPalOrderId,
            idempotencyKey);

        return succeeded
            ? new OrderPaymentSucceededIntegrationEvent(@event.OrderId)
            : new OrderPaymentFailedIntegrationEvent(@event.OrderId);
    }

    private static string BuildPayPalCaptureIdempotencyKey(int orderId, string paypalOrderId)
        => $"capture-{orderId}-{paypalOrderId}";
}

