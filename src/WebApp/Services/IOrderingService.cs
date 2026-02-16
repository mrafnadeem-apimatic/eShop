namespace eShop.WebApp.Services;

public interface IOrderingService
{
    Task CreateOrder(CreateOrderRequest request, Guid requestId);
}
