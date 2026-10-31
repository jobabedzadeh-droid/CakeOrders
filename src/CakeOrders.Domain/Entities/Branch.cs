namespace CakeOrders.Domain.Entities;

public sealed class Branch
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CashRegister> CashRegisters { get; set; } = new List<CashRegister>();
}
