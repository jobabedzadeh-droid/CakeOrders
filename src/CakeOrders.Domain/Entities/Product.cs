namespace CakeOrders.Domain.Entities;

public enum ProductUnit
{
    Piece = 1,
    Kilogram = 2,
    Tray = 3,
    Box = 4
}

public sealed class Product
{
    public long Id { get; set; }

    public long CategoryId { get; set; }

    public ProductCategory Category { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ProductUnit Unit { get; set; } = ProductUnit.Piece;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ProductWorkshopRouting> WorkshopRoutings { get; set; } = new List<ProductWorkshopRouting>();
}
