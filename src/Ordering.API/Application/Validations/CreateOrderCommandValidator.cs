namespace eShop.Ordering.API.Application.Validations;
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator(ILogger<CreateOrderCommandValidator> logger)
    {
        RuleFor(command => command.City).NotEmpty();
        RuleFor(command => command.Street).NotEmpty();
        RuleFor(command => command.State).NotEmpty();
        RuleFor(command => command.Country).NotEmpty();
        RuleFor(command => command.ZipCode).NotEmpty();

        // Common rule for all payment methods
        RuleFor(command => command.OrderItems)
            .Must(ContainOrderItems)
            .WithMessage("No order items found");

        // Allowed payment methods
        RuleFor(command => command.PaymentMethod)
            .Must(paymentMethod =>
                string.IsNullOrWhiteSpace(paymentMethod)
                || paymentMethod.Equals("Card", StringComparison.OrdinalIgnoreCase)
                || paymentMethod.Equals("PayPal", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Unsupported payment method.");

        // Card-specific validation rules
        When(IsCardPayment, () =>
        {
            RuleFor(command => command.CardNumber)
                .NotEmpty()
                .Length(12, 19);

            RuleFor(command => command.CardHolderName)
                .NotEmpty();

            RuleFor(command => command.CardExpiration)
                .NotEmpty()
                .Must(BeValidExpirationDate)
                .WithMessage("Please specify a valid card expiration date");

            RuleFor(command => command.CardSecurityNumber)
                .NotEmpty()
                .Length(3);

            RuleFor(command => command.CardTypeId)
                .NotEmpty();
        });

        // PayPal-specific validation rules
        When(IsPayPalPayment, () =>
        {
            RuleFor(command => command.PayPalOrderId)
                .NotEmpty()
                .WithMessage("PayPalOrderId is required when payment method is PayPal");
        });

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("INSTANCE CREATED - {ClassName}", GetType().Name);
        }
    }

    private bool BeValidExpirationDate(DateTime dateTime)
    {
        return dateTime >= DateTime.UtcNow;
    }

    private bool ContainOrderItems(IEnumerable<OrderItemDTO> orderItems)
    {
        return orderItems.Any();
    }

    private static bool IsCardPayment(CreateOrderCommand command)
    {
        // Treat null/empty as Card for backwards compatibility
        return string.IsNullOrWhiteSpace(command.PaymentMethod)
               || command.PaymentMethod.Equals("Card", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPayPalPayment(CreateOrderCommand command)
    {
        return command.PaymentMethod is not null
               && command.PaymentMethod.Equals("PayPal", StringComparison.OrdinalIgnoreCase);
    }
}
