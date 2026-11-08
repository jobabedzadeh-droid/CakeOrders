using System.Data;
using System.Security.Claims;
using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Identity;
using CakeOrders.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

/// <summary>
/// Workshop task decisions. The workshop boundary is enforced in every action,
/// not by filtering the UI alone.
/// </summary>
[Authorize(Roles = "Admin,User")]
[Route("[controller]/[action]")]
public sealed class WorkshopPanelController(
    CakeOrdersDbContext db,
    IUserNotificationService notifications) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(long taskId, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorId))
            return Challenge();

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var task = await db.ProductionTasks
            .Include(x => x.Workshop)
            .Include(x => x.Order)
                .ThenInclude(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);

        if (task is null)
            return NotFound();

        if (!await CanManageWorkshopAsync(actorId, task.WorkshopId, cancellationToken))
            return Forbid();

        if (task.Status != ProductionTaskStatus.AwaitingWorkshopReview)
        {
            TempData["Error"] = "این وظیفه در انتظار بررسی کارگاه نیست.";
            return RedirectToAction(nameof(Index));
        }

        var now = DateTime.UtcNow;
        task.Status = ProductionTaskStatus.InProgress;
        task.UpdatedAtUtc = now;
        task.UpdatedByUserId = actorId;
        task.History.Add(new ProductionTaskHistory
        {
            PreviousStatus = ProductionTaskStatus.AwaitingWorkshopReview,
            NewStatus = ProductionTaskStatus.InProgress,
            ChangedAtUtc = now,
            ChangedByUserId = actorId,
            Note = "وظیفه تولید توسط مسئول کارگاه پذیرفته و تولید آغاز شد."
        });

        var orderPreviousStatus = task.Order.Status;
        task.Order.Status = OrderStatus.InProduction;
        task.Order.UpdatedByUserId = actorId;
        task.Order.StatusHistory.Add(new OrderStatusHistory
        {
            PreviousStatus = orderPreviousStatus,
            NewStatus = OrderStatus.InProduction,
            ChangedAtUtc = now,
            ChangedByUserId = actorId,
            Note = "سفارش توسط مسئول کارگاه پذیرفته شد و تولید آغاز گردید."
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["Success"] = "وظیفه پذیرفته شد و وضعیت سفارش به «در حال تولید» تغییر کرد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult StartProduction(long taskId) => BadRequest("شروع تولید هم‌زمان با پذیرش وظیفه ثبت می‌شود.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CompleteProduction(long taskId, CancellationToken cancellationToken) =>
        ChangeTaskStatusAsync(
            taskId,
            ProductionTaskStatus.InProgress,
            ProductionTaskStatus.Completed,
            "اتمام تولید توسط مسئول کارگاه ثبت شد.",
            "تکمیل تولید با موفقیت ثبت شد.",
            cancellationToken);

    private async Task<IActionResult> ChangeTaskStatusAsync(
        long taskId,
        ProductionTaskStatus expectedStatus,
        ProductionTaskStatus newStatus,
        string historyNote,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorId))
            return Challenge();

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var task = await db.ProductionTasks
            .Include(x => x.History)
            .Include(x => x.Order)
                .ThenInclude(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);

        if (task is null)
            return NotFound();

        if (!await CanManageWorkshopAsync(actorId, task.WorkshopId, cancellationToken))
            return Forbid();

        if (task.Status != expectedStatus)
        {
            TempData["Error"] = "وضعیت فعلی وظیفه با این عملیات سازگار نیست.";
            return RedirectToAction(nameof(Index));
        }

        var now = DateTime.UtcNow;
        task.Status = newStatus;
        task.UpdatedAtUtc = now;
        task.UpdatedByUserId = actorId;
        task.History.Add(new ProductionTaskHistory
        {
            PreviousStatus = expectedStatus,
            NewStatus = newStatus,
            ChangedAtUtc = now,
            ChangedByUserId = actorId,
            Note = historyNote
        });

        if (newStatus == ProductionTaskStatus.Completed)
        {
            var nextOrderStatus = task.Order.RemainingAmount <= 0
                ? OrderStatus.Delivered
                : OrderStatus.ReadyForDelivery;
            var orderNote = nextOrderStatus == OrderStatus.Delivered
                ? "تولید تکمیل شد و به‌دلیل تسویه کامل، سفارش به‌صورت خودکار تحویل‌شده ثبت شد."
                : "اتمام تولید توسط مسئول کارگاه ثبت شد؛ سفارش آماده تحویل است.";

            var previousOrderStatus = task.Order.Status;
            task.Order.Status = nextOrderStatus;
            task.Order.UpdatedByUserId = actorId;
            task.Order.StatusHistory.Add(new OrderStatusHistory
            {
                PreviousStatus = previousOrderStatus,
                NewStatus = nextOrderStatus,
                ChangedAtUtc = now,
                ChangedByUserId = actorId,
                Note = orderNote
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["Success"] = successMessage;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(
        long taskId,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "ثبت دلیل رد وظیفه الزامی است.";
            return RedirectToAction(nameof(Index));
        }

        reason = reason.Trim();
        if (reason.Length > 1000)
        {
            TempData["Error"] = "دلیل رد نمی‌تواند بیشتر از ۱۰۰۰ نویسه باشد.";
            return RedirectToAction(nameof(Index));
        }

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorId))
            return Challenge();

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var task = await db.ProductionTasks
            .Include(x => x.Order)
            .Include(x => x.Items)
            .Include(x => x.History)
            .Include(x => x.Workshop)
            .FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);

        if (task is null)
            return NotFound();

        if (!await CanManageWorkshopAsync(actorId, task.WorkshopId, cancellationToken))
            return Forbid();

        if (task.Status != ProductionTaskStatus.AwaitingWorkshopReview)
        {
            TempData["Error"] = "این وظیفه در انتظار بررسی کارگاه نیست.";
            return RedirectToAction(nameof(Index));
        }

        var now = DateTime.UtcNow;
        task.Status = ProductionTaskStatus.Rejected;
        task.UpdatedAtUtc = now;
        task.UpdatedByUserId = actorId;
        task.History.Add(new ProductionTaskHistory
        {
            PreviousStatus = ProductionTaskStatus.AwaitingWorkshopReview,
            NewStatus = ProductionTaskStatus.Rejected,
            ChangedAtUtc = now,
            ChangedByUserId = actorId,
            Note = reason
        });

        await notifications.NotifyWorkshopTaskRejectedAsync(
            task.OrderId,
            task.Order.OrderNumber,
            task.Workshop.Name,
            reason,
            task.Order.CreatedByUserId,
            cancellationToken);

        // The task history, status, and recipient notifications are persisted together.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["Success"] = "رد وظیفه ثبت شد و اعلان برای مدیران و اپراتور ارسال شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = db.ProductionTasks
            .AsNoTracking()
            .Include(x => x.Order)
            .Include(x => x.Workshop)
                .ThenInclude(x => x.Branch)
            .AsQueryable();

        if (!User.IsInRole("Admin"))
        {
            var membership = await db.Users
                .AsNoTracking()
                .Where(x => x.Id == actorId && x.IsActive)
                .Select(x => new { x.WorkshopId, x.WorkshopRole })
                .SingleOrDefaultAsync(cancellationToken);

            if (membership?.WorkshopId is not long workshopId ||
                membership.WorkshopRole is null)
                return Forbid();

            query = query.Where(x => x.WorkshopId == workshopId);
        }

        var tasks = await query
            .OrderBy(x => x.Status)
            .ThenBy(x => x.Order.DeliveryAt)
            .ToListAsync(cancellationToken);

        return View(tasks);
    }

    private async Task<bool> CanManageWorkshopAsync(
        string userId,
        long workshopId,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin"))
            return true;

        return await db.Users
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == userId &&
                x.IsActive &&
                x.WorkshopId == workshopId &&
                x.WorkshopRole == WorkshopMemberRole.Manager,
                cancellationToken);
    }
}
