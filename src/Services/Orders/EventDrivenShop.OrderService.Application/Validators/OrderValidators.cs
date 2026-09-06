using EventDrivenShop.OrderService.Application.DTOs;
using FluentValidation;

namespace EventDrivenShop.OrderService.Application.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    private static readonly string[] AllowedCurrencies = ["USD", "EUR", "GBP", "TRY"];

    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty().WithMessage("CustomerEmail is required.")
            .EmailAddress().WithMessage("CustomerEmail must be a valid email address.")
            .MaximumLength(200).WithMessage("CustomerEmail must not exceed 200 characters.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Must(c => AllowedCurrencies.Contains(c?.Trim().ToUpperInvariant()))
            .WithMessage($"Currency must be one of: {string.Join(", ", AllowedCurrencies)}.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("An order must contain at least one item.")
            .Must(items => items != null && items.Count <= 50).WithMessage("An order cannot contain more than 50 items.");

        RuleForEach(x => x.Items).SetValidator(new CreateOrderItemRequestValidator());
    }
}

public sealed class CreateOrderItemRequestValidator : AbstractValidator<CreateOrderItemRequest>
{
    public CreateOrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId is required.");

        RuleFor(x => x.ProductName)
            .NotEmpty().WithMessage("ProductName is required.")
            .MaximumLength(200).WithMessage("ProductName cannot exceed 200 characters.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0.")
            .LessThanOrEqualTo(1000).WithMessage("Quantity cannot exceed 1000.");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0.01m).WithMessage("UnitPrice must be at least 0.01.");
    }
}
