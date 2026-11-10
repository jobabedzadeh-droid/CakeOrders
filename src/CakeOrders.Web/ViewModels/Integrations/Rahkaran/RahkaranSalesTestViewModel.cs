namespace CakeOrders.Web.ViewModels.Integrations.Rahkaran;

public sealed class RahkaranSalesTestViewModel
{
    public long? OrderId { get; set; }
    public decimal? QuantityKg { get; set; }
    public decimal? PricePerKg { get; set; }
    public string? Confirmation { get; set; }
    public IReadOnlyList<RahkaranSalesTestOrderOption> Orders { get; set; } = [];
    public string? ResultMessage { get; set; }
    public string? ResponseBody { get; set; }
    public string? RemoteSalesRequestId { get; set; }
    public int? HttpStatusCode { get; set; }
    public bool? Succeeded { get; set; }
}

public sealed record RahkaranSalesTestOrderOption(
    long Id,
    string OrderNumber,
    string CustomerName,
    string? RahkaranCustomerId,
    string? CakeType);
