using Ordering.Domain.Models;
using Ordering.Domain.Services;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Controllers;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Http.Response;
using PaypalServerSdk.Standard.Models;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;
using Order = eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order;

namespace Ordering.Infrastructure.Services;

public class PayPalPaymentProviderService(IConfiguration configuration) : IPaymentProviderService
{
    private readonly PaypalServerSdkClient _client = PaypalServerSdkClient.FromConfiguration(configuration.GetSection("PaypalServerSdk"));

    public async Task<OrderPaymentUri> CreateOrderPayment(Order order)
    {
        OrdersController ordersController = _client.OrdersController;

        var totalAmount = order.GetTotal().ToString("C");
        CreateOrderInput createOrderInput = new CreateOrderInput
        {
            Body = new OrderRequest
            {
                Intent = CheckoutPaymentIntent.Authorize,
                PurchaseUnits =
                [
                    new PurchaseUnitRequest
                    {
                        Amount = new AmountWithBreakdown
                        {
                            CurrencyCode = CurrencyFormats.Usd,
                            MValue = totalAmount,
                            Breakdown = new AmountBreakdown {
                                ItemTotal = new Money {
                                    CurrencyCode = CurrencyFormats.Usd,
                                    MValue = totalAmount,
                                },
                            },
                        },
                        Items = order.OrderItems.Select(item =>
                            new ItemRequest
                            {
                                Name = item.ProductName,
                                UnitAmount = new Money { CurrencyCode = CurrencyFormats.Usd, MValue = item.UnitPrice.ToString("C"), }, // This might conflict with the total.
                                Quantity = item.Units.ToString(),
                                Sku = item.GetSku(), // Not sure about this
                            }).ToList(),
                    }
                ]
            },
            Prefer = "return=minimal",
        };

        try
        {
            ApiResponse<PaypalServerSdk.Standard.Models.Order> result = await ordersController.CreateOrderAsync(createOrderInput);

            return new OrderPaymentUri(result.Data.Links
                .First(link => link.Rel == "approve" && link.Method == LinkHttpMethod.Get).Href);
        }
        catch (ApiException e)
        {
            throw new OrderingDomainException(e.Message);
        }
    }
}
