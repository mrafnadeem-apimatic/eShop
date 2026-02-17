namespace eShop.Ordering.UnitTests.Application;

using eShop.Ordering.API.Application.Validations;

[TestClass]
public class CreateOrderCommandValidatorTests
{
    private static CreateOrderCommandValidator CreateValidator()
    {
        var logger = Substitute.For<ILogger<CreateOrderCommandValidator>>();
        return new CreateOrderCommandValidator(logger);
    }

    private static List<BasketItem> CreateDefaultBasketItems()
    {
        return
        [
            new BasketItem
            {
                Id = "1",
                ProductId = 1,
                ProductName = "Test product",
                UnitPrice = 10m,
                OldUnitPrice = 10m,
                Quantity = 1,
                PictureUrl = "test"
            }
        ];
    }

    [TestMethod]
    public void Validate_paypal_payment_requires_paypal_order_id()
    {
        // Arrange
        var validator = CreateValidator();
        var basketItems = CreateDefaultBasketItems();

        var command = new CreateOrderCommand(
            basketItems,
            userId: "user-1",
            userName: "Test User",
            city: "City",
            street: "Street",
            state: "State",
            country: "Country",
            zipcode: "12345",
            cardNumber: null,
            cardHolderName: null,
            cardExpiration: DateTime.UtcNow.AddYears(1),
            cardSecurityNumber: null,
            cardTypeId: 0,
            paymentMethod: "PayPal",
            payPalOrderId: null);

        // Act
        var result = validator.Validate(command);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.PropertyName == nameof(CreateOrderCommand.PayPalOrderId)));
    }

    [TestMethod]
    public void Validate_paypal_payment_does_not_require_card_details()
    {
        // Arrange
        var validator = CreateValidator();
        var basketItems = CreateDefaultBasketItems();

        var command = new CreateOrderCommand(
            basketItems,
            userId: "user-1",
            userName: "Test User",
            city: "City",
            street: "Street",
            state: "State",
            country: "Country",
            zipcode: "12345",
            cardNumber: null,
            cardHolderName: null,
            cardExpiration: DateTime.UtcNow.AddYears(1),
            cardSecurityNumber: null,
            cardTypeId: 0,
            paymentMethod: "PayPal",
            payPalOrderId: "PAYPAL-123");

        // Act
        var result = validator.Validate(command);

        // Assert
        Assert.IsTrue(result.IsValid);
    }
}

