using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Web.ViewModels.Account;

public sealed class ProfileViewModel
{
    [Required(ErrorMessage = "نام و نام خانوادگی الزامی است.")]
    [MaxLength(150)]
    [Display(Name = "نام و نام خانوادگی")]
    public string DisplayName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "فرمت ایمیل صحیح نیست.")]
    [Display(Name = "ایمیل")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "شماره موبایل صحیح نیست.")]
    [Display(Name = "شماره موبایل")]
    public string? PhoneNumber { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}