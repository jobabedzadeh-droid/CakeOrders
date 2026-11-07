namespace CakeOrders.Domain.Entities;

public sealed class Customer
{
    public long Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    // Canonical Iranian mobile number (11 digits, starting with 09).
    public string Phone { get; set; } = string.Empty;

    // Used for reliable lookup and unique enforcement.
    public string NormalizedPhone { get; set; } = string.Empty;

    // Populated when this customer is matched or created in Rahkaran.
    public string? RahkaranCustomerId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();

    public string FullName => (FirstName + " " + LastName).Trim();
}
