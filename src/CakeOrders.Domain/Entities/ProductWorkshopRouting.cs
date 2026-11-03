namespace CakeOrders.Domain.Entities;

/// <summary>Default production workshop for a product at a specific branch.</summary>
public sealed class ProductWorkshopRouting
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public long WorkshopId { get; set; }

    public Workshop Workshop { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
