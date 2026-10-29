namespace CakeOrders.Domain.Entities;

/// <summary>
/// Configuration for a Sadad POS terminal associated with a cashier workstation.
/// </summary>
public sealed class PosTerminal
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public int Port { get; set; } = 8888;

    /// <summary>Supported values currently include LAN and Serial.</summary>
    public string ConnectionType { get; set; } = "LAN";

    public string? TerminalCode { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}