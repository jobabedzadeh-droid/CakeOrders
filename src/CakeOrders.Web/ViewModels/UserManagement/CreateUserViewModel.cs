using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Web.ViewModels.UserManagement;

public sealed class CreateUserViewModel
{
    [Required(ErrorMessage = "نام و نام خانوادگی الزامی است.")]
    [MaxLength(150)]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام کاربری الزامی است.")]
    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "فرمت ایمیل صحیح نیست.")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "شماره موبایل صحیح نیست.")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "رمز عبور الزامی است.")]
    [MinLength(8, ErrorMessage = "رمز عبور باید حداقل ۸ کاراکتر باشد.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "انتخاب نقش الزامی است.")]
    public string Role { get; set; } = "User";
}