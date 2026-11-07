using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Web.Services;
using CakeOrders.Web.ViewModels.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin,User")]
public sealed class CustomersController(CakeOrdersDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> LookupByPhone(string? phone, CancellationToken cancellationToken)
    {
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

        if (!CustomerPhoneNormalizer.TryNormalize(phone, out var normalizedPhone))
            return Json(new { found = false, invalid = true });

        var customer = await db.Customers
            .AsNoTracking()
            .Where(x => x.NormalizedPhone == normalizedPhone)
            .Select(x => new
            {
                x.Id,
                x.FirstName,
                x.LastName,
                x.Phone,
                x.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (customer is null)
            return Json(new { found = false, invalid = false });

        if (!customer.IsActive)
            return Json(new { found = false, invalid = false, inactive = true });

        return Json(new
        {
            found = true,
            customer = new
            {
                customer.Id,
                customer.FirstName,
                customer.LastName,
                customer.Phone,
                FullName = (customer.FirstName + " " + customer.LastName).Trim()
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerCreateRequest model, CancellationToken cancellationToken)
    {
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

        if (!CustomerPhoneNormalizer.TryNormalize(model.Phone, out var normalizedPhone))
            ModelState.AddModelError(nameof(model.Phone), "شماره موبایل معتبر نیست. شماره باید متعلق به موبایل ایران باشد.");

        if (!ModelState.IsValid)
            return BadRequest(new
            {
                success = false,
                message = ModelState.Values.SelectMany(x => x.Errors)
                    .Select(x => x.ErrorMessage)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "اطلاعات مشتری معتبر نیست."
            });

        var existing = await db.Customers
            .SingleOrDefaultAsync(x => x.NormalizedPhone == normalizedPhone, cancellationToken);

        if (existing is not null)
        {
            if (!existing.IsActive)
                return Conflict(new { success = false, message = "این شماره متعلق به مشتری غیرفعال است؛ ابتدا وضعیت مشتری را بررسی کنید." });

            return Json(new
            {
                success = true,
                alreadyExists = true,
                customer = ToCustomerResponse(existing)
            });
        }

        var customer = new Customer
        {
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Phone = normalizedPhone,
            NormalizedPhone = normalizedPhone,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Customers.Add(customer);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have registered the same phone after the lookup.
            var concurrentCustomer = await db.Customers
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.NormalizedPhone == normalizedPhone, cancellationToken);

            if (concurrentCustomer is null)
                throw;

            if (!concurrentCustomer.IsActive)
                return Conflict(new { success = false, message = "این شماره متعلق به مشتری غیرفعال است؛ ابتدا وضعیت مشتری را بررسی کنید." });

            return Json(new
            {
                success = true,
                alreadyExists = true,
                customer = ToCustomerResponse(concurrentCustomer)
            });
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            success = true,
            alreadyExists = false,
            customer = ToCustomerResponse(customer)
        });
    }

    private async Task<bool> IsWorkshopMemberAsync(CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin"))
            return false;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return await db.Users.AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.IsActive && x.WorkshopId != null, cancellationToken);
    }

    private static object ToCustomerResponse(Customer customer) => new
    {
        customer.Id,
        customer.FirstName,
        customer.LastName,
        customer.Phone,
        FullName = customer.FullName
    };
}
