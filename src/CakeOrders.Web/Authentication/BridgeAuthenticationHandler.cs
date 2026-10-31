using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using CakeOrders.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CakeOrders.Web.Authentication;

/// <summary>
/// Authenticates a registered SadadBridge installation using its installation ID
/// and one-time-issued secret. Only the SHA-256 hash is stored in the database.
/// </summary>
public sealed class BridgeAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    CakeOrdersDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(
                BridgeAuthenticationDefaults.InstallationIdHeader,
                out var installationIdValues) ||
            !Request.Headers.TryGetValue(
                BridgeAuthenticationDefaults.SecretHeader,
                out var secretValues))
        {
            return AuthenticateResult.NoResult();
        }

        var installationId = installationIdValues.ToString();
        var secret = secretValues.ToString();

        // Generated values have fixed formats: GUID "N" and 32-byte Base64Url.
        if (installationId.Length != 32 ||
            !Guid.TryParseExact(installationId, "N", out _) ||
            secret.Length != 43)
        {
            return AuthenticateResult.Fail("Invalid bridge credentials.");
        }

        var register = await db.CashRegisters
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.PosTerminal)
            .SingleOrDefaultAsync(
                x => x.BridgeInstallationId == installationId,
                Context.RequestAborted);

        if (register is null ||
            !register.IsActive ||
            !register.Branch.IsActive ||
            register.PosTerminal is null ||
            !register.PosTerminal.IsActive ||
            string.IsNullOrWhiteSpace(register.BridgeSecretHash))
        {
            return AuthenticateResult.Fail("Invalid bridge credentials.");
        }

        byte[] storedHash;
        try
        {
            storedHash = Convert.FromHexString(register.BridgeSecretHash);
        }
        catch (FormatException)
        {
            return AuthenticateResult.Fail("Invalid bridge credentials.");
        }

        if (storedHash.Length != 32)
            return AuthenticateResult.Fail("Invalid bridge credentials.");

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));

        if (!CryptographicOperations.FixedTimeEquals(storedHash, suppliedHash))
            return AuthenticateResult.Fail("Invalid bridge credentials.");

        var claims = new[]
        {
            new Claim(
                BridgeAuthenticationDefaults.CashRegisterIdClaim,
                register.Id.ToString()),
            new Claim(
                BridgeAuthenticationDefaults.InstallationIdClaim,
                installationId)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(
        AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";

        return Response.WriteAsJsonAsync(new
        {
            error = "unauthorized",
            message = "Bridge authentication failed."
        });
    }

    protected override Task HandleForbiddenAsync(
        AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json";

        return Response.WriteAsJsonAsync(new
        {
            error = "forbidden",
            message = "The bridge is not allowed to access this resource."
        });
    }
}
