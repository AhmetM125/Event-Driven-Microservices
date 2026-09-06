using EventDrivenShop.PaymentService.Application.Common;
using EventDrivenShop.PaymentService.Application.Services;
using EventDrivenShop.PaymentService.Infrastructure.BackgroundJobs;
using EventDrivenShop.PaymentService.Infrastructure.Consumers;
using EventDrivenShop.PaymentService.Infrastructure.Persistence;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventDrivenShop.PaymentService.Infrastructure;

public static class PaymentInfrastructureExtensions
{
    public static IServiceCollection AddPaymentService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Database Configuration
        var connectionString = configuration.GetConnectionString("PaymentsDb") ??
            configuration["Database:ConnectionString"] ??
            "Host=localhost;Port=5432;Database=payments_db;Username=postgres;Password=postgres";

        services.AddDbContext<PaymentDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(PaymentDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            });
        });

        services.AddScoped<IPaymentDbContext>(sp => sp.GetRequiredService<PaymentDbContext>());

        // 2. Application Services
        services.AddSingleton<IPaymentSimulator, PaymentSimulator>();
        services.AddScoped<IPaymentApplicationService, PaymentApplicationService>();

        // 3. MassTransit Messaging with RabbitMQ
        services.AddMassTransit(busConfig =>
        {
            busConfig.SetKebabCaseEndpointNameFormatter();
            busConfig.AddConsumer<OrderCreatedConsumer>();

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

                cfg.UseMessageRetry(r =>
                {
                    r.Incremental(3, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500));
                    r.Ignore<ArgumentException>();
                });

                cfg.ReceiveEndpoint("payment-service.order-created", e =>
                {
                    e.ConfigureConsumer<OrderCreatedConsumer>(context);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        // 4. Background Outbox Processor
        services.AddHostedService<PaymentOutboxProcessor>();

        return services;
    }
}
