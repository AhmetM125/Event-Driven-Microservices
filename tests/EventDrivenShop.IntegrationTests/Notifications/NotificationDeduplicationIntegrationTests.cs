using EventDrivenShop.Contracts;
using EventDrivenShop.NotificationService.Application.DTOs;
using EventDrivenShop.NotificationService.Application.Services;
using EventDrivenShop.NotificationService.Domain;
using EventDrivenShop.NotificationService.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventDrivenShop.IntegrationTests.Notifications;

public sealed class NotificationDeduplicationIntegrationTests
{
    private static NotificationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: $"NotificationsDb_{Guid.NewGuid()}")
            .Options;

        return new NotificationDbContext(options);
    }

    [Fact]
    public async Task ProcessPaymentCompleted_WhenDeliveredMultipleTimes_ShouldCreateSingleNotification()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new NotificationApplicationService(dbContext, NullLogger<NotificationApplicationService>.Instance);

        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var paymentCompletedEvent = new PaymentCompletedIntegrationEvent
        {
            EventId = eventId,
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = "corr-notif-dedup",
            PaymentId = Guid.NewGuid(),
            OrderId = orderId,
            Amount = 199.99m,
            Currency = "EUR"
        };

        // Act - Deliver 3 times
        await service.ProcessPaymentCompletedAsync(paymentCompletedEvent);
        await service.ProcessPaymentCompletedAsync(paymentCompletedEvent);
        await service.ProcessPaymentCompletedAsync(paymentCompletedEvent);

        // Assert - Exactly 1 notification persisted
        var notifications = await dbContext.Notifications.Where(n => n.OrderId == orderId).ToListAsync();
        notifications.Should().HaveCount(1);
        notifications[0].Type.Should().Be(NotificationType.PaymentSucceeded);
        notifications[0].Content.Should().Contain("199.99 EUR");

        // Assert - Exactly 1 inbox entry
        var inboxCount = await dbContext.InboxMessages.CountAsync(m => m.EventId == eventId);
        inboxCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessPaymentFailed_ShouldCreatePaymentFailedNotification()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new NotificationApplicationService(dbContext, NullLogger<NotificationApplicationService>.Instance);

        var orderId = Guid.NewGuid();
        var paymentFailedEvent = new PaymentFailedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = "corr-notif-fail",
            PaymentId = Guid.NewGuid(),
            OrderId = orderId,
            Amount = 50.00m,
            Currency = "USD",
            Reason = "Insufficient funds"
        };

        // Act
        await service.ProcessPaymentFailedAsync(paymentFailedEvent);

        // Assert
        var notifications = await service.GetNotificationsByOrderIdAsync(orderId);
        notifications.Should().HaveCount(1);
        notifications[0].Type.Should().Be(NotificationType.PaymentFailed.ToString());
        notifications[0].Content.Should().Contain("failed");
        notifications[0].Content.Should().Contain("Insufficient funds");
    }
}
