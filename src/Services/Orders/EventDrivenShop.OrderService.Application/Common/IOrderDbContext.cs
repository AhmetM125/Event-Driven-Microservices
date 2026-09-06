using EventDrivenShop.Common.Inbox;
using EventDrivenShop.Common.Outbox;
using EventDrivenShop.OrderService.Domain;
using Microsoft.EntityFrameworkCore;

namespace EventDrivenShop.OrderService.Application.Common;

public interface IOrderDbContext : IOutboxDbContext
{
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<InboxMessage> InboxMessages { get; }
}
