using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Web.ViewModels.UserManagement;

public sealed class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;

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

    [Required(ErrorMessage = "انتخاب نقش الزامی است.")]
    public string Role { get; set; } = "User";

    public bool IsActive { get; set; }
}