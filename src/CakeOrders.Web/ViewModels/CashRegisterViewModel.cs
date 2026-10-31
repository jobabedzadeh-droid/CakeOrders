using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CakeOrders.Web.ViewModels;

public sealed class CashRegisterViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "نام صندوق را وارد کنید.")]
    [StringLength(100, ErrorMessage = "نام صندوق حداکثر ۱۰۰ کاراکتر است.")]
    [Display(Name = "نام صندوق")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "شعبه را انتخاب کنید.")]
    [Display(Name = "شعبه")]
    public long BranchId { get; set; }

    [Display(Name = "دستگاه کارت‌خوان")]
    public long? PosTerminalId { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<SelectListItem> Branches { get; set; }
        = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> PosTerminals { get; set; }
        = Enumerable.Empty<SelectListItem>();
}