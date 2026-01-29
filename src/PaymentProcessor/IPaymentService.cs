namespace eShop.PaymentProcessor;

public interface IPaymentService
{
    Task<bool> ProcessPaymentAsync(int orderId, CancellationToken cancellationToken = default);
}


