using EventDrivenShop.Common.Inbox;
using EventDrivenShop.NotificationService.Application.Common;
using EventDrivenShop.NotificationService.Domain;
using Microsoft.EntityFrameworkCore;

namespace EventDrivenShop.NotificationService.Infrastructure.Persistence;

public class NotificationDbContext : DbContext, INotificationDbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Notification Configuration
        modelBuilder.Entity<Notification>(builder =>
        {
            builder.ToTable("Notifications");
            builder.HasKey(n => n.Id);

            builder.Property(n => n.OrderId)
                .IsRequired();

            builder.Property(n => n.Recipient)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(n => n.Type)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(n => n.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(n => n.Content)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(n => n.CreatedAtUtc)
                .IsRequired();

            builder.HasIndex(n => n.OrderId);
            builder.HasIndex(n => n.CreatedAtUtc);
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
