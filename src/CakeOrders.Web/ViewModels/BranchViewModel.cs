using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Web.ViewModels;

public sealed class BranchViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "نام شعبه الزامی است.")]
    [MaxLength(100, ErrorMessage = "نام شعبه حداکثر ۱۰۰ کاراکتر است.")]
    [Display(Name = "نام شعبه")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300, ErrorMessage = "آدرس حداکثر ۳۰۰ کاراکتر است.")]
    [Display(Name = "آدرس شعبه")]
    public string? Address { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;
}