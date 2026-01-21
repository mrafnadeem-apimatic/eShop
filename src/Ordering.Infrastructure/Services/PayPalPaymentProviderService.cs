using Ordering.Domain.Models;
using Ordering.Domain.Services;

namespace Ordering.Infrastructure.Services
{
    public class PayPalPaymentProviderService : IPaymentProviderService
    {
        public PayPalPaymentProviderService()
        {
        }

        public Task<OrderPaymentUri> CreateOrderPayment(Order order)
        {
            return Task.FromResult(new OrderPaymentUri("user/orders"));
        }
    }
}
