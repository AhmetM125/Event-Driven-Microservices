using EventDrivenShop.Common.Exceptions;
using EventDrivenShop.Common.Inbox;
using EventDrivenShop.Common.Outbox;
using EventDrivenShop.Common.Pagination;
using EventDrivenShop.Contracts;
using EventDrivenShop.OrderService.Application.Common;
using EventDrivenShop.OrderService.Application.DTOs;
using EventDrivenShop.OrderService.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventDrivenShop.OrderService.Application.Services;

public sealed class OrderApplicationService : IOrderApplicationService
{
    private readonly IOrderDbContext _dbContext;
    private readonly IValidator<CreateOrderRequest> _validator;
    private readonly ILogger<OrderApplicationService> _logger;

    public OrderApplicationService(
        IOrderDbContext dbContext,
        IValidator<CreateOrderRequest> validator,
        ILogger<OrderApplicationService> logger)
    {
        _dbContext = dbContext;
        _validator = validator;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateOrderAsync(
        CreateOrderRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var orderId = Guid.NewGuid();

        var domainItems = request.Items.Select(i => (
            ProductId: i.ProductId,
            ProductName: i.ProductName,
            Quantity: i.Quantity,
            UnitPrice: i.UnitPrice
        ));

        // Create domain aggregate (calculates total server-side, sets PendingPayment)
        var order = Order.Create(
            orderId,
            request.CustomerId,
            request.CustomerEmail,
            request.Currency,
            domainItems);

        // Build integration event
        var orderCreatedEvent = new OrderCreatedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = correlationId,
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            CustomerEmail = order.CustomerEmail,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            Items = order.Items.Select(i => new OrderItemDto(
                i.ProductId,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
                i.TotalPrice)).ToList()
        };

        // Create Transactional Outbox record
        var outboxMessage = OutboxMessage.FromEvent(orderCreatedEvent);

        // Commit both Order aggregate + Outbox message in one single database transaction
        _dbContext.Orders.Add(order);
        _dbContext.OutboxMessages.Add(outboxMessage);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created order {OrderId} with TotalAmount {TotalAmount} {Currency} and saved OutboxMessage {OutboxId}",
            order.Id, order.TotalAmount, order.Currency, outboxMessage.Id);

        return MapToResponse(order);
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order is null ? null : MapToResponse(order);
    }

    public async Task<PagedResult<OrderResponse>> GetOrdersAsync(GetOrdersQuery query, CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };

        var dbQuery = _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .AsQueryable();

        if (query.CustomerId.HasValue && query.CustomerId.Value != Guid.Empty)
        {
            dbQuery = dbQuery.Where(o => o.CustomerId == query.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<OrderStatus>(query.Status, true, out var parsedStatus))
        {
            dbQuery = dbQuery.Where(o => o.Status == parsedStatus);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        dbQuery = query.SortDescending
            ? dbQuery.OrderByDescending(o => o.CreatedAtUtc)
            : dbQuery.OrderBy(o => o.CreatedAtUtc);

        var orders = await dbQuery
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var mapped = orders.Select(MapToResponse).ToList();
        return new PagedResult<OrderResponse>(mapped, pagination.PageNumber, pagination.PageSize, totalCount);
    }

    public async Task ProcessPaymentCompletedAsync(PaymentCompletedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        const string consumerName = "OrderService.PaymentCompletedConsumer";

        // Idempotency: check if this message was already processed
        var alreadyProcessed = await _dbContext.InboxMessages
            .AnyAsync(m => m.EventId == @event.EventId && m.Consumer == consumerName, cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Duplicate PaymentCompletedIntegrationEvent {EventId} for Order {OrderId} ignored via Inbox check.",
                @event.EventId, @event.OrderId);
            return;
        }

        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == @event.OrderId, cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found when processing PaymentCompleted event {EventId}", @event.OrderId, @event.EventId);
            return;
        }

        order.MarkAsPaid();

        // Persist Inbox record in the same atomic transaction
        _dbContext.InboxMessages.Add(InboxMessage.Create(@event.EventId, consumerName));

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} transitioned to Paid status after PaymentCompleted event {EventId}",
            order.Id, @event.EventId);
    }

    public async Task ProcessPaymentFailedAsync(PaymentFailedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        const string consumerName = "OrderService.PaymentFailedConsumer";

        // Idempotency: check if this message was already processed
        var alreadyProcessed = await _dbContext.InboxMessages
            .AnyAsync(m => m.EventId == @event.EventId && m.Consumer == consumerName, cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Duplicate PaymentFailedIntegrationEvent {EventId} for Order {OrderId} ignored via Inbox check.",
                @event.EventId, @event.OrderId);
            return;
        }

        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == @event.OrderId, cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found when processing PaymentFailed event {EventId}", @event.OrderId, @event.EventId);
            return;
        }

        order.MarkAsPaymentFailed(@event.Reason);

        // Persist Inbox record in the same atomic transaction
        _dbContext.InboxMessages.Add(InboxMessage.Create(@event.EventId, consumerName));

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} transitioned to PaymentFailed status ({Reason}) after event {EventId}",
            order.Id, @event.Reason, @event.EventId);
    }

    private static OrderResponse MapToResponse(Order order)
    {
        return new OrderResponse(
            order.Id,
            order.CustomerId,
            order.CustomerEmail,
            order.TotalAmount,
            order.Currency,
            order.Status.ToString(),
            order.FailureReason,
            order.CreatedAtUtc,
            order.UpdatedAtUtc,
            order.Items.Select(i => new OrderItemResponse(
                i.Id,
                i.ProductId,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
                i.TotalPrice)).ToList());
    }
}
