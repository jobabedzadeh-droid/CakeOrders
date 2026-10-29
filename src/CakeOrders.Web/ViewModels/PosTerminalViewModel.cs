using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Web.ViewModels;

public sealed class PosTerminalViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "نام دستگاه را وارد کنید.")]
    [StringLength(100, ErrorMessage = "نام دستگاه حداکثر ۱۰۰ کاراکتر است.")]
    [Display(Name = "نام دستگاه")]
    public string Name { get; set; } = "صندوق شماره ۱";

    [Required(ErrorMessage = "آدرس IP را وارد کنید.")]
    [StringLength(45)]
    [RegularExpression(@"^(?:(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)$",
        ErrorMessage = "آدرس IPv4 معتبر وارد کنید.")]
    [Display(Name = "آدرس IP")]
    public string IpAddress { get; set; } = "192.16.1.119";

    [Range(1, 65535, ErrorMessage = "پورت باید بین ۱ تا ۶۵۵۳۵ باشد.")]
    [Display(Name = "پورت")]
    public int Port { get; set; } = 8888;

    [Required(ErrorMessage = "نوع اتصال را انتخاب کنید.")]
    [RegularExpression("^(LAN|Serial)$", ErrorMessage = "نوع اتصال نامعتبر است.")]
    [Display(Name = "نوع اتصال")]
    public string ConnectionType { get; set; } = "LAN";

    [StringLength(100, ErrorMessage = "شناسه پایانه حداکثر ۱۰۰ کاراکتر است.")]
    [Display(Name = "شناسه پایانه")]
    public string? TerminalCode { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;
}