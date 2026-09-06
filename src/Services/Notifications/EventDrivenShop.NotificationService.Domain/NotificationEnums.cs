namespace EventDrivenShop.NotificationService.Domain;

public enum NotificationType
{
    PaymentSucceeded = 1,
    PaymentFailed = 2
}

public enum NotificationStatus
{
    Sent = 1,
    Failed = 2
}
