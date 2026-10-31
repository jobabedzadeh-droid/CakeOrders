namespace CakeOrders.Domain.Entities;

public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public bool Succeeded { get; set; } = true;
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
}
