using System.ComponentModel.DataAnnotations;

namespace CakeOrders.Domain.Entities;

/// <summary>
/// An in-app notification addressed to one specific user.
/// Each recipient receives an independent row so read state is never shared.
/// </summary>
public sealed class UserNotification
{
    public long Id { get; set; }

    [MaxLength(450)]
    public string RecipientUserId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Link { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAtUtc { get; set; }
}
