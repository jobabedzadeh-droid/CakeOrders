using Microsoft.AspNetCore.Mvc.Rendering;

namespace CakeOrders.Web.ViewModels;

public sealed class RahkaranSettingsViewModel
{
    public long? BranchId { get; set; }
    public long? CashId { get; set; }

    public long? CashAccountingOperationId { get; set; }
    public long? CashFlowFactorId { get; set; }

    public long? PosBankAccountId { get; set; }
    public long? PosAccountingOperationId { get; set; }
    public long? PosCashFlowFactorId { get; set; }

    public string ServiceBaseUrl { get; set; } = string.Empty;
    public bool IntegrationEnabled { get; set; }

    public bool HasSuccessfulResponse { get; set; }
    public int SuccessfulEndpoints { get; set; }
    public List<string> LoadMessages { get; set; } = [];

    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];
    public IReadOnlyList<SelectListItem> Cashes { get; set; } = [];
    public IReadOnlyList<SelectListItem> BankAccounts { get; set; } = [];
    public IReadOnlyList<SelectListItem> AccountingOperations { get; set; } = [];
    public IReadOnlyList<SelectListItem> CashFlowFactors { get; set; } = [];
}
