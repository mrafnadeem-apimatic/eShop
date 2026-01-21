using Ordering.Domain.Models;

namespace eShop.Ordering.API.Application.Models
{
    public record OrderSubmission(
        bool OrderSubmitted,
        OrderPaymentUri ApprovalUri
    );
}
