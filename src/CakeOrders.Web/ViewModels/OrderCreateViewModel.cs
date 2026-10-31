using System.ComponentModel.DataAnnotations;
using CakeOrders.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CakeOrders.Web.ViewModels;

public class OrderCreateViewModel
{
    [Display(Name = "نام مشتری")]
    [StringLength(200)]
    public string? CustomerName { get; set; }

    [Display(Name = "شماره تماس")]
    [Phone(ErrorMessage = "شماره تماس معتبر نیست.")]
    [StringLength(30)]
    public string? CustomerPhone { get; set; }

    [Display(Name = "نوع سفارش")]
    [Required(ErrorMessage = "نوع سفارش را انتخاب کنید.")]
    public string OrderType { get; set; } = "";

    [Display(Name = "نوع کیک")]
    [Required]
    public CakeType CakeType { get; set; } = CakeType.Cream;

    [Display(Name = "وزن درخواستی (کیلوگرم)")]
    [Range(typeof(decimal), "0.1", "100", ErrorMessage = "وزن باید بین ۰٫۱ تا ۱۰۰ کیلوگرم باشد.")]
    public decimal RequestedWeightKg { get; set; } = 1;

    [Display(Name = "قیمت هر کیلو")]
    [Range(typeof(decimal), "0", "1000000000")]
    public decimal PricePerKg { get; set; }

    [Display(Name = "نوع چاپ")]
    public string? PrintType { get; set; }

    [Display(Name = "رنگ زمینه")]
    [StringLength(100)]
    public string? BackgroundColor { get; set; }

    [Display(Name = "قالب کیک")]
    public string? MoldType { get; set; }

    [Display(Name = "فیلینگ موز")]
    public bool HasBananaFilling { get; set; }

    [Display(Name = "فیلینگ گردو")]
    public bool HasWalnutFilling { get; set; }

    [Display(Name = "محل نوشته")]
    public string? WritingLocation { get; set; }

    [Display(Name = "متن روی کیک")]
    [StringLength(500)]
    public string? CakeText { get; set; }

    [Display(Name = "توضیحات طراحی")]
    [StringLength(2000)]
    public string? DesignNotes { get; set; }

    [Display(Name = "تصویر نمونه کیک")]
    public IFormFile? SampleImage { get; set; }

    [Display(Name = "تصویر موردنظر برای چاپ")]
    public IFormFile? PrintImage { get; set; }

    [Display(Name = "هزینه ماکارون")]
    [Range(typeof(decimal), "0", "1000000000")]
    public decimal MacaronCost { get; set; }

    [Display(Name = "هزینه گل")]
    [Range(typeof(decimal), "0", "1000000000")]
    public decimal FlowerCost { get; set; }

    [Display(Name = "هزینه ماکت")]
    [Range(typeof(decimal), "0", "1000000000")]
    public decimal DummyCost { get; set; }

    [Display(Name = "هزینه چاپ")]
    [Range(typeof(decimal), "0", "1000000000")]
    public decimal PrintingCost { get; set; }

    [Display(Name = "هزینه ارسال")]
    [Range(typeof(decimal), "0", "1000000000")]
    public decimal ShippingCost { get; set; }

    [Display(Name = "سایر هزینه‌ها")]
    [Range(typeof(decimal), "0", "1000000000")]
    public decimal OtherCost { get; set; }

    [Display(Name = "تخفیف")]
    [Range(typeof(decimal), "0", "1000000000")]
    public decimal Discount { get; set; }

    [Display(Name = "مبلغ بیعانه درخواستی")]
    [Range(typeof(decimal), "0", "1000000000")]
    public decimal RequestedDepositAmount { get; set; }

    [Display(Name = "کد معرف")]
    [StringLength(50)]
    public string? ReferrerCode { get; set; }

    [Display(Name = "تاریخ و ساعت تحویل")]
    [Required(ErrorMessage = "زمان تحویل را وارد کنید.")]
    public DateTime DeliveryAt { get; set; } = DateTime.Now.AddHours(2);

    public decimal CalculatedTotal => (RequestedWeightKg * PricePerKg) + MacaronCost + FlowerCost + DummyCost + PrintingCost + ShippingCost + OtherCost - Discount;

    public IEnumerable<SelectListItem> OrderTypes => new[]
    {
        new SelectListItem("طبق نمونه", "طبق نمونه"),
        new SelectListItem("چاپ عکس", "چاپ عکس")
    };

    public IEnumerable<SelectListItem> PrintTypes => new[]
    {
        new SelectListItem("بدون چاپ", ""),
        new SelectListItem("A4", "A4"),
        new SelectListItem("A5", "A5")
    };

    public IEnumerable<SelectListItem> MoldTypes => new[]
    {
        "1 مستطیل", "2 گرد", "3 بیضی", "4 مربع", "5 استوانه‌ای", "6 مخروطی",
        "7 قلبی", "8 گرد بلند", "9 کوتاه", "10 سابله", "11 طبق نمونه"
    }.Select(x => new SelectListItem(x, x));

    public IEnumerable<SelectListItem> WritingLocations => new[]
    {
        new SelectListItem("دیواره", "دیواره"),
        new SelectListItem("سینی", "سینی"),
        new SelectListItem("روی کیک", "روی کیک")
    };
}