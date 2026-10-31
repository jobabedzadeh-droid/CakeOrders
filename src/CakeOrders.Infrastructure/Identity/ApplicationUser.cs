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
}
