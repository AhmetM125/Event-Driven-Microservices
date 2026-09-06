using EventDrivenShop.Common.Exceptions;
using EventDrivenShop.NotificationService.Infrastructure;
using EventDrivenShop.NotificationService.Infrastructure.Persistence;
using EventDrivenShop.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog & Observability
builder.ConfigureSerilog("NotificationService");
builder.Services.AddAppObservability(builder.Configuration, "NotificationService");

// 2. Exception Handling & Controllers
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "EventDrivenShop - Notification Service API",
        Version = "v1",
        Description = "Notification dispatch simulation, querying, and inbox event consumption."
    });
});

// 3. Application & Infrastructure Services
builder.Services.AddNotificationService(builder.Configuration);

// 4. Health Checks
var dbConn = builder.Configuration.GetConnectionString("NotificationsDb") ??
             builder.Configuration["Database:ConnectionString"] ??
             "Host=localhost;Port=5432;Database=notifications_db;Username=postgres;Password=postgres";

var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitPort = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var rp) ? rp : 5672;
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

builder.Services.AddHealthChecks()
    .AddNpgSql(dbConn, name: "notifications-postgres", tags: ["ready"])
    .AddRabbitMQ(
        sp => new ConnectionFactory
        {
            HostName = rabbitHost,
            Port = rabbitPort,
            UserName = rabbitUser,
            Password = rabbitPass
        }.CreateConnectionAsync(),
        name: "notifications-rabbitmq",
        tags: ["ready"]);

var app = builder.Build();

// 5. Database Auto-migration for Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service API v1");
    });

    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not apply database migrations on startup. Ensure PostgreSQL is running.");
    }
}

app.UseExceptionHandler();
app.UseCorrelationId();

app.UseRouting();
app.MapControllers();

// 6. Health check endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

await app.RunAsync();

public partial class Program { }
