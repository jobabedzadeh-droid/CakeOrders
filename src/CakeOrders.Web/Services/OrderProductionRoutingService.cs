using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Services;

public sealed class OrderProductionRoutingService(CakeOrdersDbContext db)
    : IOrderProductionRoutingService
{
    public async Task<bool> TryRouteBirthdayCakeAsync(
        Order order,
        string? actorUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (order.Kind != OrderKind.BirthdayCake)
            return true;

        var hasRequiredDeposit = order.RequestedDepositAmount > 0 &&
            order.DepositAmount >= order.RequestedDepositAmount;
        var hasZeroDepositReferral = order.RequestedDepositAmount == 0 &&
            !string.IsNullOrWhiteSpace(order.ReferrerCode) &&
            await db.Referrers.AnyAsync(x => x.IsActive && x.Code == order.ReferrerCode.Trim(), cancellationToken);

        if (!hasRequiredDeposit && !hasZeroDepositReferral)
            return false;

        if (!await EnsureBirthdayCakeTaskAsync(order, actorUserId, now, cancellationToken))
            return false;

        var taskExists = await db.ProductionTasks.AnyAsync(x => x.OrderId == order.Id, cancellationToken);
        if (taskExists && order.Status is OrderStatus.PendingReview or OrderStatus.Confirmed)
        {
            ChangeOrderStatus(
                order,
                OrderStatus.SentToWorkshop,
                actorUserId,
                now,
                hasZeroDepositReferral
                    ? "ارجاع خودکار به کارگاه به‌دلیل ثبت کد معرف معتبر برای سفارش بدون بیعانه."
                    : "ارجاع خودکار به کارگاه پس از تکمیل پرداخت بیعانه.");
        }

        return true;
    }

    public void ChangeOrderStatus(
        Order order,
        OrderStatus newStatus,
        string? actorUserId,
        DateTime now,
        string note)
    {
        if (order.Status == newStatus)
            return;

        var previousStatus = order.Status;
        order.Status = newStatus;
        order.UpdatedByUserId = actorUserId;
        order.StatusHistory.Add(new OrderStatusHistory
        {
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedAtUtc = now,
            ChangedByUserId = actorUserId,
            Note = note
        });
    }

    public async Task<bool> EnsureBirthdayCakeTaskAsync(
        Order order,
        string? actorUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (order.Kind != OrderKind.BirthdayCake)
            return true;

        if (order.ProductionBranchId is not long branchId)
            return false;

        var workshopId = await db.Workshops
            .Where(x => x.BranchId == branchId && x.IsActive && x.Name == "کیک‌سازی")
            .Select(x => (long?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!workshopId.HasValue)
            return false;

        var task = await db.ProductionTasks
            .Include(x => x.Items)
            .Include(x => x.History)
            .Where(x => x.OrderId == order.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // Completed and all other non-rejected tasks are terminal/current and must
        // never be reset by a payment callback.
        if (task is not null && task.Status != ProductionTaskStatus.Rejected)
            return true;

        var cakeTypeText = order.CakeType switch
        {
            CakeType.Cream => "خامه‌ای",
            CakeType.SemiFondant => "نیمه فوندانت",
            CakeType.FullFondant => "تمام فوندانت",
            _ => "نامشخص"
        };

        var notes = string.Join(" | ", new[]
        {
            $"نوع سفارش: {order.OrderType}",
            $"نوع کیک: {cakeTypeText}",
            $"وزن درخواستی: {order.RequestedWeightKg:0.##} کیلوگرم",
            string.IsNullOrWhiteSpace(order.BackgroundColor) ? null : $"رنگ زمینه: {order.BackgroundColor}",
            string.IsNullOrWhiteSpace(order.MoldType) ? null : $"قالب: {order.MoldType}",
            string.IsNullOrWhiteSpace(order.PrintType) ? null : $"نوع چاپ: {order.PrintType}",
            order.HasBananaFilling ? "فیلینگ موز" : null,
            order.HasWalnutFilling ? "فیلینگ گردو" : null,
            string.IsNullOrWhiteSpace(order.CakeText) ? null : $"متن روی کیک: {order.CakeText}",
            string.IsNullOrWhiteSpace(order.DesignNotes) ? null : $"توضیحات طرح: {order.DesignNotes}"
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var isNew = task is null;
        if (isNew)
        {
            task = new ProductionTask
            {
                OrderId = order.Id,
                BranchId = branchId,
                WorkshopId = workshopId.Value,
                Status = ProductionTaskStatus.AwaitingWorkshopReview,
                CreatedAtUtc = now,
                CreatedByUserId = actorUserId
            };
            db.ProductionTasks.Add(task);
        }
        else
        {
            task!.Status = ProductionTaskStatus.AwaitingWorkshopReview;
            task.BranchId = branchId;
            task.WorkshopId = workshopId.Value;
            task.UpdatedAtUtc = now;
            task.UpdatedByUserId = actorUserId;
            task.Items.Clear();
        }

        task!.Items.Add(new ProductionTaskItem
        {
            ProductName = "کیک تولد",
            Unit = ProductUnit.Piece,
            Quantity = 1,
            Notes = notes
        });

        task.History.Add(new ProductionTaskHistory
        {
            PreviousStatus = isNew ? null : ProductionTaskStatus.Rejected,
            NewStatus = ProductionTaskStatus.AwaitingWorkshopReview,
            ChangedAtUtc = now,
            ChangedByUserId = actorUserId,
            Note = isNew
                ? "وظیفه تولید پس از تکمیل بیعانه ایجاد و برای بررسی به کارگاه ارجاع شد."
                : "وظیفه تولید ردشده پس از تکمیل بیعانه مجدداً برای بررسی ارجاع شد."
        });

        return true;
    }
}
