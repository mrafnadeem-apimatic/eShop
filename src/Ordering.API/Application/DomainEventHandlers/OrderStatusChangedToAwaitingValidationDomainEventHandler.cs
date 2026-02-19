namespace eShop.Ordering.API.Application.DomainEventHandlers;

public class OrderStatusChangedToAwaitingValidationDomainEventHandler
                : INotificationHandler<OrderStatusChangedToAwaitingValidationDomainEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger _logger;
    private readonly IBuyerRepository _buyerRepository;
    private readonly IOrderingIntegrationEventService _orderingIntegrationEventService;

    public OrderStatusChangedToAwaitingValidationDomainEventHandler(
        IOrderRepository orderRepository,
        ILogger<OrderStatusChangedToAwaitingValidationDomainEventHandler> logger,
        IBuyerRepository buyerRepository,
        IOrderingIntegrationEventService orderingIntegrationEventService)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _buyerRepository = buyerRepository;
        _orderingIntegrationEventService = orderingIntegrationEventService;
    }

    public async Task Handle(OrderStatusChangedToAwaitingValidationDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        OrderingApiTrace.LogOrderStatusUpdated(_logger, domainEvent.OrderId, OrderStatus.AwaitingValidation);

        var order = await _orderRepository.GetAsync(domainEvent.OrderId);
        Buyer buyer = null;
        if (order.BuyerId is int buyerId)
        {
            buyer = await _buyerRepository.FindByIdAsync(buyerId);
            if (buyer is null)
            {
                _logger.LogWarning(
                    "Buyer with id {BuyerId} not found for order {OrderId}; publishing event with empty buyer info.",
                    buyerId,
                    order.Id);
            }
        }

        var orderStockList = domainEvent.OrderItems
            .Select(orderItem => new OrderStockItem(orderItem.ProductId, orderItem.Units));

        var buyerName = buyer?.Name ?? string.Empty;
        var buyerIdentityGuid = buyer?.IdentityGuid ?? string.Empty;

        var integrationEvent = new OrderStatusChangedToAwaitingValidationIntegrationEvent(
            order.Id,
            order.OrderStatus,
            buyerName,
            buyerIdentityGuid,
            orderStockList);
        await _orderingIntegrationEventService.AddAndSaveEventAsync(integrationEvent);
    }
}
