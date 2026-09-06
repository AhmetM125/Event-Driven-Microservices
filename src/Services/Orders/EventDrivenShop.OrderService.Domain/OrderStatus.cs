namespace EventDrivenShop.OrderService.Domain;

public enum OrderStatus
{
    PendingPayment = 1,
    Paid = 2,
    PaymentFailed = 3
}
