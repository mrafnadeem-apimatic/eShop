namespace eShop.Ordering.API.Application.DomainEventHandlers;

public class OrderStatusChangedToStockConfirmedDomainEventHandler
                : INotificationHandler<OrderStatusChangedToStockConfirmedDomainEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IBuyerRepository _buyerRepository;
    private readonly ILogger _logger;
    private readonly IOrderingIntegrationEventService _orderingIntegrationEventService;

    public OrderStatusChangedToStockConfirmedDomainEventHandler(
        IOrderRepository orderRepository,
        IBuyerRepository buyerRepository,
        ILogger<OrderStatusChangedToStockConfirmedDomainEventHandler> logger,
        IOrderingIntegrationEventService orderingIntegrationEventService)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _buyerRepository = buyerRepository ?? throw new ArgumentNullException(nameof(buyerRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _orderingIntegrationEventService = orderingIntegrationEventService;
    }

    public async Task Handle(OrderStatusChangedToStockConfirmedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        OrderingApiTrace.LogOrderStatusUpdated(_logger, domainEvent.OrderId, OrderStatus.StockConfirmed);

        var order = await _orderRepository.GetAsync(domainEvent.OrderId);
        Buyer buyer = null;
        if (order.BuyerId is int buyerId)
        {
            buyer = await _buyerRepository.FindByIdAsync(buyerId);
        }

        var buyerName = buyer?.Name ?? string.Empty;
        var buyerIdentityGuid = buyer?.IdentityGuid ?? string.Empty;

        var integrationEvent = new OrderStatusChangedToStockConfirmedIntegrationEvent(
            order.Id,
            order.OrderStatus,
            buyerName,
            buyerIdentityGuid,
            order.PaymentMethod,
            order.PayPalOrderId);
        await _orderingIntegrationEventService.AddAndSaveEventAsync(integrationEvent);
    }
}
