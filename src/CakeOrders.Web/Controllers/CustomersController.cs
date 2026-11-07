using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Rahkaran;
using System.Text.Json;
using System.Globalization;
using CakeOrders.Web.Services;
using CakeOrders.Web.ViewModels.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin,User")]
public sealed class CustomersController(CakeOrdersDbContext db, IRahkaranMasterDataClient rahkaran) : Controller
{
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncFromRahkaran(CancellationToken cancellationToken)
    {
        try
        {
            // Full read-only import. No customer is created or modified in Rahkaran.
            var customerIndex = await rahkaran.GetCustomersAsync(cancellationToken);
            if (customerIndex.Count == 0)
            {
                TempData["Error"] = "راهکاران هیچ مشتری‌ای برنگرداند؛ برای جلوگیری از همگام‌سازی اشتباه، اطلاعات داخلی تغییر نکرد.";
                return RedirectToAction("Index", "RahkaranSettings");
            }

            // GetCustomers is the lightweight index. GetCustomerList returns the richer
            // customer record (DLCode, national/economic codes, company data and Addresses).
            // Its documented MasterEntityID is the customer ID, so enrich each indexed row.
            var remoteCustomers = new List<RahkaranCustomerDto>(customerIndex.Count);
            var detailFailures = 0;
            var fromDate = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var toDate = DateTimeOffset.UtcNow;

            foreach (var indexedCustomer in customerIndex.Where(x => x.Id > 0))
            {
                try
                {
                    var details = await rahkaran.GetCustomerChangesAsync(
                        fromDate, toDate, indexedCustomer.Id, 1, 100, cancellationToken);
                    var detailedCustomer = details.FirstOrDefault(x => x.Id == indexedCustomer.Id)
                        ?? details.FirstOrDefault();
                    remoteCustomers.Add(detailedCustomer ?? indexedCustomer);
                }
                catch (Exception ex)
                {
                    detailFailures++;
                    HttpContext.RequestServices.GetRequiredService<ILogger<CustomersController>>()
                        .LogWarning(ex,
                            "Could not retrieve detailed Rahkaran customer data for CustomerID={CustomerId}; the basic customer record will be retained.",
                            indexedCustomer.Id);
                    remoteCustomers.Add(indexedCustomer);
                }
            }

            var localCustomers = await db.Customers.ToListAsync(cancellationToken);
            var byRahkaranId = localCustomers
                .Where(x => !string.IsNullOrWhiteSpace(x.RahkaranCustomerId))
                .GroupBy(x => x.RahkaranCustomerId!)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
            var byPhone = localCustomers
                .Where(x => x.NormalizedPhone.Length == 11 && x.NormalizedPhone.StartsWith("09"))
                .GroupBy(x => x.NormalizedPhone)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

            var added = 0;
            var updated = 0;
            var skipped = 0;
            var now = DateTime.UtcNow;

            foreach (var remote in remoteCustomers.Where(x => x.Id > 0))
            {
                var remoteId = remote.Id.ToString(CultureInfo.InvariantCulture);
                Customer? local = null;

                if (!byRahkaranId.TryGetValue(remoteId, out local) &&
                    CustomerPhoneNormalizer.TryNormalize(remote.Mobile, out var normalizedPhone))
                {
                    byPhone.TryGetValue(normalizedPhone, out local);
                }

                if (local is not null &&
                    !string.IsNullOrWhiteSpace(local.RahkaranCustomerId) &&
                    !string.Equals(local.RahkaranCustomerId, remoteId, StringComparison.OrdinalIgnoreCase))
                {
                    // Never merge two different Rahkaran identities solely because a phone matches.
                    skipped++;
                    continue;
                }

                var mobileIsValid = CustomerPhoneNormalizer.TryNormalize(remote.Mobile, out var phone);
                if (local is null)
                {
                    // Customers without a valid mobile are still retained. The R-prefixed key
                    // is deliberately outside the Iranian-mobile format and cannot match lookups.
                    var normalizedKey = mobileIsValid ? phone : "R" + remote.Id.ToString(CultureInfo.InvariantCulture);
                    if (byPhone.ContainsKey(normalizedKey))
                    {
                        skipped++;
                        continue;
                    }

                    local = new Customer
                    {
                        FirstName = remote.FirstName?.Trim() ?? string.Empty,
                        LastName = remote.LastName?.Trim() ?? string.Empty,
                        Phone = mobileIsValid ? phone : string.Empty,
                        NormalizedPhone = normalizedKey,
                        IsActive = true,
                        CreatedAtUtc = now
                    };
                    db.Customers.Add(local);
                    localCustomers.Add(local);
                    byPhone[normalizedKey] = local;
                    added++;
                }
                else
                {
                    updated++;
                    if (!string.IsNullOrWhiteSpace(remote.FirstName))
                        local.FirstName = remote.FirstName.Trim();
                    if (!string.IsNullOrWhiteSpace(remote.LastName))
                        local.LastName = remote.LastName.Trim();

                    if (mobileIsValid)
                    {
                        // Update the indexed mobile only when it is not owned by another local row.
                        if (!byPhone.TryGetValue(phone, out var phoneOwner) || phoneOwner.Id == local.Id)
                        {
                            byPhone.Remove(local.NormalizedPhone);
                            local.Phone = phone;
                            local.NormalizedPhone = phone;
                            byPhone[phone] = local;
                        }
                    }
                }

                local.RahkaranCustomerId = remoteId;
                local.RahkaranPartyId = remote.PartyId ?? remote.PartyRef;
                local.RahkaranCustomerNumber = remote.Number?.Trim();
                local.RahkaranSecondCode = remote.SecondCode?.Trim();
                local.RahkaranDLCode = remote.DLCode?.Trim();
                local.RahkaranPartyName = remote.PartyName?.Trim();
                local.CompanyName = remote.CompanyName?.Trim();
                local.Email = remote.Email?.Trim();
                local.Telephone = remote.Tel?.Trim();
                local.Fax = remote.Fax?.Trim();
                local.PostalCode = remote.ZipCode?.Trim();
                local.AddressesJson = remote.Addresses?.GetRawText();
                local.Details = remote.Details;
                local.NationalId = remote.NationalId?.Trim();
                local.EconomicCode = remote.EconomicCode?.Trim();
                local.RegistrationCode = remote.RegistrationCode?.Trim();
                local.WebsiteUrl = remote.WebsiteUrl?.Trim();
                local.RahkaranStateCode = remote.StateCode;
                local.RahkaranStateTitle = remote.StateTitle?.Trim();
                local.RahkaranTypeCode = remote.TypeCode;
                local.RahkaranTypeTitle = remote.TypeTitle?.Trim();
                local.RahkaranStatus = remote.Status;
                local.RahkaranPartyType = remote.PartyType ?? remote.Type;
                local.RahkaranCreatedAt = remote.CreationDate?.UtcDateTime;
                local.RahkaranLastModifiedAt = remote.LastModificationDate?.UtcDateTime;
                local.RahkaranRawDataJson = JsonSerializer.Serialize(remote);
                local.LastRahkaranSyncAtUtc = now;
                local.UpdatedAtUtc = now;

                if (!string.IsNullOrWhiteSpace(local.RahkaranCustomerId))
                    byRahkaranId[remoteId] = local;
            }

            await db.SaveChangesAsync(cancellationToken);

            TempData["Success"] =
                $"همگام‌سازی مشتریان راهکاران انجام شد؛ {added} مشتری جدید، {updated} مشتری به‌روزرسانی و {skipped} مورد به‌دلیل تعارض هویتی/شماره همراه رد شد. دریافت جزئیات تکمیلی برای {detailFailures} مشتری ناموفق بود و اطلاعات پایه آن‌ها ذخیره شد. هیچ داده‌ای در راهکاران تغییر نکرد.";
        }
        catch (Exception ex)
        {
            // Keep details in server logs; never expose service responses or credentials in the UI.
            HttpContext.RequestServices.GetRequiredService<ILogger<CustomersController>>()
                .LogError(ex, "Read-only Rahkaran customer synchronization failed.");
            TempData["Error"] = "همگام‌سازی مشتریان راهکاران ناموفق بود؛ اطلاعات قبلی حفظ شدند. جزئیات در لاگ برنامه ثبت شده است.";
        }

        return RedirectToAction("Index", "RahkaranSettings");
    }

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
