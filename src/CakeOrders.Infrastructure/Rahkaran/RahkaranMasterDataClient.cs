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
    Task<IReadOnlyList<RahkaranEmployeeDto>> GetEmployeesAsync(CancellationToken cancellationToken = default);
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
    private const string Employees = "HCM/Staff/Services/EmployeeWebService.svc/";

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

    public async Task<IReadOnlyList<RahkaranCustomerDto>> GetCustomersAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync(Parties + "GetCustomers", ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var payload = FindCustomerCollection(document.RootElement);
        if (payload.ValueKind == JsonValueKind.Array)
        {
            var customers = new List<RahkaranCustomerDto>();
            foreach (var item in payload.EnumerateArray())
            {
                var customerElement = FindCustomerCollection(item);
                if (customerElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                    customerElement = item;
                if (customerElement.ValueKind == JsonValueKind.Array)
                    continue;

                var customer = JsonSerializer.Deserialize<RahkaranCustomerDto>(
                    customerElement.GetRawText(), JsonOptions);
                if (customer is not null && customer.Id > 0)
                    customers.Add(customer);
            }
            return customers;
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            var single = JsonSerializer.Deserialize<RahkaranCustomerDto>(
                payload.GetRawText(), JsonOptions);
            return single is not null && single.Id > 0 ? [single] : [];
        }

        return [];
    }

    private static JsonElement FindCustomerCollection(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element;

        if (element.ValueKind != JsonValueKind.Object)
            return default;

        if (element.TryGetProperty("id", out _) || element.TryGetProperty("ID", out _))
            return element;

        // WCF versions may wrap the list in Response/Result/CustomerDocument objects.
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                continue;

            var nested = FindCustomerCollection(property.Value);
            if (nested.ValueKind == JsonValueKind.Array)
                return nested;

            if (nested.ValueKind == JsonValueKind.Object &&
                (nested.TryGetProperty("id", out _) || nested.TryGetProperty("ID", out _)))
                return nested;
        }

        return default;
    }

    public async Task<IReadOnlyList<RahkaranEmployeeDto>> GetEmployeesAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            Employees + "GetEmployeeList",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        var payload = document.RootElement;

        // Rahkaran may return either a direct JSON array or a WCF-style object wrapper.
        // TryGetProperty is valid only for JSON objects, so inspect the token kind first.
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("GetEmployeeListResponse", out var responseElement))
        {
            payload = responseElement;
        }

        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("GetEmployeeListResult", out var resultElement))
        {
            payload = resultElement;
        }

        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("EmployeeDocument", out var employeeDocumentElement))
        {
            payload = employeeDocumentElement;
        }

        var employees = new List<RahkaranEmployeeDto>();

        if (payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payload.EnumerateArray())
            {
                var employeeElement = item;
                if (employeeElement.ValueKind == JsonValueKind.Object &&
                    employeeElement.TryGetProperty("EmployeeDocument", out var nestedEmployee))
                {
                    employeeElement = nestedEmployee;
                }

                var employee = JsonSerializer.Deserialize<RahkaranEmployeeDto>(
                    employeeElement.GetRawText(),
                    JsonOptions);

                if (employee is not null)
                    employees.Add(employee);
            }

            return employees;
        }

        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("Code", out _))
        {
            var employee = JsonSerializer.Deserialize<RahkaranEmployeeDto>(
                payload.GetRawText(),
                JsonOptions);

            if (employee is not null)
                employees.Add(employee);
        }

        return employees;
    }

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

public sealed class RahkaranEmployeeDto
{
    [JsonPropertyName("EmployeeId"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? EmployeeId { get; init; }

    [JsonPropertyName("Code")]
    public string? Code { get; init; }

    [JsonPropertyName("FirstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("LastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("Name")]
    public string? Name { get; init; }
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
    // Rahkaran uses both id/ID and PartyId/PartyRef across its documented methods.
    [JsonPropertyName("id"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long Id { get; init; }

    [JsonPropertyName("PartyId"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? PartyId { get; init; }

    [JsonPropertyName("PartyRef"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? PartyRef { get; init; }

    [JsonPropertyName("number")] public string? Number { get; init; }
    [JsonPropertyName("SecondCode")] public string? SecondCode { get; init; }
    [JsonPropertyName("DLCode")] public string? DLCode { get; init; }
    [JsonPropertyName("PartyName")] public string? PartyName { get; init; }
    [JsonPropertyName("FirstName")] public string? FirstName { get; init; }
    [JsonPropertyName("LastName")] public string? LastName { get; init; }
    [JsonPropertyName("CompanyName")] public string? CompanyName { get; init; }
    [JsonPropertyName("Mobile")] public string? Mobile { get; init; }
    [JsonPropertyName("Tel")] public string? Tel { get; init; }
    [JsonPropertyName("Email")] public string? Email { get; init; }
    [JsonPropertyName("Details")] public string? Details { get; init; }
    [JsonPropertyName("NationalID")] public string? NationalId { get; init; }
    [JsonPropertyName("EconomicCode")] public string? EconomicCode { get; init; }
    [JsonPropertyName("RegistrationCode")] public string? RegistrationCode { get; init; }
    [JsonPropertyName("WebsiteUrl")] public string? WebsiteUrl { get; init; }

    [JsonPropertyName("StateCode"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? StateCode { get; init; }
    [JsonPropertyName("StateTitle")] public string? StateTitle { get; init; }
    [JsonPropertyName("TypeCode"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? TypeCode { get; init; }
    [JsonPropertyName("TypeTitle")] public string? TypeTitle { get; init; }
    [JsonPropertyName("Status"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Status { get; init; }
    [JsonPropertyName("Type"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Type { get; init; }
    [JsonPropertyName("PartyType"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? PartyType { get; init; }

    [JsonPropertyName("CreationDate")]
    public DateTimeOffset? CreationDate { get; init; }
    [JsonPropertyName("LastModificationDate")]
    public DateTimeOffset? LastModificationDate { get; init; }

    // Keep nested address records intact; their exact shape can vary by Rahkaran version.
    [JsonPropertyName("Addresses")]
    public JsonElement? Addresses { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalFields { get; init; }
}

public sealed class RahkaranCustomerListRequest
{
    [JsonPropertyName("FromDate")] public DateTimeOffset FromDate { get; init; }
    [JsonPropertyName("ToDate")] public DateTimeOffset ToDate { get; init; }
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
