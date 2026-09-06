using EventDrivenShop.NotificationService.Application.Common;
using EventDrivenShop.NotificationService.Application.Services;
using EventDrivenShop.NotificationService.Infrastructure.Consumers;
using EventDrivenShop.NotificationService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventDrivenShop.NotificationService.Infrastructure;

public static class NotificationInfrastructureExtensions
{
    public static IServiceCollection AddNotificationService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Database Configuration
        var connectionString = configuration.GetConnectionString("NotificationsDb") ??
            configuration["Database:ConnectionString"] ??
            "Host=localhost;Port=5432;Database=notifications_db;Username=postgres;Password=postgres";

        services.AddDbContext<NotificationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(NotificationDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            });
        });

        services.AddScoped<INotificationDbContext>(sp => sp.GetRequiredService<NotificationDbContext>());

        // 2. Application Services
        services.AddScoped<INotificationApplicationService, NotificationApplicationService>();

        // 3. MassTransit Messaging with RabbitMQ
        services.AddMassTransit(busConfig =>
        {
            busConfig.SetKebabCaseEndpointNameFormatter();
            busConfig.AddConsumer<PaymentCompletedNotificationConsumer>();
            busConfig.AddConsumer<PaymentFailedNotificationConsumer>();

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

                cfg.ReceiveEndpoint("notification-service.payment-completed", e =>
                {
                    e.ConfigureConsumer<PaymentCompletedNotificationConsumer>(context);
                });

                cfg.ReceiveEndpoint("notification-service.payment-failed", e =>
                {
                    e.ConfigureConsumer<PaymentFailedNotificationConsumer>(context);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
