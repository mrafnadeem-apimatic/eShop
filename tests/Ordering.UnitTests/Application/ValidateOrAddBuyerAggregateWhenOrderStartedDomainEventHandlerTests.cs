namespace eShop.Ordering.UnitTests.Application;

using eShop.Ordering.API.Application.DomainEventHandlers;
using eShop.Ordering.API.Application.IntegrationEvents;
using eShop.Ordering.API.Application.IntegrationEvents.Events;
using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;
using eShop.Ordering.Domain.Seedwork;

[TestClass]
public class ValidateOrAddBuyerAggregateWhenOrderStartedDomainEventHandlerTests
{
    private static ValidateOrAddBuyerAggregateWhenOrderStartedDomainEventHandler CreateHandler(
        IBuyerRepository buyerRepository,
        IOrderingIntegrationEventService integrationEventService)
    {
        var logger = Substitute.For<ILogger<ValidateOrAddBuyerAggregateWhenOrderStartedDomainEventHandler>>();
        return new ValidateOrAddBuyerAggregateWhenOrderStartedDomainEventHandler(logger, buyerRepository, integrationEventService);
    }

    private static (Order Order, OrderStartedDomainEvent DomainEvent) CreateOrderStartedEvent(
        string paymentMethod,
        string payPalOrderId = null)
    {
        var address = new Address("street", "city", "state", "country", "zipcode");
        var order = new Order(
            "user-1",
            "Test User",
            address,
            cardTypeId: 1,
            cardNumber: "4111111111111111",
            cardSecurityNumber: "123",
            cardHolderName: "Test User",
            cardExpiration: DateTime.UtcNow.AddYears(1),
            paymentMethod: paymentMethod,
            payPalOrderId: payPalOrderId);

        var domainEvent = new OrderStartedDomainEvent(
            order,
            "user-1",
            "Test User",
            1,
            "4111111111111111",
            "123",
            "Test User",
            DateTime.UtcNow.AddYears(1),
            paymentMethod,
            payPalOrderId);

        return (order, domainEvent);
    }

    [TestMethod]
    public async Task Handle_card_payment_creates_payment_method_and_integration_event()
    {
        // Arrange
        var buyerRepository = Substitute.For<IBuyerRepository>();
        var integrationEventService = Substitute.For<IOrderingIntegrationEventService>();

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveEntitiesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        buyerRepository.UnitOfWork.Returns(unitOfWork);

        buyerRepository.FindAsync(Arg.Any<string>()).Returns((Buyer)null);

        Buyer addedBuyer = null;
        buyerRepository.Add(Arg.Do<Buyer>(b => addedBuyer = b)).Returns(callInfo => addedBuyer);

        var (_, domainEvent) = CreateOrderStartedEvent(paymentMethod: "Card");
        var handler = CreateHandler(buyerRepository, integrationEventService);

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        Assert.IsNotNull(addedBuyer);
        Assert.IsTrue(addedBuyer.DomainEvents.OfType<BuyerAndPaymentMethodVerifiedDomainEvent>().Any());
        await integrationEventService.Received(1)
            .AddAndSaveEventAsync(Arg.Any<OrderStatusChangedToSubmittedIntegrationEvent>());
    }

    [TestMethod]
    public async Task Handle_paypal_payment_skips_card_validation_but_still_publishes_integration_event()
    {
        // Arrange
        var buyerRepository = Substitute.For<IBuyerRepository>();
        var integrationEventService = Substitute.For<IOrderingIntegrationEventService>();

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveEntitiesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        buyerRepository.UnitOfWork.Returns(unitOfWork);

        buyerRepository.FindAsync(Arg.Any<string>()).Returns((Buyer)null);

        Buyer addedBuyer = null;
        buyerRepository.Add(Arg.Do<Buyer>(b => addedBuyer = b)).Returns(callInfo => addedBuyer);

        var (_, domainEvent) = CreateOrderStartedEvent(paymentMethod: "PayPal", payPalOrderId: "PAYPAL-123");
        var handler = CreateHandler(buyerRepository, integrationEventService);

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        Assert.IsNotNull(addedBuyer);
        var buyerEvents = addedBuyer.DomainEvents ?? Array.Empty<INotification>();
        Assert.IsFalse(buyerEvents.OfType<BuyerAndPaymentMethodVerifiedDomainEvent>().Any());
        await integrationEventService.Received(1)
            .AddAndSaveEventAsync(Arg.Any<OrderStatusChangedToSubmittedIntegrationEvent>());
    }
}

