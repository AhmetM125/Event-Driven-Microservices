using EventDrivenShop.Common.Inbox;
using EventDrivenShop.Common.Outbox;
using EventDrivenShop.PaymentService.Application.Common;
using EventDrivenShop.PaymentService.Domain;
using Microsoft.EntityFrameworkCore;

namespace EventDrivenShop.PaymentService.Infrastructure.Persistence;

public class PaymentDbContext : DbContext, IPaymentDbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Payment Configuration
        modelBuilder.Entity<Payment>(builder =>
        {
            builder.ToTable("Payments");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.OrderId)
                .IsRequired();

            // CRITICAL: Unique index on OrderId enforces single payment per order constraint at DB level
            builder.HasIndex(p => p.OrderId)
                .IsUnique();

            builder.Property(p => p.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(p => p.Currency)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(p => p.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(p => p.FailureReason)
                .HasMaxLength(500);

            builder.Property(p => p.CreatedAtUtc)
                .IsRequired();

            builder.HasIndex(p => p.CreatedAtUtc);
            builder.HasIndex(p => p.Status);
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
