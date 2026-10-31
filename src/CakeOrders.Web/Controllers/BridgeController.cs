using System.Security.Claims;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[ApiController]
[Route("api/bridge")]
[Authorize(Policy = BridgeAuthenticationDefaults.Policy)]
public sealed class BridgeController(CakeOrdersDbContext db) : ControllerBase
{
    /// <summary>
    /// Returns the active POS configuration assigned to the authenticated bridge installation.
    /// </summary>
    [HttpGet("terminals/current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentTerminal(
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";

        var cashRegisterIdValue = User.FindFirstValue(
            BridgeAuthenticationDefaults.CashRegisterIdClaim);
        var installationId = User.FindFirstValue(
            BridgeAuthenticationDefaults.InstallationIdClaim);

        if (!long.TryParse(cashRegisterIdValue, out var cashRegisterId) ||
            string.IsNullOrWhiteSpace(installationId))
        {
            return Unauthorized(new
            {
                error = "invalid_bridge_identity"
            });
        }

        // Re-read current state so a disabled/reassigned terminal is not served
        // based only on the state observed during authentication.
        var register = await db.CashRegisters
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.PosTerminal)
            .SingleOrDefaultAsync(
                x => x.Id == cashRegisterId &&
                     x.BridgeInstallationId == installationId,
                cancellationToken);

        if (register is null ||
            !register.IsActive ||
            !register.Branch.IsActive ||
            register.PosTerminal is null ||
            !register.PosTerminal.IsActive)
        {
            return Unauthorized(new
            {
                error = "bridge_or_terminal_inactive",
                message = "The bridge installation or assigned POS terminal is inactive."
            });
        }

        return Ok(new
        {
            installationId,
            cashRegister = new
            {
                id = register.Id,
                name = register.Name,
                branchId = register.BranchId,
                branchName = register.Branch.Name
            },
            terminal = new
            {
                id = register.PosTerminal.Id,
                name = register.PosTerminal.Name,
                ipAddress = register.PosTerminal.IpAddress,
                port = register.PosTerminal.Port,
                connectionType = register.PosTerminal.ConnectionType,
                terminalCode = register.PosTerminal.TerminalCode
            }
        });
    }
}
