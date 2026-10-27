using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Domain.Entities;

public enum OrderChannel { [Display(Name = "حضوری")] InPerson = 1, [Display(Name = "آنلاین")] Online = 2 }
public enum OrderStatus { [Display(Name = "در انتظار بررسی")] PendingReview = 1, [Display(Name = "تأیید شده")] Confirmed = 2, [Display(Name = "رد شده")] Rejected = 3, [Display(Name = "در حال تولید")] InProduction = 4, [Display(Name = "آماده تحویل")] ReadyForDelivery = 5, [Display(Name = "تحویل شده")] Delivered = 6, [Display(Name = "لغو شده")] Cancelled = 7 }
public enum CakeType { [Display(Name = "خامه‌ای")] Cream = 1, [Display(Name = "نیمه فوندانت")] SemiFondant = 2, [Display(Name = "تمام فوندانت")] FullFondant = 3 }

public sealed class Order
{
    public long Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderChannel Channel { get; set; } = OrderChannel.InPerson;
    public OrderStatus Status { get; set; } = OrderStatus.PendingReview;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime DeliveryAt { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public CakeType CakeType { get; set; } = CakeType.Cream;
    public decimal RequestedWeightKg { get; set; }
    public decimal PricePerKg { get; set; }
    public string? PrintType { get; set; }
    public string? BackgroundColor { get; set; }
    public string? MoldType { get; set; }
    public bool HasBananaFilling { get; set; }
    public bool HasWalnutFilling { get; set; }
    public string? WritingLocation { get; set; }
    public string? CakeText { get; set; }
    public string? DesignNotes { get; set; }
    public string? SampleImagePath { get; set; }
    public string? PrintImagePath { get; set; }
    public decimal MacaronCost { get; set; }
    public decimal FlowerCost { get; set; }
    public decimal DummyCost { get; set; }
    public decimal PrintingCost { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal OtherCost { get; set; }
    public decimal Discount { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public string? ReferrerCode { get; set; }
    public string? RejectionReason { get; set; }
    public string? RahkaranCustomerId { get; set; }
    public string? RahkaranSalesRequestId { get; set; }
    public string? RahkaranReceiptId { get; set; }
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
    public decimal RemainingAmount => Math.Max(0, FinalAmount - DepositAmount);
}