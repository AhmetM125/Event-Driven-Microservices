using EventDrivenShop.Contracts;
using EventDrivenShop.NotificationService.Application.Common;
using EventDrivenShop.NotificationService.Application.Services;
using EventDrivenShop.NotificationService.Domain;
using EventDrivenShop.NotificationService.Infrastructure.Consumers;
using EventDrivenShop.NotificationService.Infrastructure.Persistence;
using EventDrivenShop.OrderService.Application.Common;
using EventDrivenShop.OrderService.Application.DTOs;
using EventDrivenShop.OrderService.Application.Services;
using EventDrivenShop.OrderService.Application.Validators;
using EventDrivenShop.OrderService.Domain;
using EventDrivenShop.OrderService.Infrastructure.Consumers;
using EventDrivenShop.OrderService.Infrastructure.Persistence;
using EventDrivenShop.PaymentService.Application.Common;
using EventDrivenShop.PaymentService.Application.Services;
using EventDrivenShop.PaymentService.Domain;
using EventDrivenShop.PaymentService.Infrastructure.Consumers;
using EventDrivenShop.PaymentService.Infrastructure.Persistence;
using FluentAssertions;
using FluentValidation;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventDrivenShop.IntegrationTests.Messaging;

public sealed class MassTransitConsumerHarnessIntegrationTests
{
    [Fact]
    public async Task PaymentService_OrderCreatedConsumer_ShouldConsumeEventAndPersistPayment()
    {
        // Arrange
        var services = new ServiceCollection();

        var dbName = $"PaymentsDb_{Guid.NewGuid()}";
        services.AddDbContext<PaymentDbContext>(opts => opts.UseInMemoryDatabase(dbName));
        services.AddScoped<IPaymentDbContext>(sp => sp.GetRequiredService<PaymentDbContext>());
        services.AddSingleton<IPaymentSimulator, PaymentSimulator>();
        services.AddScoped<IPaymentApplicationService, PaymentApplicationService>();

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<OrderCreatedConsumer>();
        });

        var serviceProvider = services.BuildServiceProvider();
        var harness = serviceProvider.GetRequiredService<ITestHarness>();

        await harness.Start();

        try
        {
            var orderId = Guid.NewGuid();
            var orderCreatedEvent = new OrderCreatedIntegrationEvent
            {
                EventId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                CorrelationId = "test-corr-harness-1",
                OrderId = orderId,
                CustomerId = Guid.NewGuid(),
                CustomerEmail = "harness@test.com",
                TotalAmount = 75.00m,
                Currency = "EUR"
            };

            // Act
            await harness.Bus.Publish(orderCreatedEvent);

            // Assert
            var consumerHarness = harness.GetConsumerHarness<OrderCreatedConsumer>();
            (await consumerHarness.Consumed.Any<OrderCreatedIntegrationEvent>()).Should().BeTrue();

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);

            payment.Should().NotBeNull();
            payment!.Status.Should().Be(PaymentStatus.Completed);
            payment.Amount.Should().Be(75.00m);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task OrderService_PaymentCompletedConsumer_ShouldConsumeEventAndMarkOrderPaid()
    {
        // Arrange
        var services = new ServiceCollection();

        var dbName = $"OrdersDb_{Guid.NewGuid()}";
        services.AddDbContext<OrderDbContext>(opts => opts.UseInMemoryDatabase(dbName));
        services.AddScoped<IOrderDbContext>(sp => sp.GetRequiredService<OrderDbContext>());
        services.AddScoped<IOrderApplicationService, OrderApplicationService>();
        services.AddValidatorsFromAssembly(typeof(CreateOrderRequestValidator).Assembly);

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<PaymentCompletedConsumer>();
        });

        var serviceProvider = services.BuildServiceProvider();
        var harness = serviceProvider.GetRequiredService<ITestHarness>();

        await harness.Start();

        try
        {
            // Create order first
            Guid orderId;
            using (var scope = serviceProvider.CreateScope())
            {
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderApplicationService>();
                var order = await orderService.CreateOrderAsync(
                    new CreateOrderRequest(Guid.NewGuid(), "buyer@harness.com", "USD", [new(Guid.NewGuid(), "Item", 1, 100m)]),
                    "test-corr-harness-2");
                orderId = order.Id;
            }

            var paymentCompletedEvent = new PaymentCompletedIntegrationEvent
            {
                EventId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                CorrelationId = "test-corr-harness-2",
                PaymentId = Guid.NewGuid(),
                OrderId = orderId,
                Amount = 100m,
                Currency = "USD"
            };

            // Act
            await harness.Bus.Publish(paymentCompletedEvent);

            // Assert
            var consumerHarness = harness.GetConsumerHarness<PaymentCompletedConsumer>();
            (await consumerHarness.Consumed.Any<PaymentCompletedIntegrationEvent>()).Should().BeTrue();

            using var verifyScope = serviceProvider.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var updatedOrder = await db.Orders.FindAsync(orderId);

            updatedOrder.Should().NotBeNull();
            updatedOrder!.Status.Should().Be(OrderStatus.Paid);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task NotificationService_PaymentCompletedConsumer_ShouldConsumeAndPersistNotification()
    {
        // Arrange
        var services = new ServiceCollection();

        var dbName = $"NotifsDb_{Guid.NewGuid()}";
        services.AddDbContext<NotificationDbContext>(opts => opts.UseInMemoryDatabase(dbName));
        services.AddScoped<INotificationDbContext>(sp => sp.GetRequiredService<NotificationDbContext>());
        services.AddScoped<INotificationApplicationService, NotificationApplicationService>();

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<PaymentCompletedNotificationConsumer>();
        });

        var serviceProvider = services.BuildServiceProvider();
        var harness = serviceProvider.GetRequiredService<ITestHarness>();

        await harness.Start();

        try
        {
            var orderId = Guid.NewGuid();
            var paymentCompletedEvent = new PaymentCompletedIntegrationEvent
            {
                EventId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                CorrelationId = "test-corr-harness-3",
                PaymentId = Guid.NewGuid(),
                OrderId = orderId,
                Amount = 150m,
                Currency = "USD"
            };

            // Act
            await harness.Bus.Publish(paymentCompletedEvent);

            // Assert
            var consumerHarness = harness.GetConsumerHarness<PaymentCompletedNotificationConsumer>();
            (await consumerHarness.Consumed.Any<PaymentCompletedIntegrationEvent>()).Should().BeTrue();

            using var verifyScope = serviceProvider.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var notifications = await db.Notifications.Where(n => n.OrderId == orderId).ToListAsync();

            notifications.Should().HaveCount(1);
            notifications[0].Status.Should().Be(NotificationStatus.Sent);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
