using System.ComponentModel.DataAnnotations;
using CakeOrders.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CakeOrders.Web.ViewModels;

public sealed class OrderPaymentStartViewModel
{
    public long OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public decimal RemainingAmount { get; set; }

    [Required(ErrorMessage = "صندوق دریافت‌کننده را انتخاب کنید.")]
    public long CashRegisterId { get; set; }

    [Required]
    [EnumDataType(typeof(PaymentPurpose))]
    public PaymentPurpose Purpose { get; set; } = PaymentPurpose.Deposit;

    [Range(typeof(decimal), "1", "999999999999999", ErrorMessage = "مبلغ باید بیشتر از صفر باشد.")]
    public decimal Amount { get; set; }

    public IReadOnlyList<SelectListItem> CashRegisters { get; set; }
        = Array.Empty<SelectListItem>();
}