using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Domain.Entities;

public enum PaymentTransactionStatus
{
    Created = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4,
    Cancelled = 5,
    Unknown = 6,
    ReconciliationRequired = 7
}

public enum PaymentPurpose
{
    Deposit = 1,
    Settlement = 2
}

public sealed class PaymentTransaction
{
    public long Id { get; set; }

    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public long CashRegisterId { get; set; }
    public CashRegister CashRegister { get; set; } = null!;

    public long PosTerminalId { get; set; }
    public PosTerminal PosTerminal { get; set; } = null!;

    public PaymentPurpose Purpose { get; set; }

    public decimal Amount { get; set; }

    // کلید یکتای درخواست برای جلوگیری از ثبت تکراری
    [Required]
    [MaxLength(100)]
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");

    // شناسه اختصاصی تراکنش در نرم‌افزار ما
    [Required]
    [MaxLength(100)]
    public string MerchantReference { get; set; } = Guid.NewGuid().ToString("N");

    public PaymentTransactionStatus Status { get; set; }
        = PaymentTransactionStatus.Created;

    // اطلاعات برگشتی از کارت‌خوان
    [MaxLength(100)]
    public string? Rrn { get; set; }

    [MaxLength(100)]
    public string? TraceNumber { get; set; }

    [MaxLength(50)]
    public string? ResponseCode { get; set; }

    [MaxLength(1000)]
    public string? ResponseMessage { get; set; }

    // شناسه نصب SadadBridge که درخواست را اجرا کرده است
    [MaxLength(100)]
    public string? BridgeInstallationId { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessingAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}