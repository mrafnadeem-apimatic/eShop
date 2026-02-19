using System.ComponentModel.DataAnnotations;

namespace eShop.WebApp.Services;

public class BasketCheckoutInfo
{
    [Required]
    public string? Street { get; set; }

    [Required]
    public string? City { get; set; }

    [Required]
    public string? State { get; set; }

    [Required]
    public string? Country { get; set; }

    [Required]
    public string? ZipCode { get; set; }

    public string? CardNumber { get; set; }

    public string? CardHolderName { get; set; }

    public string? CardSecurityNumber { get; set; }

    public DateTime? CardExpiration { get; set; }

    public int CardTypeId { get; set; }

    public string? Buyer { get; set; }
    public Guid RequestId { get; set; }

    /// <summary>
    /// Indicates how the customer chose to pay for the order.
    /// Defaults to <c>Card</c> for the existing credit card flow.
    /// </summary>
    public string PaymentMethod { get; set; } = "Card";

    /// <summary>
    /// The PayPal order identifier to associate with this checkout, when
    /// <see cref="PaymentMethod"/> is <c>PayPal</c>. For card-based flows this
    /// value is not required and will be ignored.
    /// </summary>
    public string? PayPalOrderId { get; set; }
}
