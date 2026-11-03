using System.ComponentModel.DataAnnotations;
using CakeOrders.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CakeOrders.Web.ViewModels.WorkshopManagement;

public sealed class WorkshopManagementIndexViewModel
{
    public IReadOnlyList<WorkshopManagementItemViewModel> Workshops { get; set; }
        = Array.Empty<WorkshopManagementItemViewModel>();
}

public sealed class WorkshopManagementItemViewModel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public IReadOnlyList<WorkshopMemberItemViewModel> Members { get; set; }
        = Array.Empty<WorkshopMemberItemViewModel>();

    public string ManagerName =>
        Members.FirstOrDefault(x => x.Role == WorkshopMemberRole.Manager)?.DisplayName
        ?? "تعیین نشده";

    public int EmployeeCount =>
        Members.Count(x => x.Role == WorkshopMemberRole.Employee);
}

public sealed class WorkshopMemberItemViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public WorkshopMemberRole Role { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AssignWorkshopMemberViewModel
{
    public long WorkshopId { get; set; }
    public string WorkshopName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;

    [Required(ErrorMessage = "انتخاب کاربر الزامی است.")]
    public string UserId { get; set; } = string.Empty;

    [EnumDataType(typeof(WorkshopMemberRole), ErrorMessage = "نقش انتخاب‌شده معتبر نیست.")]
    public WorkshopMemberRole Role { get; set; } = WorkshopMemberRole.Employee;

    public IReadOnlyList<SelectListItem> AvailableUsers { get; set; }
        = Array.Empty<SelectListItem>();
}
