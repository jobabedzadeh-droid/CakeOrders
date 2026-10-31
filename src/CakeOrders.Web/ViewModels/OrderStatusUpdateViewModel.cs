using CakeOrders.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Web.ViewModels;

public sealed class OrderStatusUpdateViewModel
{
    public long OrderId { get; set; }

    [Required(ErrorMessage = "انتخاب وضعیت جدید الزامی است.")]
    public OrderStatus NewStatus { get; set; }

    [StringLength(1000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد.")]
    public string? Note { get; set; }
}