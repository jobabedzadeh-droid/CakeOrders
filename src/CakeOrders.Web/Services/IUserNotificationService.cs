namespace CakeOrders.Web.Services;

public interface IUserNotificationService
{
    Task NotifyWorkshopTaskRejectedAsync(
        long orderId,
        string orderNumber,
        string workshopName,
        string rejectionReason,
        string? operatorUserId,
        CancellationToken cancellationToken = default);
}
