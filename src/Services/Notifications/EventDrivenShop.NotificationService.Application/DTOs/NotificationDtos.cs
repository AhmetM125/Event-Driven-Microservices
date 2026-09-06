namespace EventDrivenShop.NotificationService.Application.DTOs;

public sealed record NotificationResponse(
    Guid Id,
    Guid OrderId,
    string Recipient,
    string Type,
    string Status,
    string Content,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);

public sealed record GetNotificationsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    Guid? OrderId = null);
