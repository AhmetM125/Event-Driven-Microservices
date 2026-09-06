namespace EventDrivenShop.PaymentService.Application.DTOs;

public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string Status,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
