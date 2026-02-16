namespace eShop.PaymentProcessor.IntegrationEvents.Events;

public record OrderStatusChangedToStockConfirmedIntegrationEvent(
    int OrderId,
    string PaymentMethod = "Card",
    string PayPalOrderId = "") : IntegrationEvent;
