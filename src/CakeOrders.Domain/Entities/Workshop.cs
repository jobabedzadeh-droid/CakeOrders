namespace CakeOrders.Domain.Entities;

/// <summary>A production workshop belonging to a specific branch.</summary>
public sealed class Workshop
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
