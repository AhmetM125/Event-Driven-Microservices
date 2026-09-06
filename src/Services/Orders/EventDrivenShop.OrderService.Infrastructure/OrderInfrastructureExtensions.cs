using EventDrivenShop.OrderService.Application.Common;
using EventDrivenShop.OrderService.Application.Services;
using EventDrivenShop.OrderService.Infrastructure.BackgroundJobs;
using EventDrivenShop.OrderService.Infrastructure.Consumers;
using EventDrivenShop.OrderService.Infrastructure.Persistence;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventDrivenShop.OrderService.Infrastructure;

public static class OrderInfrastructureExtensions
{
    public static IServiceCollection AddOrderService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Database Configuration
        var connectionString = configuration.GetConnectionString("OrdersDb") ??
            configuration["Database:ConnectionString"] ??
            "Host=localhost;Port=5432;Database=orders_db;Username=postgres;Password=postgres";

        services.AddDbContext<OrderDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(OrderDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            });
        });

        services.AddScoped<IOrderDbContext>(sp => sp.GetRequiredService<OrderDbContext>());

        // 2. Application Services & Validation
        services.AddScoped<IOrderApplicationService, OrderApplicationService>();
        services.AddValidatorsFromAssembly(typeof(IOrderApplicationService).Assembly);

        // 3. MassTransit Messaging with RabbitMQ
        services.AddMassTransit(busConfig =>
        {
            busConfig.SetKebabCaseEndpointNameFormatter();

            busConfig.AddConsumer<PaymentCompletedConsumer>();
            busConfig.AddConsumer<PaymentFailedConsumer>();

            var rabbitHost = configuration["RabbitMQ:Host"] ?? "localhost";
            var rabbitUser = configuration["RabbitMQ:Username"] ?? "guest";
            var rabbitPass = configuration["RabbitMQ:Password"] ?? "guest";
            var rabbitPort = ushort.TryParse(configuration["RabbitMQ:Port"], out var port) ? port : (ushort)5672;

            busConfig.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitHost, rabbitPort, "/", h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                // Configure retries & dead-letter queue behavior
                cfg.UseMessageRetry(r =>
                {
                    r.Incremental(3, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500));
                    r.Ignore<ArgumentException>();
                    r.Ignore<ValidationException>();
                });

                cfg.ReceiveEndpoint("order-service.payment-completed", e =>
                {
                    e.ConfigureConsumer<PaymentCompletedConsumer>(context);
                });

                cfg.ReceiveEndpoint("order-service.payment-failed", e =>
                {
                    e.ConfigureConsumer<PaymentFailedConsumer>(context);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        // 4. Background Outbox Processor
        services.AddHostedService<OrderOutboxProcessor>();

        return services;
    }
}
