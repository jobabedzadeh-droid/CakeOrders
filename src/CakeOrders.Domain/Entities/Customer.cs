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

    // Rahkaran identifiers are deliberately stored separately: CustomerID is used by
    // sales requests, PartyID identifies the shared party, and DLCode is used by finance.
    public string? RahkaranCustomerId { get; set; }
    public long? RahkaranPartyId { get; set; }
    public string? RahkaranCustomerNumber { get; set; }
    public string? RahkaranSecondCode { get; set; }
    public string? RahkaranDLCode { get; set; }

    public string? RahkaranPartyName { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public string? Details { get; set; }
    public string? NationalId { get; set; }
    public string? EconomicCode { get; set; }
    public string? RegistrationCode { get; set; }
    public string? WebsiteUrl { get; set; }
    public int? RahkaranStateCode { get; set; }
    public string? RahkaranStateTitle { get; set; }
    public int? RahkaranTypeCode { get; set; }
    public string? RahkaranTypeTitle { get; set; }
    public int? RahkaranStatus { get; set; }
    public int? RahkaranPartyType { get; set; }
    public DateTime? RahkaranCreatedAt { get; set; }
    public DateTime? RahkaranLastModifiedAt { get; set; }

    // Preserve complete source payload, including Addresses and fields added by Rahkaran,
    // so synchronization never silently discards undocumented fields.
    public string? RahkaranRawDataJson { get; set; }
    public DateTime? LastRahkaranSyncAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();

    public string FullName => (FirstName + " " + LastName).Trim();
}
