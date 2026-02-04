using eShop.EventBus.Events;

namespace eShop.WebApp.Services.OrderStatus.IntegrationEvents;

public record OrderStatusChangedToStockConfirmedIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }
    public string OrderStatus { get; }
    public string BuyerName { get; }
    public string BuyerIdentityGuid { get; }
    public string ExternalPaymentId { get; }

    public OrderStatusChangedToStockConfirmedIntegrationEvent(
        int orderId,
        string orderStatus,
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
