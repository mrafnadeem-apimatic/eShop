using Ordering.Domain.Models;

namespace Ordering.Domain.Services;

public interface IPaymentProviderService
{
    Task<OrderPaymentUri> CreateOrderPayment(Order order);
}
