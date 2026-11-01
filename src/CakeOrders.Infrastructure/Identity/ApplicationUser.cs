using CakeOrders.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    // Null for users who do not belong to a production workshop (e.g. operators/admins).
    public long? WorkshopId { get; set; }

    public Workshop? Workshop { get; set; }

    // A workshop user is either its manager or an employee.
    public WorkshopMemberRole? WorkshopRole { get; set; }
}
