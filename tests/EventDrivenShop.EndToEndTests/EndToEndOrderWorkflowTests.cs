using EventDrivenShop.Contracts;
using EventDrivenShop.NotificationService.Application.Common;
using EventDrivenShop.NotificationService.Application.DTOs;
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
using EventDrivenShop.PaymentService.Application.DTOs;
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

namespace EventDrivenShop.EndToEndTests;

public sealed class EndToEndOrderWorkflowTests
{
    private sealed class TestSystemFixture : IAsyncDisposable
    {
        public ServiceProvider ServiceProvider { get; }
        public ITestHarness Harness { get; }
        public string OrderDbName { get; } = $"OrdersDb_{Guid.NewGuid()}";
        public string PaymentDbName { get; } = $"PaymentsDb_{Guid.NewGuid()}";
        public string NotificationDbName { get; } = $"NotifsDb_{Guid.NewGuid()}";

        public TestSystemFixture()
        {
            var services = new ServiceCollection();

            // 1. Order Service DB & Services
            services.AddDbContext<OrderDbContext>(opts => opts.UseInMemoryDatabase(OrderDbName));
            services.AddScoped<IOrderDbContext>(sp => sp.GetRequiredService<OrderDbContext>());
            services.AddScoped<IOrderApplicationService, OrderApplicationService>();
            services.AddValidatorsFromAssembly(typeof(CreateOrderRequestValidator).Assembly);

            // 2. Payment Service DB & Services
            services.AddDbContext<PaymentDbContext>(opts => opts.UseInMemoryDatabase(PaymentDbName));
            services.AddScoped<IPaymentDbContext>(sp => sp.GetRequiredService<PaymentDbContext>());
            services.AddSingleton<IPaymentSimulator, PaymentSimulator>();
            services.AddScoped<IPaymentApplicationService, PaymentApplicationService>();

            // 3. Notification Service DB & Services
            services.AddDbContext<NotificationDbContext>(opts => opts.UseInMemoryDatabase(NotificationDbName));
            services.AddScoped<INotificationDbContext>(sp => sp.GetRequiredService<NotificationDbContext>());
            services.AddScoped<INotificationApplicationService, NotificationApplicationService>();

            // 4. MassTransit Harness with all consumers configured
            services.AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<OrderCreatedConsumer>();
                cfg.AddConsumer<PaymentCompletedConsumer>();
                cfg.AddConsumer<PaymentFailedConsumer>();
                cfg.AddConsumer<PaymentCompletedNotificationConsumer>();
                cfg.AddConsumer<PaymentFailedNotificationConsumer>();
            });

