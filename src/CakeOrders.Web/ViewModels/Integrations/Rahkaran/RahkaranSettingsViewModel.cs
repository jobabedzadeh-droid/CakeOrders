using Microsoft.AspNetCore.Mvc.Rendering;

namespace CakeOrders.Web.ViewModels.Integrations.Rahkaran;

public sealed class RahkaranSettingsViewModel
{
    public long? BranchId { get; set; }
    public long? CashId { get; set; }
    public long? CashAccountingOperationId { get; set; }
    public long? CashFlowFactorId { get; set; }
    public long? PosBankAccountId { get; set; }
    public long? PosAccountingOperationId { get; set; }
    public long? PosCashFlowFactorId { get; set; }

    public long? SalesOfficeId { get; set; }
    public long? SalesAreaId { get; set; }
    public long? SalesTypeId { get; set; }
    public long? SalesInventoryId { get; set; }
    public long? SalesPlantId { get; set; }
    public long? SalesCurrencyId { get; set; }
    public long? SalesPaymentAgreementId { get; set; }

    public long? CreamCakeProductId { get; set; }
    public long? CreamCakeUnitId { get; set; }
    public long? SemiFondantCakeProductId { get; set; }
    public long? SemiFondantCakeUnitId { get; set; }
    public long? FullFondantCakeProductId { get; set; }
    public long? FullFondantCakeUnitId { get; set; }

    public string ServiceBaseUrl { get; set; } = string.Empty;
    public bool IntegrationEnabled { get; set; }
    public int CachedEmployeeCount { get; set; }

    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];
    public IReadOnlyList<SelectListItem> Cashes { get; set; } = [];
    public IReadOnlyList<SelectListItem> BankAccounts { get; set; } = [];
    public IReadOnlyList<SelectListItem> AccountingOperations { get; set; } = [];
    public IReadOnlyList<SelectListItem> CashFlowFactors { get; set; } = [];
    public IReadOnlyList<SelectListItem> SalesOffices { get; set; } = [];
    public IReadOnlyList<SelectListItem> SalesAreas { get; set; } = [];
    public IReadOnlyList<SelectListItem> SalesTypes { get; set; } = [];
}
