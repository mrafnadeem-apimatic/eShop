namespace eShop.Ordering.API.Application.IntegrationEvents.Events;

public record OrderStatusChangedToStockConfirmedIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }
    public OrderStatus OrderStatus { get; }
    public string BuyerName { get; }
    public string BuyerIdentityGuid { get; }
    /// <summary>
    /// Logical payment method for this order (e.g., "Card", "PayPal").
    /// Defaults to "Card" for backwards compatibility.
    /// </summary>
    public string PaymentMethod { get; }
    /// <summary>
    /// PayPal order identifier when the payment method is PayPal.
    /// </summary>
    public string PayPalOrderId { get; }

    public OrderStatusChangedToStockConfirmedIntegrationEvent(
        int orderId,
        OrderStatus orderStatus,
        string buyerName,
        string buyerIdentityGuid,
        string paymentMethod,
        string payPalOrderId)
    {
        OrderId = orderId;
        OrderStatus = orderStatus;
        BuyerName = buyerName;
        BuyerIdentityGuid = buyerIdentityGuid;
        PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Card" : paymentMethod;
        PayPalOrderId = payPalOrderId;
    }
}
