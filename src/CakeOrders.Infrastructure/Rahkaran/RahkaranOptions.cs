namespace CakeOrders.Infrastructure.Rahkaran;

public sealed class RahkaranOptions
{
    public const string SectionName = "Rahkaran";

    /// <summary>Base address of the Rahkaran service gateway, ending with the virtual application path if required.</summary>
    public string ServiceBaseUrl { get; set; } = string.Empty;

    /// <summary>HTTP timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>When false, integration calls are blocked even if the URL is configured.</summary>
    public bool Enabled { get; set; }
}
