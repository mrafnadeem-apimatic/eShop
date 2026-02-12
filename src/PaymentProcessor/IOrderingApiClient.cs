#nullable enable
namespace eShop.PaymentProcessor;

public interface IOrderingApiClient
{
    Task<OrderDto?> GetOrderAsync(int orderId, CancellationToken cancellationToken = default);
}
