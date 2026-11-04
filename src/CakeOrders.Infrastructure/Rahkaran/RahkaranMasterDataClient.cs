using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CakeOrders.Infrastructure.Rahkaran;

public interface IRahkaranMasterDataClient
{
    Task<IReadOnlyList<RahkaranBranchDto>> GetBranchesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RahkaranCashDto>> GetCashesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RahkaranAccountingOperationDto>> GetAccountingOperationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RahkaranCashFlowFactorDto>> GetCashFlowFactorsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RahkaranBankAccountDto>> GetBankAccountsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RahkaranSalesAreaDto>> GetSalesAreasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RahkaranSalesOfficeDto>> GetSalesOfficesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RahkaranSalesTypeDto>> GetSalesTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RahkaranCustomerDto>> GetCustomersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RahkaranCustomerDto>> GetCustomerChangesAsync(
        DateTimeOffset fromDate, DateTimeOffset toDate, long masterEntityId,
        int pageIndex, int pageSize, CancellationToken cancellationToken = default);
}

public sealed class RahkaranMasterDataClient(HttpClient httpClient) : IRahkaranMasterDataClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new RahkaranDateTimeOffsetConverter() }
    };
    private const string Treasury = "ReceiptAndPayment/TreasuryStructure/Services/TreasuryStructureService.svc/";
    private const string Financial = "ReceiptAndPayment/FinancialOperation/Services/FinancialOperationService.svc/";
    private const string Branches = "General/BranchManagement/Services/BranchService.svc/";
    private const string Parties = "Sales/PartyManagement/Services/PartyManagementService.svc/";
    private const string SalesStructure = "Sales/SalesStructure/Services/SalesStructureService.svc/";

    public Task<IReadOnlyList<RahkaranBranchDto>> GetBranchesAsync(CancellationToken ct = default) =>
        GetListAsync<RahkaranBranchDto>(Branches + "GetBranches", ct);

    public Task<IReadOnlyList<RahkaranCashDto>> GetCashesAsync(CancellationToken ct = default) =>
        GetListAsync<RahkaranCashDto>(Treasury + "GetAllCashes", ct);

    public Task<IReadOnlyList<RahkaranAccountingOperationDto>> GetAccountingOperationsAsync(CancellationToken ct = default) =>
        GetListAsync<RahkaranAccountingOperationDto>(Financial + "GetAllAccountingOperations", ct);

    public Task<IReadOnlyList<RahkaranCashFlowFactorDto>> GetCashFlowFactorsAsync(CancellationToken ct = default) =>
        GetListAsync<RahkaranCashFlowFactorDto>(Financial + "GetAllCashFlowFactors", ct);

    public Task<IReadOnlyList<RahkaranBankAccountDto>> GetBankAccountsAsync(CancellationToken ct = default) =>
        GetListAsync<RahkaranBankAccountDto>(Treasury + "GetAllBankAccounts", ct);

    public Task<IReadOnlyList<RahkaranSalesAreaDto>> GetSalesAreasAsync(CancellationToken ct = default) =>
        GetListAsync<RahkaranSalesAreaDto>(SalesStructure + "GetSalesAreas", ct);

    public Task<IReadOnlyList<RahkaranSalesOfficeDto>> GetSalesOfficesAsync(CancellationToken ct = default) =>
        GetListAsync<RahkaranSalesOfficeDto>(SalesStructure + "GetSalesOffices", ct);

    public Task<IReadOnlyList<RahkaranSalesTypeDto>> GetSalesTypesAsync(CancellationToken ct = default) =>
        GetListAsync<RahkaranSalesTypeDto>(SalesStructure + "GetSalesTypes", ct);

    public Task<IReadOnlyList<RahkaranCustomerDto>> GetCustomersAsync(CancellationToken ct = default) =>
        GetListAsync<RahkaranCustomerDto>(Parties + "GetCustomers", ct);

    public async Task<IReadOnlyList<RahkaranCustomerDto>> GetCustomerChangesAsync(
        DateTimeOffset fromDate, DateTimeOffset toDate, long masterEntityId,
        int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        if (masterEntityId < 0) throw new ArgumentOutOfRangeException(nameof(masterEntityId));
        if (pageIndex < 1) throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (pageSize < 1 || pageSize > 1000) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (toDate < fromDate) throw new ArgumentException("ToDate cannot be earlier than FromDate.");

        var request = new RahkaranCustomerListRequest
        {
            FromDate = fromDate,
            ToDate = toDate,
            LastModificationDate = toDate,
            MasterEntityID = masterEntityId,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        using var response = await httpClient.PostAsJsonAsync(
            new Uri("Sales/PartyManagement/Services/PartyManagementService.svc/GetCustomerList", UriKind.Relative),
            request,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<RahkaranCustomerDto>>(
            cancellationToken: cancellationToken) ?? [];
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(relativePath, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<T>>(
            cancellationToken: cancellationToken) ?? [];
    }
}

public sealed class RahkaranBranchDto
{
    [JsonPropertyName("ID")] public long Id { get; init; }
    [JsonPropertyName("Code")] public string? Code { get; init; }
    [JsonPropertyName("Title")] public string? Title { get; init; }
    [JsonPropertyName("Title_En")] public string? TitleEn { get; init; }
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }
}

public sealed class RahkaranCashDto
{
    [JsonPropertyName("ID")] public long Id { get; init; }
    [JsonPropertyName("Title")] public string? Title { get; init; }
    [JsonPropertyName("BranchRef")] public long? BranchRef { get; init; }
    [JsonPropertyName("BranchTitle")] public string? BranchTitle { get; init; }
    [JsonPropertyName("CashierName")] public string? CashierName { get; init; }
}

public sealed class RahkaranAccountingOperationDto
{
    [JsonPropertyName("ID")] public long Id { get; init; }
    [JsonPropertyName("Code")] public string? Code { get; init; }
    [JsonPropertyName("Title")] public string? Title { get; init; }
    [JsonPropertyName("SlRef")] public long? SlRef { get; init; }
}

public sealed class RahkaranCashFlowFactorDto
{
    [JsonPropertyName("ID")] public long Id { get; init; }
    [JsonPropertyName("Code")] public string? Code { get; init; }
    [JsonPropertyName("Title")] public string? Title { get; init; }
    [JsonPropertyName("SlRef")] public long? SlRef { get; init; }
}

public sealed class RahkaranBankAccountDto
{
    [JsonPropertyName("ID")] public long Id { get; init; }
    [JsonPropertyName("AccountNumber")] public string? AccountNumber { get; init; }
    [JsonPropertyName("BankName")] public string? BankName { get; init; }
    [JsonPropertyName("BankBranchTitle")] public string? BankBranchTitle { get; init; }
    [JsonPropertyName("InternationalNumber")] public string? Iban { get; init; }
    [JsonPropertyName("State")] public int? State { get; init; }
}

public sealed class RahkaranCustomerDto
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("PartyId")] public long? PartyId { get; init; }
    [JsonPropertyName("number")] public string? Number { get; init; }
    [JsonPropertyName("PartyName")] public string? PartyName { get; init; }
    [JsonPropertyName("FirstName")] public string? FirstName { get; init; }
    [JsonPropertyName("LastName")] public string? LastName { get; init; }
    [JsonPropertyName("Mobile")] public string? Mobile { get; init; }
    [JsonPropertyName("Tel")] public string? Tel { get; init; }
    [JsonPropertyName("Email")] public string? Email { get; init; }
    [JsonPropertyName("Details")] public string? Details { get; init; }
    [JsonPropertyName("DLCode")] public string? DLCode { get; init; }
    [JsonPropertyName("Status")] public int? Status { get; init; }
    [JsonPropertyName("Type")] public int? Type { get; init; }
}

