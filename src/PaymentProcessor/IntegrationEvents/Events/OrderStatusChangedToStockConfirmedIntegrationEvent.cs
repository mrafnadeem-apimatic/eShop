namespace eShop.PaymentProcessor.IntegrationEvents.Events;

public record OrderStatusChangedToStockConfirmedIntegrationEvent(
    int OrderId,
    string ExternalPaymentId) : IntegrationEvent;
