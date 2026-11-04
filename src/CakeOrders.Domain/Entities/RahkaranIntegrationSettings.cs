namespace CakeOrders.Domain.Entities;

public sealed class RahkaranIntegrationSettings
{
    public int Id { get; set; } = 1;

    // Finance mappings
    public long? BranchId { get; set; }
    public long? CashId { get; set; }
    public long? CashAccountingOperationId { get; set; }
    public long? CashFlowFactorId { get; set; }
    public long? PosBankAccountId { get; set; }
    public long? PosAccountingOperationId { get; set; }
    public long? PosCashFlowFactorId { get; set; }

    // Sales request header mappings
    public long? SalesOfficeId { get; set; }
    public long? SalesAreaId { get; set; }
    public long? SalesTypeId { get; set; }
    public long? SalesInventoryId { get; set; }
    public long? SalesPlantId { get; set; }
    public long? SalesCurrencyId { get; set; }
    public long? SalesPaymentAgreementId { get; set; }

    // Cake product and kilogram-unit mappings
    public long? CreamCakeProductId { get; set; }
    public long? CreamCakeUnitId { get; set; }
    public long? SemiFondantCakeProductId { get; set; }
    public long? SemiFondantCakeUnitId { get; set; }
    public long? FullFondantCakeProductId { get; set; }
    public long? FullFondantCakeUnitId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
