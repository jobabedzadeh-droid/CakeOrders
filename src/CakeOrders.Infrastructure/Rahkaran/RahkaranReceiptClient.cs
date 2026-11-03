using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CakeOrders.Infrastructure.Rahkaran;

public interface IRahkaranReceiptClient
{
    Task<RahkaranReceiptResult> RegisterCashReceiptAsync(
        string serviceBaseUrl,
        RahkaranCashReceiptRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RahkaranReceiptClient(HttpClient httpClient) : IRahkaranReceiptClient
{
    private const string Endpoint =
        "ReceiptAndPayment/ReceiptManagement/Services/ReceiptManagementService.svc/RegisterReceipt";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new RahkaranDateTimeOffsetConverter() }
    };

    public async Task<RahkaranReceiptResult> RegisterCashReceiptAsync(
        string serviceBaseUrl,
        RahkaranCashReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceBaseUrl);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var baseUri = new Uri(
            serviceBaseUrl.EndsWith('/') ? serviceBaseUrl : serviceBaseUrl + '/',
            UriKind.Absolute);
        var endpointUri = new Uri(baseUri, Endpoint);

        using var content = JsonContent.Create(request, options: JsonOptions);
        using var response = await httpClient.PostAsync(endpointUri, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new RahkaranReceiptResult(
                false, null, null, responseBody,
                (int)response.StatusCode);
        }

        RahkaranReceiptApiResponse? apiResponse;
        try
        {
            apiResponse = JsonSerializer.Deserialize<RahkaranReceiptApiResponse>(
                responseBody, JsonOptions);
        }
        catch (JsonException)
        {
            return new RahkaranReceiptResult(
                false, null, null,
                "پاسخ راهکاران قابل تجزیه نیست: " + responseBody,
                (int)response.StatusCode);
        }

        if (apiResponse is null)
            return new RahkaranReceiptResult(false, null, null, "پاسخ خالی از راهکاران دریافت شد.", (int)response.StatusCode);

        var validationError = apiResponse.GetValidationError();
        if (apiResponse.ID <= 0 || validationError is not null)
        {
            return new RahkaranReceiptResult(
                false,
                apiResponse.ID > 0 ? apiResponse.ID : null,
                apiResponse.Number,
                validationError ?? "راهکاران شناسه رسید معتبر برنگرداند.",
                (int)response.StatusCode);
        }

        return new RahkaranReceiptResult(
            true, apiResponse.ID, apiResponse.Number, null,
            (int)response.StatusCode);
    }
}

public sealed record RahkaranReceiptResult(
    bool Succeeded,
    long? ReceiptId,
    string? ReceiptNumber,
    string? Error,
    int HttpStatusCode);

public sealed class RahkaranCashReceiptRequest
{
    [JsonPropertyName("BranchID")]
    public long BranchID { get; init; }

    [JsonPropertyName("CashID")]
    public long CashID { get; init; }

    [JsonPropertyName("CounterPartDLCode")]
    public string CounterPartDLCode { get; init; } = string.Empty;

    [JsonPropertyName("Date")]
    public DateTimeOffset Date { get; init; }

    [JsonPropertyName("Description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("Description_En")]
    public string Description_En { get; init; } = string.Empty;

    [JsonPropertyName("IsApproved")]
    public bool IsApproved { get; init; }

    [JsonPropertyName("Number")]
    public string Number { get; init; } = string.Empty;

    [JsonPropertyName("ReceiptCashMoneys")]
    public IReadOnlyCollection<RahkaranCashReceiptItem> ReceiptCashMoneys { get; init; }
        = Array.Empty<RahkaranCashReceiptItem>();

    public void Validate()
    {
        if (BranchID <= 0) throw new ArgumentOutOfRangeException(nameof(BranchID));
        if (CashID <= 0) throw new ArgumentOutOfRangeException(nameof(CashID));
        ArgumentException.ThrowIfNullOrWhiteSpace(CounterPartDLCode);
        if (ReceiptCashMoneys.Count == 0)
            throw new ArgumentException("حداقل یک قلم دریافت نقدی الزامی است.", nameof(ReceiptCashMoneys));

        foreach (var item in ReceiptCashMoneys)
            item.Validate();
    }
}

public sealed class RahkaranCashReceiptItem
{
    [JsonPropertyName("AccountingOperationID")]
    public long AccountingOperationID { get; init; }

    [JsonPropertyName("Amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("BaseCurrencyAbbreviation")]
    public string? BaseCurrencyAbbreviation { get; init; }

    [JsonPropertyName("BaseCurrencyExchangeRate")]
    public decimal BaseCurrencyExchangeRate { get; init; } = 1m;

    [JsonPropertyName("CashFlowFactorID")]
    public long CashFlowFactorID { get; init; }

    [JsonPropertyName("CounterPartDLCode")]
    public string CounterPartDLCode { get; init; } = string.Empty;

    [JsonPropertyName("CurrencyAbbreviation")]
    public string? CurrencyAbbreviation { get; init; }

    [JsonPropertyName("Description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("Description_En")]
    public string Description_En { get; init; } = string.Empty;

    [JsonPropertyName("OperationalCurrencyExchangeRate")]
    public decimal OperationalCurrencyExchangeRate { get; init; } = 1m;

    public void Validate()
    {
        if (AccountingOperationID <= 0)
            throw new ArgumentOutOfRangeException(nameof(AccountingOperationID));
        if (Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(Amount));
        if (CashFlowFactorID <= 0)
            throw new ArgumentOutOfRangeException(nameof(CashFlowFactorID));
        ArgumentException.ThrowIfNullOrWhiteSpace(CounterPartDLCode);
    }
}

public sealed class RahkaranReceiptApiResponse
{
    [JsonPropertyName("ID")]
    public long ID { get; init; }

    [JsonPropertyName("Number")]
    public string? Number { get; init; }

    [JsonPropertyName("ValidationErrors")]
    public JsonElement ValidationErrors { get; init; }

    public string? GetValidationError()
    {
        if (ValidationErrors.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;

        if (ValidationErrors.ValueKind == JsonValueKind.Array)
        {
            var errors = ValidationErrors.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x));
            var message = string.Join(" | ", errors);
            return string.IsNullOrWhiteSpace(message) ? null : message;
        }

        if (ValidationErrors.ValueKind == JsonValueKind.String)
            return ValidationErrors.GetString();

        return ValidationErrors.ToString();
    }
}

internal sealed class RahkaranDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException("تاریخ راهکاران خالی است.");

        const string prefix = "/Date(";
        if (value.StartsWith(prefix, StringComparison.Ordinal) && value.EndsWith(")/", StringComparison.Ordinal))
        {
            var raw = value[prefix.Length..^2];
            var signIndex = raw.IndexOfAny(['+', '-'], 1);
            var millisecondsText = signIndex >= 0 ? raw[..signIndex] : raw;
            if (long.TryParse(millisecondsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
                return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }

        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    public override void Write(
        Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        var milliseconds = value.ToUnixTimeMilliseconds();
        var offset = value.Offset;
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();
        var suffix = $"{sign}{(int)absolute.TotalHours:00}{absolute.Minutes:00}";
        writer.WriteStringValue($"/Date({milliseconds}{suffix})/");
    }
}
