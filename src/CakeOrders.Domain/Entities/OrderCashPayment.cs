using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Domain.Entities;

/// <summary>
/// Records money physically received by an operator, independently of POS transactions.
/// A cash receipt is only created after the operator confirms that the money was received.
/// </summary>
public sealed class OrderCashPayment
{
    public long Id { get; set; }

    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public decimal Amount { get; set; }

    public PaymentPurpose Purpose { get; set; }

    [Required]
    [MaxLength(450)]
    public string ReceivedByUserId { get; set; } = string.Empty;

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(100)]
    public string? ReceiptReference { get; set; }

    [Required]
    [MaxLength(100)]
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
}
