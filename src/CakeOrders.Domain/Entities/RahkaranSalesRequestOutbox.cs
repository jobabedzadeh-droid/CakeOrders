namespace CakeOrders.Domain.Entities;

public enum RahkaranSalesRequestSyncStatus
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    NeedsReview = 4
}

/// <summary>
/// Durable queue entry created in the same database transaction that confirms an order.
/// The remote Rahkaran call is intentionally performed later, outside the order transaction.
/// </summary>
public sealed class RahkaranSalesRequestOutbox
{
    public long Id { get; set; }

    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public RahkaranSalesRequestSyncStatus Status { get; set; }
        = RahkaranSalesRequestSyncStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? NextAttemptAtUtc { get; set; }
    public DateTime? ProcessingStartedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public string? RemoteSalesRequestId { get; set; }
    public string? RemoteSalesRequestNumber { get; set; }
    public string? LastError { get; set; }
}
