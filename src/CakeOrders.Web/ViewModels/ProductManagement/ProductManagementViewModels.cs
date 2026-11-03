using System.ComponentModel.DataAnnotations;
using CakeOrders.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CakeOrders.Web.ViewModels.ProductManagement;

public sealed class ProductListItemViewModel
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public ProductUnit Unit { get; set; }
    public bool IsActive { get; set; }
    public int RoutingCount { get; set; }
}

public sealed class ProductEditViewModel
{
    public long Id { get; set; }

    [Display(Name = "گروه محصول")]
    [Range(1, long.MaxValue, ErrorMessage = "گروه محصول را انتخاب کنید.")]
    public long CategoryId { get; set; }

    [Display(Name = "کد محصول")]
    [Required(ErrorMessage = "کد محصول الزامی است.")]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "نام محصول")]
    [Required(ErrorMessage = "نام محصول الزامی است.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "واحد اندازه‌گیری")]
    [EnumDataType(typeof(ProductUnit))]
    public ProductUnit Unit { get; set; } = ProductUnit.Piece;

    [Display(Name = "توضیحات")]
    [StringLength(500)]
    public string? Description { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    public List<ProductBranchRoutingViewModel> BranchRoutings { get; set; } = new();

    public IEnumerable<SelectListItem> Categories { get; set; } = Enumerable.Empty<SelectListItem>();
}

public sealed class ProductBranchRoutingViewModel
{
    public long BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;

    [Display(Name = "کارگاه پیش‌فرض")]
    public long? WorkshopId { get; set; }

    public List<SelectListItem> Workshops { get; set; } = new();
}

public sealed class ProductCategoryEditViewModel
{
    public long Id { get; set; }

    [Display(Name = "نام گروه")]
    [Required(ErrorMessage = "نام گروه الزامی است.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;
}
