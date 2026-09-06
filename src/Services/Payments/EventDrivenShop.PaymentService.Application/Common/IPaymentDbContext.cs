using EventDrivenShop.Common.Inbox;
using EventDrivenShop.Common.Outbox;
using EventDrivenShop.PaymentService.Domain;
using Microsoft.EntityFrameworkCore;

namespace EventDrivenShop.PaymentService.Application.Common;

public interface IPaymentDbContext : IOutboxDbContext
{
    DbSet<Payment> Payments { get; }
    DbSet<InboxMessage> InboxMessages { get; }
}
