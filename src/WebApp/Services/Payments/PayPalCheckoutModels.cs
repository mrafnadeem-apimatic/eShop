namespace eShop.WebApp.Services.Payments;

/// <summary>
/// Application-level result of creating a PayPal order (no SDK types).
/// </summary>
public sealed record CreatePayPalOrderResult(string PaypalOrderId, string ApprovalUrl);

/// <summary>
/// Line item for creating a PayPal order (no SDK types).
/// </summary>
public sealed record PayPalLineItem(string Name, int Quantity, decimal UnitPrice);

/// <summary>
/// Application-level request for the PayPal orders client (no SDK types).
/// </summary>
public sealed record CreatePayPalOrderRequest(
    string BasketId,
    string UserId,
    IReadOnlyList<PayPalLineItem> Items);
