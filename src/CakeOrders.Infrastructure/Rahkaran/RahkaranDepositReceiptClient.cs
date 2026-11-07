using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace CakeOrders.Infrastructure.Rahkaran;

public interface IRahkaranDepositReceiptClient
{
    Task<RahkaranReceiptResult> RegisterDepositReceiptAsync(
        string serviceBaseUrl,
        RahkaranDepositReceiptRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates a receipt for funds deposited to a bank account (e.g. POS settlement).
/// Use only after Finance confirms the target bank account and operation mapping.
/// </summary>
public sealed class RahkaranDepositReceiptClient(HttpClient httpClient) : IRahkaranDepositReceiptClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new RahkaranDateTimeOffsetConverter() }
    };

    private const string Endpoint =
        "ReceiptAndPayment/ReceiptManagement/Services/ReceiptManagementService.svc/RegisterReceipt";

    public async Task<RahkaranReceiptResult> RegisterDepositReceiptAsync(
        string serviceBaseUrl,
        RahkaranDepositReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceBaseUrl);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var baseUri = new Uri(
            serviceBaseUrl.EndsWith('/') ? serviceBaseUrl : serviceBaseUrl + '/',
            UriKind.Absolute);

        using var content = JsonContent.Create(request, options: JsonOptions);
        using var response = await httpClient.PostAsync(
            new Uri(baseUri, Endpoint), content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new(false, null, null, body, (int)response.StatusCode);

        RahkaranReceiptApiResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<RahkaranReceiptApiResponse>(
                body, JsonOptions);
        }
        catch (JsonException)
        {
            return new(false, null, null, "پاسخ راهکاران قابل تجزیه نیست: " + body, (int)response.StatusCode);
        }

        if (result is null)
            return new(false, null, null, "پاسخ خالی از راهکاران دریافت شد.", (int)response.StatusCode);

        var validationError = result.GetValidationError();
        if (result.ID <= 0 || validationError is not null)
            return new(false, result.ID > 0 ? result.ID : null, result.Number,
                validationError ?? "راهکاران شناسه رسید معتبر برنگرداند.", (int)response.StatusCode);

        return new(true, result.ID, result.Number, null, (int)response.StatusCode);
    }
}

public sealed class RahkaranDepositReceiptRequest
{
    [JsonPropertyName("BranchID")] public long BranchID { get; init; }
    [JsonPropertyName("CashID")] public long CashID { get; init; }
    [JsonPropertyName("CounterPartDLCode")] public string CounterPartDLCode { get; init; } = "";
    [JsonPropertyName("Date")] public DateTimeOffset Date { get; init; }
    [JsonPropertyName("Description")] public string Description { get; init; } = "";
    [JsonPropertyName("Description_En")] public string Description_En { get; init; } = "";
    [JsonPropertyName("IsApproved")] public bool IsApproved { get; init; }
    [JsonPropertyName("Number")] public string Number { get; init; } = "";
    [JsonPropertyName("ReceiptDeposits")] public IReadOnlyCollection<RahkaranDepositReceiptItem> ReceiptDeposits { get; init; }
        = Array.Empty<RahkaranDepositReceiptItem>();

    public void Validate()
    {
        if (BranchID <= 0) throw new ArgumentOutOfRangeException(nameof(BranchID));
        if (CashID <= 0) throw new ArgumentOutOfRangeException(nameof(CashID));
        ArgumentException.ThrowIfNullOrWhiteSpace(CounterPartDLCode);
        if (ReceiptDeposits.Count == 0)
            throw new ArgumentException("حداقل یک قلم واریز بانکی الزامی است.", nameof(ReceiptDeposits));
        foreach (var item in ReceiptDeposits) item.Validate();
    }
}

public sealed class RahkaranDepositReceiptItem
{
    [JsonPropertyName("AccountingOperationID")] public long AccountingOperationID { get; init; }
    [JsonPropertyName("Amount")] public decimal Amount { get; init; }
    [JsonPropertyName("BankAccountID")] public long BankAccountID { get; init; }
    [JsonPropertyName("BaseCurrencyAbbreviation")] public string? BaseCurrencyAbbreviation { get; init; }
    [JsonPropertyName("BaseCurrencyExchangeRate")] public decimal BaseCurrencyExchangeRate { get; init; } = 1m;
    [JsonPropertyName("CashFlowFactorID")] public long? CashFlowFactorID { get; init; }
    [JsonPropertyName("CounterPartDLCode")] public string CounterPartDLCode { get; init; } = "";
    [JsonPropertyName("Date")] public DateTimeOffset Date { get; init; }
    [JsonPropertyName("Description")] public string Description { get; init; } = "";
    [JsonPropertyName("Description_En")] public string Description_En { get; init; } = "";
    [JsonPropertyName("Number")] public string Number { get; init; } = "";
    [JsonPropertyName("OperationalCurrencyExchangeRate")] public decimal OperationalCurrencyExchangeRate { get; init; } = 1m;

    public void Validate()
    {
        if (AccountingOperationID <= 0) throw new ArgumentOutOfRangeException(nameof(AccountingOperationID));
        if (Amount <= 0) throw new ArgumentOutOfRangeException(nameof(Amount));
        if (BankAccountID <= 0) throw new ArgumentOutOfRangeException(nameof(BankAccountID));
        if (CashFlowFactorID is not > 0) throw new ArgumentOutOfRangeException(nameof(CashFlowFactorID));
        ArgumentException.ThrowIfNullOrWhiteSpace(CounterPartDLCode);
    }
}