            ServiceProvider = services.BuildServiceProvider();
            Harness = ServiceProvider.GetRequiredService<ITestHarness>();
        }

        public async Task StartAsync()
        {
            await Harness.Start();
        }

        public async ValueTask DisposeAsync()
        {
            await Harness.Stop();
            await ServiceProvider.DisposeAsync();
        }
    }

    [Fact]
    public async Task CompleteE2EFlow_SuccessfulOrder_TransitionsToPaidAndNotifies()
    {
        // Arrange
        await using var fixture = new TestSystemFixture();
        await fixture.StartAsync();

        var correlationId = $"corr-e2e-success-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var customerEmail = "buyer@example.com";

        // Step 1: Client submits order to Order Service
        OrderResponse createdOrder;
        using (var scope = fixture.ServiceProvider.CreateScope())
        {
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderApplicationService>();
            var request = new CreateOrderRequest(
                customerId,
                customerEmail,
                "USD",
                [
                    new(Guid.NewGuid(), "Mechanical Keyboard", 1, 120.00m),
                    new(Guid.NewGuid(), "Desk Mat", 2, 25.00m)
                ]);

            createdOrder = await orderService.CreateOrderAsync(request, correlationId);
        }

        // Verify initial state is PendingPayment
        createdOrder.Status.Should().Be(OrderStatus.PendingPayment.ToString());
        createdOrder.TotalAmount.Should().Be(170.00m);

        // Step 2: Simulate Outbox message processing -> Publish OrderCreatedIntegrationEvent to bus
        using (var scope = fixture.ServiceProvider.CreateScope())
        {
            var orderDb = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var outboxMsg = await orderDb.OutboxMessages.FirstAsync(m => m.CorrelationId == correlationId);

            var eventObj = new OrderCreatedIntegrationEvent
            {
                EventId = outboxMsg.Id,
                OccurredAtUtc = outboxMsg.OccurredAtUtc,
                CorrelationId = correlationId,
                OrderId = createdOrder.Id,
                CustomerId = customerId,
                CustomerEmail = customerEmail,
                TotalAmount = createdOrder.TotalAmount,
                Currency = "USD"
            };

            await fixture.Harness.Bus.Publish(eventObj);
            outboxMsg.ProcessedAtUtc = DateTime.UtcNow;
            await orderDb.SaveChangesAsync();
        }

        // Step 3: Wait for Payment Service to consume OrderCreatedIntegrationEvent
        var paymentConsumerHarness = fixture.Harness.GetConsumerHarness<OrderCreatedConsumer>();
        await PollUntilAsync(async () => await paymentConsumerHarness.Consumed.Any<OrderCreatedIntegrationEvent>());

        // Step 4: Verify Payment Service created Payment and Outbox message
        Payment? paymentRecord;
        PaymentCompletedIntegrationEvent? paymentCompletedEvent;
        using (var scope = fixture.ServiceProvider.CreateScope())
        {
            var paymentDb = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            paymentRecord = await paymentDb.Payments.FirstOrDefaultAsync(p => p.OrderId == createdOrder.Id);
            paymentRecord.Should().NotBeNull();
            paymentRecord!.Status.Should().Be(PaymentStatus.Completed);
            paymentRecord.Amount.Should().Be(170.00m);

            var paymentOutbox = await paymentDb.OutboxMessages.FirstOrDefaultAsync(m => m.CorrelationId == correlationId);
            paymentOutbox.Should().NotBeNull();

            paymentCompletedEvent = new PaymentCompletedIntegrationEvent
            {
                EventId = paymentOutbox!.Id,
                OccurredAtUtc = DateTime.UtcNow,
                CorrelationId = correlationId,
                PaymentId = paymentRecord.Id,
                OrderId = paymentRecord.OrderId,
                Amount = paymentRecord.Amount,
                Currency = paymentRecord.Currency
            };

            // Simulate Payment Outbox publisher publishing to bus
            await fixture.Harness.Bus.Publish(paymentCompletedEvent);
            paymentOutbox.ProcessedAtUtc = DateTime.UtcNow;
            await paymentDb.SaveChangesAsync();
        }

        // Step 5: Wait for Order Service & Notification Service to consume PaymentCompletedIntegrationEvent
        var orderConsumerHarness = fixture.Harness.GetConsumerHarness<PaymentCompletedConsumer>();
        var notifConsumerHarness = fixture.Harness.GetConsumerHarness<PaymentCompletedNotificationConsumer>();

        await PollUntilAsync(async () =>
            await orderConsumerHarness.Consumed.Any<PaymentCompletedIntegrationEvent>() &&
            await notifConsumerHarness.Consumed.Any<PaymentCompletedIntegrationEvent>());

        // Step 6: Verify Final State of Order is PAID
        using (var scope = fixture.ServiceProvider.CreateScope())
        {
            var orderDb = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var finalOrder = await orderDb.Orders.FindAsync(createdOrder.Id);
            finalOrder.Should().NotBeNull();
            finalOrder!.Status.Should().Be(OrderStatus.Paid);

            // Step 7: Verify Notification record exists
            var notifDb = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var notifs = await notifDb.Notifications.Where(n => n.OrderId == createdOrder.Id).ToListAsync();
            notifs.Should().HaveCount(1);
            notifs[0].Type.Should().Be(NotificationType.PaymentSucceeded);
            notifs[0].Status.Should().Be(NotificationStatus.Sent);
            notifs[0].Content.Should().Contain("170.00 USD");
        }
    }

    [Fact]
    public async Task CompleteE2EFlow_FailedOrder_TransitionsToPaymentFailedAndNotifies()
    {
        // Arrange
        await using var fixture = new TestSystemFixture();
        await fixture.StartAsync();

        var correlationId = $"corr-e2e-fail-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var customerEmail = "fail-payment@example.com"; // Deterministic failure

        // Step 1: Create order
        OrderResponse createdOrder;
        using (var scope = fixture.ServiceProvider.CreateScope())
        {
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderApplicationService>();
            var request = new CreateOrderRequest(
                customerId,
                customerEmail,
                "EUR",
                [new(Guid.NewGuid(), "Gaming Mouse", 1, 80.00m)]);

            createdOrder = await orderService.CreateOrderAsync(request, correlationId);
        }

        // Step 2: Publish OrderCreated
        using (var scope = fixture.ServiceProvider.CreateScope())
        {
            var orderDb = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var outboxMsg = await orderDb.OutboxMessages.FirstAsync(m => m.CorrelationId == correlationId);

            var eventObj = new OrderCreatedIntegrationEvent
            {
                EventId = outboxMsg.Id,
                OccurredAtUtc = outboxMsg.OccurredAtUtc,
                CorrelationId = correlationId,
                OrderId = createdOrder.Id,
                CustomerId = customerId,
                CustomerEmail = customerEmail,
                TotalAmount = 80.00m,
                Currency = "EUR"
            };

            await fixture.Harness.Bus.Publish(eventObj);
            outboxMsg.ProcessedAtUtc = DateTime.UtcNow;
            await orderDb.SaveChangesAsync();
        }

        // Step 3: Wait for Payment Service to process
        var paymentConsumerHarness = fixture.Harness.GetConsumerHarness<OrderCreatedConsumer>();
        await PollUntilAsync(async () => await paymentConsumerHarness.Consumed.Any<OrderCreatedIntegrationEvent>());

        // Step 4: Verify Payment record is FAILED and publish PaymentFailedIntegrationEvent
        using (var scope = fixture.ServiceProvider.CreateScope())
        {
            var paymentDb = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var paymentRecord = await paymentDb.Payments.FirstOrDefaultAsync(p => p.OrderId == createdOrder.Id);
            paymentRecord.Should().NotBeNull();
            paymentRecord!.Status.Should().Be(PaymentStatus.Failed);

            var paymentOutbox = await paymentDb.OutboxMessages.FirstOrDefaultAsync(m => m.CorrelationId == correlationId);
            paymentOutbox.Should().NotBeNull();

            var paymentFailedEvent = new PaymentFailedIntegrationEvent
            {
                EventId = paymentOutbox!.Id,
                OccurredAtUtc = DateTime.UtcNow,
                CorrelationId = correlationId,
                PaymentId = paymentRecord.Id,
                OrderId = paymentRecord.OrderId,
                Amount = paymentRecord.Amount,
                Currency = paymentRecord.Currency,
                Reason = paymentRecord.FailureReason ?? "Payment declined."
            };

            await fixture.Harness.Bus.Publish(paymentFailedEvent);
            paymentOutbox.ProcessedAtUtc = DateTime.UtcNow;
            await paymentDb.SaveChangesAsync();
        }

        // Step 5: Wait for Order Service & Notification Service to consume PaymentFailedIntegrationEvent
        var orderFailedConsumerHarness = fixture.Harness.GetConsumerHarness<PaymentFailedConsumer>();
        var notifFailedConsumerHarness = fixture.Harness.GetConsumerHarness<PaymentFailedNotificationConsumer>();

        await PollUntilAsync(async () =>
            await orderFailedConsumerHarness.Consumed.Any<PaymentFailedIntegrationEvent>() &&
            await notifFailedConsumerHarness.Consumed.Any<PaymentFailedIntegrationEvent>());

        // Step 6: Verify Final State of Order is PaymentFailed
        using (var scope = fixture.ServiceProvider.CreateScope())
        {
            var orderDb = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var finalOrder = await orderDb.Orders.FindAsync(createdOrder.Id);
            finalOrder.Should().NotBeNull();
            finalOrder!.Status.Should().Be(OrderStatus.PaymentFailed);
            finalOrder.FailureReason.Should().NotBeNullOrWhiteSpace();

            // Step 7: Verify Failure Notification record exists
            var notifDb = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var notifs = await notifDb.Notifications.Where(n => n.OrderId == createdOrder.Id).ToListAsync();
            notifs.Should().HaveCount(1);
            notifs[0].Type.Should().Be(NotificationType.PaymentFailed);
            notifs[0].Content.Should().Contain("failed");
        }
    }

    private static async Task PollUntilAsync(Func<Task<bool>> predicate, TimeSpan? timeout = null, TimeSpan? interval = null)
    {
        var maxTime = timeout ?? TimeSpan.FromSeconds(10);
        var delayTime = interval ?? TimeSpan.FromMilliseconds(50);
        var cts = new CancellationTokenSource(maxTime);

        while (!cts.Token.IsCancellationRequested)
        {
            if (await predicate())
            {
                return;
            }

            await Task.Delay(delayTime, cts.Token);
        }

        throw new TimeoutException($"Condition was not met within {maxTime.TotalSeconds} seconds.");
    }
}
