namespace eShop.PaymentProcessor.IntegrationEvents.Events;

public record OrderStatusChangedToStockConfirmedIntegrationEvent(
    int OrderId,
    /// <summary>
    /// Logical payment method for this order (e.g., "Card", "PayPal").
    /// Defaults to "Card" for backwards compatibility.
    /// </summary>
    string PaymentMethod = "Card",
    /// <summary>
    /// PayPal order identifier when the payment method is PayPal.
    /// </summary>
    string PayPalOrderId = null) : IntegrationEvent;
