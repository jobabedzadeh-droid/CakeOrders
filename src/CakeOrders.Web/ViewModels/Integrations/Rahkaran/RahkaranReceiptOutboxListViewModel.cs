using CakeOrders.Domain.Entities;

namespace CakeOrders.Web.ViewModels.Integrations.Rahkaran;

public sealed class RahkaranReceiptOutboxListViewModel
{
    public IReadOnlyList<RahkaranReceiptOutboxRowViewModel> Items { get; init; }
        = Array.Empty<RahkaranReceiptOutboxRowViewModel>();
}

public sealed class RahkaranReceiptOutboxRowViewModel
{
    public long Id { get; init; }
    public long OrderId { get; init; }
    public string OrderNumber { get; init; } = "";
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public RahkaranReceiptSource Source { get; init; }
    public RahkaranReceiptSyncStatus Status { get; init; }
    public decimal Amount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public string? RemoteReceiptNumber { get; init; }
    public string? LastError { get; init; }
}
