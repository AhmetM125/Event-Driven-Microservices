using EventDrivenShop.OrderService.Application.DTOs;
using EventDrivenShop.OrderService.Application.Validators;
using FluentAssertions;
using Xunit;

namespace EventDrivenShop.UnitTests.Validation;

public sealed class OrderValidationTests
{
    private readonly CreateOrderRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_ShouldPassValidation()
    {
        // Arrange
        var request = new CreateOrderRequest(
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "buyer@example.com",
            Currency: "USD",
            Items: new List<CreateOrderItemRequest>
            {
                new(Guid.NewGuid(), "Mechanical Keyboard", 1, 120.00m),
                new(Guid.NewGuid(), "Desk Mat", 2, 25.00m)
            });

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("")]
    public void Validate_InvalidEmail_ShouldFailValidation(string email)
    {
        // Arrange
        var request = new CreateOrderRequest(
            CustomerId: Guid.NewGuid(),
            CustomerEmail: email,
            Currency: "USD",
            Items: new List<CreateOrderItemRequest>
            {
                new(Guid.NewGuid(), "Item", 1, 10.00m)
            });

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("CustomerEmail"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Validate_InvalidQuantity_ShouldFailValidation(int quantity)
    {
        // Arrange
        var request = new CreateOrderRequest(
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "valid@example.com",
            Currency: "EUR",
            Items: new List<CreateOrderItemRequest>
            {
                new(Guid.NewGuid(), "Item", quantity, 10.00m)
            });

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Quantity"));
    }

    [Fact]
    public void Validate_NegativeUnitPrice_ShouldFailValidation()
    {
        // Arrange
        var request = new CreateOrderRequest(
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "valid@example.com",
            Currency: "EUR",
            Items: new List<CreateOrderItemRequest>
            {
                new(Guid.NewGuid(), "Item", 1, -5.00m)
            });

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("UnitPrice"));
    }
}
