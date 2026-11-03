namespace CakeOrders.Domain.Entities;

public enum ProductionTaskStatus
{
    // A newly routed task must be reviewed by the assigned workshop manager.
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    AwaitingWorkshopReview = 5,
    Rejected = 6
}

public sealed class ProductionTask
{
    public long Id { get; set; }

    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public long WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = null!;

    public ProductionTaskStatus Status { get; set; } = ProductionTaskStatus.AwaitingWorkshopReview;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedByUserId { get; set; }

    public ICollection<ProductionTaskItem> Items { get; set; } = new List<ProductionTaskItem>();
    public ICollection<ProductionTaskHistory> History { get; set; } = new List<ProductionTaskHistory>();
}
