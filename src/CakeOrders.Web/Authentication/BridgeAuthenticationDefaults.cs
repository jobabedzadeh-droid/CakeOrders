namespace CakeOrders.Web.Authentication;

public static class BridgeAuthenticationDefaults
{
    public const string Scheme = "SadadBridge";
    public const string Policy = "SadadBridgeOnly";

    public const string InstallationIdHeader = "X-Bridge-Installation-Id";
    public const string SecretHeader = "X-Bridge-Secret";

    public const string CashRegisterIdClaim = "bridge:cash_register_id";
    public const string InstallationIdClaim = "bridge:installation_id";
}
