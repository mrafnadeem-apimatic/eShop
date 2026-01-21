namespace eShop.Ordering.API.Application.Models
{
    public record OrderSubmission(
        bool OrderSubmitted,
        string ApprovalUri
    );
}
