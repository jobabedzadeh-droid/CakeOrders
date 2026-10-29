using System.Security.Cryptography;
using System.Text;
using CakeOrders.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

/// <summary>
/// Small server-side configuration API for SadadBridge. Configure a long random
/// shared secret in SadadBridge:ApiKey and send it as a Bearer token from the bridge.
/// </summary>
[ApiController]
[Route("api/bridge")]
[AllowAnonymous]
public sealed class BridgeApiController(IConfiguration configuration, CakeOrdersDbContext db) : ControllerBase
{
    [HttpGet("terminals/{id:long}")]
    public async Task<IActionResult> GetTerminal(long id, CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
            return Unauthorized();

        var terminal = await db.PosTerminals.AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.IpAddress,
                x.Port,
                x.ConnectionType,
                x.TerminalCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        return terminal is null ? NotFound() : Ok(terminal);
    }

    private bool IsAuthorized()
    {
        var expected = configuration["SadadBridge:ApiKey"];
        var authorization = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(expected) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        var provided = authorization["Bearer ".Length..].Trim();
        var a = Encoding.UTF8.GetBytes(provided);
        var b = Encoding.UTF8.GetBytes(expected);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}