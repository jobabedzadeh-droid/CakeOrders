using System.Net;
using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class PosTerminalsController(CakeOrdersDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var terminals = await db.PosTerminals.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return View(terminals);
    }

    [HttpGet]
    public IActionResult Create() => View("Edit", new PosTerminalViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PosTerminalViewModel model, CancellationToken cancellationToken)
    {
        ValidateIp(model.IpAddress);
        if (!ModelState.IsValid)
            return View("Edit", model);

        var entity = new PosTerminal
        {
            Name = model.Name.Trim(),
            IpAddress = model.IpAddress.Trim(),
            Port = model.Port,
            ConnectionType = model.ConnectionType,
            TerminalCode = string.IsNullOrWhiteSpace(model.TerminalCode) ? null : model.TerminalCode.Trim(),
            IsActive = model.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.PosTerminals.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "تنظیمات دستگاه کارت‌خوان ذخیره شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
    {
        var entity = await db.PosTerminals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound();

        return View(new PosTerminalViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            IpAddress = entity.IpAddress,
            Port = entity.Port,
            ConnectionType = entity.ConnectionType,
            TerminalCode = entity.TerminalCode,
            IsActive = entity.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, PosTerminalViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
            return BadRequest();

        ValidateIp(model.IpAddress);
        if (!ModelState.IsValid)
            return View(model);

        var entity = await db.PosTerminals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound();

        entity.Name = model.Name.Trim();
        entity.IpAddress = model.IpAddress.Trim();
        entity.Port = model.Port;
        entity.ConnectionType = model.ConnectionType;
        entity.TerminalCode = string.IsNullOrWhiteSpace(model.TerminalCode) ? null : model.TerminalCode.Trim();
        entity.IsActive = model.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "تنظیمات دستگاه کارت‌خوان به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(long id, CancellationToken cancellationToken)
    {
        var entity = await db.PosTerminals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound();

        entity.IsActive = !entity.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = entity.IsActive ? "دستگاه فعال شد." : "دستگاه غیرفعال شد.";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateIp(string? ip)
    {
        if (!IPAddress.TryParse(ip, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            ModelState.AddModelError(nameof(PosTerminalViewModel.IpAddress), "آدرس IPv4 معتبر وارد کنید.");
    }
}