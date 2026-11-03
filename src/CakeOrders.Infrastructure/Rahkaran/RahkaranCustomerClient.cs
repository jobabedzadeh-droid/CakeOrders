using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CakeOrders.Infrastructure.Rahkaran;

public interface IRahkaranCustomerClient
{
    Task<RahkaranCreateCustomerResult> PlaceCustomerAsync(
        RahkaranPlaceCustomerRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RahkaranCustomerClient(HttpClient httpClient) : IRahkaranCustomerClient
{
    private const string Endpoint =
        "Sales/PartyManagement/Services/PartyManagementService.svc/PlaceCustomer";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new RahkaranDateTimeOffsetConverter() }
    };

    public async Task<RahkaranCreateCustomerResult> PlaceCustomerAsync(
        RahkaranPlaceCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        using var response = await httpClient.PostAsJsonAsync(
            Endpoint, request, JsonOptions, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new(false, null, null, body, (int)response.StatusCode);

        RahkaranCreateCustomerApiResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<RahkaranCreateCustomerApiResponse>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return new(false, null, null, "پاسخ ایجاد مشتری راهکاران قابل تجزیه نیست: " + body,
                (int)response.StatusCode);
        }

        if (result is null || result.CustomerID <= 0)
            return new(false, null, result?.PartyID, "راهکاران شناسه مشتری معتبر برنگرداند: " + body,
                (int)response.StatusCode);

        return new(true, result.CustomerID, result.PartyID, null, (int)response.StatusCode);
    }
}

public sealed record RahkaranCreateCustomerResult(
    bool Succeeded, long? CustomerId, long? PartyId, string? Error, int HttpStatusCode);

public sealed class RahkaranPlaceCustomerRequest
{
    [JsonPropertyName("Addresses")] public IReadOnlyCollection<RahkaranCustomerAddress> Addresses { get; init; } = [];
    [JsonPropertyName("CurrencyRef")] public long CurrencyRef { get; init; }
    [JsonPropertyName("DeclarationDate")] public DateTimeOffset DeclarationDate { get; init; }
    [JsonPropertyName("EconomicCode")] public string? EconomicCode { get; init; }
    [JsonPropertyName("ExchangeCode")] public string? ExchangeCode { get; init; }
    [JsonPropertyName("IsExportCustomer")] public bool IsExportCustomer { get; init; }
    [JsonPropertyName("Number")] public string? Number { get; init; }
    [JsonPropertyName("Party")] public RahkaranPartyData Party { get; init; } = new();
    [JsonPropertyName("SalesOfficeIDs")] public IReadOnlyCollection<long> SalesOfficeIDs { get; init; } = [];
    [JsonPropertyName("SecondCode")] public string? SecondCode { get; init; }
    [JsonPropertyName("Type")] public int Type { get; init; } = 1;

    public void Validate()
    {
        if (CurrencyRef <= 0) throw new ArgumentOutOfRangeException(nameof(CurrencyRef));
        if (SalesOfficeIDs.Count == 0 || SalesOfficeIDs.Any(x => x <= 0))
            throw new ArgumentException("حداقل یک مرکز فروش معتبر باید تنظیم شود.", nameof(SalesOfficeIDs));
        ArgumentNullException.ThrowIfNull(Party);
        Party.Validate();
    }
}

public sealed class RahkaranCustomerAddress
{
    [JsonPropertyName("Details")] public string Details { get; init; } = "";
    [JsonPropertyName("Email")] public string? Email { get; init; }
    [JsonPropertyName("Fax")] public string? Fax { get; init; }
    [JsonPropertyName("IsDefault")] public bool IsDefault { get; init; }
    [JsonPropertyName("Location")] public RahkaranLocation Location { get; init; } = new();
    [JsonPropertyName("Name")] public string Name { get; init; } = "آدرس اصلی";
    [JsonPropertyName("RegionalDivisionRef")] public long RegionalDivisionRef { get; init; }
    [JsonPropertyName("Tel")] public string? Tel { get; init; }
    [JsonPropertyName("Type")] public int Type { get; init; } = 3;
    [JsonPropertyName("ZipCode")] public string? ZipCode { get; init; }
}

public sealed class RahkaranLocation
{
    [JsonPropertyName("Latitude")] public string? Latitude { get; init; }
    [JsonPropertyName("Longitude")] public string? Longitude { get; init; }
}

public sealed class RahkaranPartyData
{
    [JsonPropertyName("BirthCertificateNumber")] public string? BirthCertificateNumber { get; init; }
    [JsonPropertyName("BirthDate")] public DateTimeOffset? BirthDate { get; init; }
    [JsonPropertyName("CompanyName")] public string? CompanyName { get; init; }
    [JsonPropertyName("CompanyRegisterationCode")] public string? CompanyRegisterationCode { get; init; }
    [JsonPropertyName("FatherName")] public string? FatherName { get; init; }
    [JsonPropertyName("FidaRegistration")] public string? FidaRegistration { get; init; }
    [JsonPropertyName("FirstName")] public string? FirstName { get; init; }
    [JsonPropertyName("LastName")] public string? LastName { get; init; }
    [JsonPropertyName("Mobile")] public string? Mobile { get; init; }
    [JsonPropertyName("NationalID")] public string? NationalID { get; init; }
    [JsonPropertyName("Type")] public int Type { get; init; }

    public void Validate()
    {
        if (Type == 0 &&
            string.IsNullOrWhiteSpace(FirstName) &&
            string.IsNullOrWhiteSpace(LastName))
            throw new ArgumentException("برای شخص حقیقی نام یا نام خانوادگی الزامی است.");
        if (Type == 1 && string.IsNullOrWhiteSpace(CompanyName))
            throw new ArgumentException("برای شخص حقوقی نام شرکت الزامی است.");
    }
}

public sealed class RahkaranCreateCustomerApiResponse
{
    [JsonPropertyName("CustomerID")] public long CustomerID { get; init; }
    [JsonPropertyName("PartyID")] public long PartyID { get; init; }
}
