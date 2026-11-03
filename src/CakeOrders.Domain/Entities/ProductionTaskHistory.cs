namespace CakeOrders.Domain.Entities;

public sealed class ProductionTaskHistory
{
    public long Id { get; set; }

    public long ProductionTaskId { get; set; }
    public ProductionTask ProductionTask { get; set; } = null!;

    public ProductionTaskStatus? PreviousStatus { get; set; }
    public ProductionTaskStatus NewStatus { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string? ChangedByUserId { get; set; }
    public string? Note { get; set; }
}
