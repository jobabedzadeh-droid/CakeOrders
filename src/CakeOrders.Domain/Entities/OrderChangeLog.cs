using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Domain.Entities;

public sealed class OrderChangeLog
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public Order Order { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string FieldName { get; set; } = null!;

    [MaxLength(2000)]
    public string? OldValue { get; set; }

    [MaxLength(2000)]
    public string? NewValue { get; set; }

    public DateTime ChangedAtUtc { get; set; }

    [MaxLength(450)]
    public string? ChangedByUserId { get; set; }
}