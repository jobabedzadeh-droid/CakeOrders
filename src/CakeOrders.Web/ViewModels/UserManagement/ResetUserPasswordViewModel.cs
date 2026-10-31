using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Web.ViewModels.UserManagement;

public sealed class ResetUserPasswordViewModel
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور جدید الزامی است.")]
    [MinLength(8, ErrorMessage = "رمز عبور باید حداقل ۸ کاراکتر باشد.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "تکرار رمز عبور الزامی است.")]
    [Compare(nameof(NewPassword), ErrorMessage = "رمزهای عبور مطابقت ندارند.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}