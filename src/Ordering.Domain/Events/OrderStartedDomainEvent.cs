
namespace eShop.Ordering.Domain.Events;

/// <summary>
/// Event used when an order is created.
/// Carries both legacy card information and logical payment method metadata
/// so that non-card payments (e.g., PayPal) can be handled without requiring card details.
/// </summary>
public record class OrderStartedDomainEvent(
    Order Order,
    string UserId,
    string UserName,
    int CardTypeId,
    string CardNumber,
    string CardSecurityNumber,
    string CardHolderName,
    DateTime CardExpiration,
    string PaymentMethod = "Card",
    string PayPalOrderId = null) : INotification;
