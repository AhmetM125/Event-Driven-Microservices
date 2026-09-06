using EventDrivenShop.Common.Inbox;
using EventDrivenShop.NotificationService.Domain;
using Microsoft.EntityFrameworkCore;

namespace EventDrivenShop.NotificationService.Application.Common;

public interface INotificationDbContext
{
    DbSet<Notification> Notifications { get; }
    DbSet<InboxMessage> InboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
