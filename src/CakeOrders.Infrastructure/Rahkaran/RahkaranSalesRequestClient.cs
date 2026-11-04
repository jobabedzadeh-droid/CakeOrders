using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CakeOrders.Infrastructure.Rahkaran;

public sealed record RahkaranSalesRequestResult(bool Succeeded, string? SalesRequestId, string? Error, int HttpStatusCode);

public interface IRahkaranSalesRequestClient
{
    Task<RahkaranSalesRequestResult> PlacePendingAsync(RahkaranSaleRequest request, CancellationToken cancellationToken);
}

public sealed class RahkaranSalesRequestClient(HttpClient httpClient) : IRahkaranSalesRequestClient
{
    private const string Endpoint = "Sales/OrderManagement/Services/OrderManagementService.svc/PlaceSaleRequestInPendingState";

    public async Task<RahkaranSalesRequestResult> PlacePendingAsync(RahkaranSaleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var response = await httpClient.PostAsJsonAsync(Endpoint, request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new(false, null, body, (int)response.StatusCode);

        var id = TryReadId(body);
        return id is > 0
            ? new(true, id.Value.ToString(CultureInfo.InvariantCulture), body, (int)response.StatusCode)
            : new(false, null, "پاسخ HTTP موفق بود اما شناسه درخواست فروش از پاسخ قابل استخراج نبود؛ نتیجه مبهم است. پاسخ: " + body, (int)response.StatusCode);
    }

    private static long? TryReadId(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind == JsonValueKind.Number && json.RootElement.TryGetInt64(out var number))
                return number;
            if (json.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in json.RootElement.EnumerateObject())
                    if (property.Name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("ID", StringComparison.OrdinalIgnoreCase))
                    {
                        if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out number)) return number;
                        if (long.TryParse(property.Value.ToString(), out number)) return number;
                    }
            }
        }
        catch (JsonException) { }

        var match = Regex.Match(body, @"(?:شناسه|ID)\s*[:：]?\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && long.TryParse(match.Groups[1].Value, out var parsed) ? parsed : null;
    }
}

public sealed class RahkaranSaleRequest
{
    [JsonPropertyName("id")] public string Id { get; init; } = "-1";
    [JsonPropertyName("customerId")] public string CustomerId { get; init; } = "";
    [JsonPropertyName("currencyId")] public string CurrencyId { get; init; } = "";
    [JsonPropertyName("date")] public string Date { get; init; } = "";
    [JsonPropertyName("inventoryId")] public string InventoryId { get; init; } = "";
    [JsonPropertyName("payerType")] public int PayerType { get; init; } = 1;
    [JsonPropertyName("paymentAgreementId")] public string? PaymentAgreementId { get; init; }
    [JsonPropertyName("plantId")] public string PlantId { get; init; } = "";
    [JsonPropertyName("salesAreaId")] public string SalesAreaId { get; init; } = "";
    [JsonPropertyName("salesOfficeId")] public string SalesOfficeId { get; init; } = "";
    [JsonPropertyName("salesTypeId")] public string SalesTypeId { get; init; } = "";
    [JsonPropertyName("price")] public decimal Price { get; init; }
    [JsonPropertyName("netPrice")] public decimal NetPrice { get; init; }
    [JsonPropertyName("additionAmount")] public decimal AdditionAmount { get; init; }
    [JsonPropertyName("reductionAmount")] public decimal ReductionAmount { get; init; }
    [JsonPropertyName("policyResults")] public object[] PolicyResults { get; init; } = [];
    [JsonPropertyName("agentID")] public string? AgentId { get; init; }
    [JsonPropertyName("oneTimeCustomerID")] public string? OneTimeCustomerId { get; init; }
    [JsonPropertyName("payerAccountID")] public string? PayerAccountId { get; init; }
    [JsonPropertyName("recipientType")] public int RecipientType { get; init; } = 1;
    [JsonPropertyName("addressId")] public string AddressId { get; init; } = "";
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("items")] public IReadOnlyList<RahkaranSaleRequestItem> Items { get; init; } = [];
}

public sealed class RahkaranSaleRequestItem
{
    [JsonPropertyName("id")] public string Id { get; init; } = "-1";
    [JsonPropertyName("index")] public int Index { get; init; } = 1;
    [JsonPropertyName("productId")] public string ProductId { get; init; } = "";
    [JsonPropertyName("quantity")] public string Quantity { get; init; } = "";
    [JsonPropertyName("requestedQuantity")] public string RequestedQuantity { get; init; } = "";
    [JsonPropertyName("salesAreaId")] public string SalesAreaId { get; init; } = "";
    [JsonPropertyName("unitId")] public string UnitId { get; init; } = "";
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("additionAmount")] public decimal AdditionAmount { get; init; }
    [JsonPropertyName("fee")] public decimal Fee { get; init; }
    [JsonPropertyName("netPrice")] public decimal NetPrice { get; init; }
    [JsonPropertyName("price")] public decimal Price { get; init; }
    [JsonPropertyName("reductionAmount")] public decimal ReductionAmount { get; init; }
    [JsonPropertyName("type")] public int Type { get; init; } = 1;
    [JsonPropertyName("productPackRef")] public string? ProductPackRef { get; init; }
    [JsonPropertyName("productPackItemRef")] public string? ProductPackItemRef { get; init; }
    [JsonPropertyName("productPackQuantity")] public string? ProductPackQuantity { get; init; }
    [JsonPropertyName("freeProductPolicyConditionRowId")] public string? FreeProductPolicyConditionRowId { get; init; }
    [JsonPropertyName("freeProductPolicyId")] public string? FreeProductPolicyId { get; init; }
    [JsonPropertyName("parentItemIndex")] public int? ParentItemIndex { get; init; }
    [JsonPropertyName("policyResults")] public object[] PolicyResults { get; init; } = [];
}
