namespace CakeOrders.Domain.Entities;

public enum RahkaranReceiptSource
{
    Cash = 1,
    PointOfSale = 2
}

public enum RahkaranReceiptSyncStatus
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    NeedsReview = 4
}

/// <summary>
/// One durable Rahkaran receipt operation per successful deposit payment.
/// A source payment is never automatically posted twice after an ambiguous result.
/// </summary>
public sealed class RahkaranReceiptOutbox
{
    public long Id { get; set; }

    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public long? OrderCashPaymentId { get; set; }
    public long? PaymentTransactionId { get; set; }

    public RahkaranReceiptSource Source { get; set; }

    // Existing rows default to Deposit for backward-compatible reconciliation.
    public PaymentPurpose Purpose { get; set; } = PaymentPurpose.Deposit;
    public RahkaranReceiptSyncStatus Status { get; set; } = RahkaranReceiptSyncStatus.Pending;

    public decimal Amount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessingStartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public string? RemoteReceiptId { get; set; }
    public string? RemoteReceiptNumber { get; set; }
    public string? LastError { get; set; }
}
