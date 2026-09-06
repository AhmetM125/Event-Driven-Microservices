namespace EventDrivenShop.OrderService.Application.DTOs;

public sealed record CreateOrderItemRequest(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

public sealed record CreateOrderRequest(
    Guid CustomerId,
    string CustomerEmail,
    string Currency,
    List<CreateOrderItemRequest> Items);

public sealed record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerEmail,
    decimal TotalAmount,
    string Currency,
    string Status,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<OrderItemResponse> Items);

public sealed record GetOrdersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Status = null,
    Guid? CustomerId = null,
    string? SortBy = "CreatedAtUtc",
    bool SortDescending = true);
