using EventDrivenShop.Common.Inbox;
using EventDrivenShop.Common.Outbox;
using EventDrivenShop.Contracts;
using EventDrivenShop.PaymentService.Application.Common;
using EventDrivenShop.PaymentService.Application.DTOs;
using EventDrivenShop.PaymentService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventDrivenShop.PaymentService.Application.Services;

public sealed class PaymentApplicationService : IPaymentApplicationService
{
    private readonly IPaymentDbContext _dbContext;
    private readonly IPaymentSimulator _paymentSimulator;
    private readonly ILogger<PaymentApplicationService> _logger;

    public PaymentApplicationService(
        IPaymentDbContext dbContext,
        IPaymentSimulator paymentSimulator,
        ILogger<PaymentApplicationService> logger)
    {
        _dbContext = dbContext;
        _paymentSimulator = paymentSimulator;
        _logger = logger;
    }

    public async Task ProcessOrderCreatedAsync(
        OrderCreatedIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        const string consumerName = "PaymentService.OrderCreatedConsumer";

        // 1. Inbox Deduplication check
        var alreadyProcessed = await _dbContext.InboxMessages
            .AnyAsync(m => m.EventId == @event.EventId && m.Consumer == consumerName, cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Duplicate OrderCreatedIntegrationEvent {EventId} for Order {OrderId} ignored via Inbox deduplication.",
                @event.EventId, @event.OrderId);
            return;
        }

        // 2. Check if a payment already exists for this order (business idempotency)
        var existingPayment = await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.OrderId == @event.OrderId, cancellationToken);

        if (existingPayment is not null)
        {
            _logger.LogWarning(
                "Payment already exists for Order {OrderId} (PaymentId: {PaymentId}). Recording Inbox deduplication.",
                @event.OrderId, existingPayment.Id);

            _dbContext.InboxMessages.Add(InboxMessage.Create(@event.EventId, consumerName));
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // 3. Simulate payment processing
        var simulationResult = _paymentSimulator.Simulate(
            @event.CustomerEmail,
            @event.TotalAmount,
            @event.Currency);

        var paymentId = Guid.NewGuid();
        OutboxMessage outboxMessage;
        Payment payment;

        if (simulationResult.IsSuccess)
        {
            payment = Payment.Create(
                paymentId,
                @event.OrderId,
                @event.TotalAmount,
                @event.Currency,
                PaymentStatus.Completed);

            var paymentCompletedEvent = new PaymentCompletedIntegrationEvent
            {
                EventId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                CorrelationId = @event.CorrelationId,
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                Amount = payment.Amount,
                Currency = payment.Currency
            };

            outboxMessage = OutboxMessage.FromEvent(paymentCompletedEvent);

            _logger.LogInformation(
                "Payment {PaymentId} succeeded for Order {OrderId} ({Amount} {Currency})",
                payment.Id, payment.OrderId, payment.Amount, payment.Currency);
        }
        else
        {
            payment = Payment.Create(
                paymentId,
                @event.OrderId,
                @event.TotalAmount,
                @event.Currency,
                PaymentStatus.Failed,
                simulationResult.FailureReason);

            var paymentFailedEvent = new PaymentFailedIntegrationEvent
            {
                EventId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                CorrelationId = @event.CorrelationId,
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Reason = simulationResult.FailureReason ?? "Payment declined."
            };

            outboxMessage = OutboxMessage.FromEvent(paymentFailedEvent);

            _logger.LogWarning(
                "Payment {PaymentId} failed for Order {OrderId}: {FailureReason}",
                payment.Id, payment.OrderId, simulationResult.FailureReason);
        }

        // 4. Save Payment, Outbox message, and Inbox deduplication record in ONE atomic transaction
        _dbContext.Payments.Add(payment);
        _dbContext.OutboxMessages.Add(outboxMessage);
        _dbContext.InboxMessages.Add(InboxMessage.Create(@event.EventId, consumerName));

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Committed Payment {PaymentId}, OutboxMessage {OutboxId}, and InboxMessage for Event {EventId} atomically.",
            payment.Id, outboxMessage.Id, @event.EventId);
    }

    public async Task<PaymentResponse?> GetPaymentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return payment is null ? null : MapToResponse(payment);
    }

    public async Task<PaymentResponse?> GetPaymentByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);

        return payment is null ? null : MapToResponse(payment);
    }

    private static PaymentResponse MapToResponse(Payment payment)
    {
        return new PaymentResponse(
            payment.Id,
            payment.OrderId,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            payment.FailureReason,
            payment.CreatedAtUtc,
            payment.UpdatedAtUtc);
    }
}
