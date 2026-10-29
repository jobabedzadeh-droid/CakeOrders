namespace CakeOrders.Domain.Entities;

/// <summary>
/// A cashier workstation in a branch. Installation identity is stable and is
/// not based on the workstation's IP address.
/// </summary>
public sealed class CashRegister
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    /// <summary>The default POS terminal assigned to this workstation.</summary>
    public long? PosTerminalId { get; set; }

    public PosTerminal? PosTerminal { get; set; }

    /// <summary>Random installation identifier registered by SadadBridge.</summary>
    public string? BridgeInstallationId { get; set; }

    /// <summary>Only a cryptographic hash of the installation secret is stored.</summary>
    public string? BridgeSecretHash { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
