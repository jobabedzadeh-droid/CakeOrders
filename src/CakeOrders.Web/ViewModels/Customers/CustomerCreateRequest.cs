using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Web.ViewModels.Customers;

public sealed class CustomerCreateRequest
{
    [Required(ErrorMessage = "نام مشتری را وارد کنید.")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام خانوادگی مشتری را وارد کنید.")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Phone { get; set; }
}
