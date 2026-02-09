namespace eShop.Ordering.API.Application.IntegrationEvents.Events;

public record OrderStatusChangedToStockConfirmedIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }
    public OrderStatus OrderStatus { get; }
    public string BuyerName { get; }
    public string BuyerIdentityGuid { get; }
    public string ExternalPaymentId { get; }

    public OrderStatusChangedToStockConfirmedIntegrationEvent(
        int orderId,
        OrderStatus orderStatus,
        string buyerName,
        string buyerIdentityGuid,
        string externalPaymentId)
    {
        OrderId = orderId;
        OrderStatus = orderStatus;
        BuyerName = buyerName;
        BuyerIdentityGuid = buyerIdentityGuid;
        ExternalPaymentId = externalPaymentId;
    }
}
