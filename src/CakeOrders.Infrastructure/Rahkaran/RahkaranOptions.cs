namespace CakeOrders.Infrastructure.Rahkaran;

public sealed class RahkaranOptions
{
    public const string SectionName = "Rahkaran";
    public string ServiceBaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public bool Enabled { get; set; }
    public bool MasterDataEnabled { get; set; }
    public bool SalesRequestDispatchEnabled { get; set; }
}