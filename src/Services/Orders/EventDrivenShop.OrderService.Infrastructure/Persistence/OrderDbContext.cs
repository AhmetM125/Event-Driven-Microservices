using EventDrivenShop.Common.Inbox;
using EventDrivenShop.Common.Outbox;
using EventDrivenShop.OrderService.Application.Common;
using EventDrivenShop.OrderService.Domain;
using Microsoft.EntityFrameworkCore;

namespace EventDrivenShop.OrderService.Infrastructure.Persistence;

public class OrderDbContext : DbContext, IOrderDbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Order Configuration
        modelBuilder.Entity<Order>(builder =>
        {
            builder.ToTable("Orders");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.CustomerEmail)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(o => o.Currency)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(o => o.TotalAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(o => o.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(o => o.FailureReason)
                .HasMaxLength(500);

            builder.Property(o => o.CreatedAtUtc)
                .IsRequired();

            builder.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(o => o.CustomerId);
            builder.HasIndex(o => o.CreatedAtUtc);
            builder.HasIndex(o => o.Status);
        });

        // OrderItem Configuration
        modelBuilder.Entity<OrderItem>(builder =>
        {
            builder.ToTable("OrderItems");
            builder.HasKey(i => i.Id);

            builder.Property(i => i.ProductName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(i => i.UnitPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Ignore(i => i.TotalPrice);
        });

        // Outbox Configuration
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(m => m.Id);

            builder.Property(m => m.EventType)
                .IsRequired()
                .HasMaxLength(250);

            builder.Property(m => m.Payload)
                .IsRequired()
                .HasColumnType("text");

            builder.Property(m => m.CorrelationId)
                .HasMaxLength(100);

            builder.Property(m => m.LastError)
                .HasMaxLength(2000);

            builder.HasIndex(m => new { m.ProcessedAtUtc, m.OccurredAtUtc });
        });

        // Inbox Configuration
        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("InboxMessages");
            builder.HasKey(m => new { m.EventId, m.Consumer });

            builder.Property(m => m.Consumer)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(m => m.ProcessedAtUtc)
                .IsRequired();
        });
    }
}
