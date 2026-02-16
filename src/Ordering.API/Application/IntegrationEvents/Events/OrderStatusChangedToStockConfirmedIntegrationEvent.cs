namespace eShop.Ordering.API.Application.IntegrationEvents.Events;

public record OrderStatusChangedToStockConfirmedIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }
    public OrderStatus OrderStatus { get; }
    public string BuyerName { get; }
    public string BuyerIdentityGuid { get; }
    public string PaymentMethod { get; }
    public string PayPalOrderId { get; }

    public OrderStatusChangedToStockConfirmedIntegrationEvent(
        int orderId,
        OrderStatus orderStatus,
        string buyerName,
        string buyerIdentityGuid,
        string paymentMethod = null,
        string payPalOrderId = null)
    {
        OrderId = orderId;
        OrderStatus = orderStatus;
        BuyerName = buyerName;
        BuyerIdentityGuid = buyerIdentityGuid;
        PaymentMethod = paymentMethod ?? "Card";
        PayPalOrderId = payPalOrderId ?? string.Empty;
    }
}
