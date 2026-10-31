using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Web.ViewModels.Account;

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "رمز عبور فعلی الزامی است.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور فعلی")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور جدید الزامی است.")]
    [MinLength(8, ErrorMessage = "رمز عبور جدید باید حداقل ۸ کاراکتر باشد.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور جدید")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "تکرار رمز عبور جدید الزامی است.")]
    [Compare(nameof(NewPassword), ErrorMessage = "تکرار رمز عبور با رمز جدید مطابقت ندارد.")]
    [DataType(DataType.Password)]
    [Display(Name = "تکرار رمز عبور جدید")]
    public string ConfirmPassword { get; set; } = string.Empty;
}