public sealed class RahkaranCustomerListRequest
{
    [JsonPropertyName("FromDate")] public DateTimeOffset FromDate { get; init; }
    [JsonPropertyName("ToDate")] public DateTimeOffset ToDate { get; init; }
    [JsonPropertyName("LastModificationDate")] public DateTimeOffset LastModificationDate { get; init; }
    [JsonPropertyName("MasterEntityID")] public long MasterEntityID { get; init; }
    [JsonPropertyName("PageIndex")] public int PageIndex { get; init; }
    [JsonPropertyName("PageSize")] public int PageSize { get; init; }
}

public sealed class RahkaranSalesAreaDto
{
    [JsonPropertyName("id"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long Id { get; init; }
    [JsonPropertyName("code")] public string? Code { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("stateTitle")] public string? StateTitle { get; init; }
}

public sealed class RahkaranSalesOfficeDto
{
    [JsonPropertyName("id"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long Id { get; init; }
    [JsonPropertyName("code")] public string? Code { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("branchRef")] public string? BranchRef { get; init; }
    [JsonPropertyName("stateTitle")] public string? StateTitle { get; init; }
    [JsonPropertyName("areas")] public IReadOnlyList<RahkaranSalesOfficeAreaDto> Areas { get; init; } = [];
}

public sealed class RahkaranSalesOfficeAreaDto
{
    [JsonPropertyName("id"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long Id { get; init; }
    [JsonPropertyName("salesAreaRef"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long SalesAreaRef { get; init; }
    [JsonPropertyName("isDefault")] public bool IsDefault { get; init; }
}

public sealed class RahkaranSalesTypeDto
{
    [JsonPropertyName("id"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long Id { get; init; }
    [JsonPropertyName("number")] public string? Number { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("stateTitle")] public string? StateTitle { get; init; }
}
