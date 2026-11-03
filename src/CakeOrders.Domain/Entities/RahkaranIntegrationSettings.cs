namespace CakeOrders.Domain.Entities;

public sealed class RahkaranIntegrationSettings
{
    public int Id { get; set; } = 1;

    public long? BranchId { get; set; }
    public long? CashId { get; set; }

    public long? CashAccountingOperationId { get; set; }
    public long? CashFlowFactorId { get; set; }

    public long? PosBankAccountId { get; set; }
    public long? PosAccountingOperationId { get; set; }
    public long? PosCashFlowFactorId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
