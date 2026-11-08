using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Data;
using CakeOrders.Infrastructure.Rahkaran;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CakeOrders.Web.Services;
using CakeOrders.Web.ViewModels.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CakeOrders.Web.Controllers;

[Authorize(Roles = "Admin,User")]
public sealed class CustomersController(
    CakeOrdersDbContext db,
    IRahkaranMasterDataClient rahkaran,
    IRahkaranCustomerClient rahkaranCustomer,
    IOptions<RahkaranOptions> rahkaranOptions,
    ILogger<CustomersController> logger) : Controller
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
            var detailFailureIds = new HashSet<long>();
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
                    if (detailedCustomer is null)
                    {
                        detailFailures++;
                        detailFailureIds.Add(indexedCustomer.Id);
                        remoteCustomers.Add(indexedCustomer);
                    }
                    else
                    {
                        remoteCustomers.Add(detailedCustomer);
                    }
                }
                catch (Exception ex)
                {
                    detailFailures++;
                    detailFailureIds.Add(indexedCustomer.Id);
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
                    var normalizedKey = mobileIsValid
                        ? phone
                        : "R" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(remoteId)))[..10];
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
                local.RahkaranPartyName = remote.PartyName?.Trim();
                local.Email = remote.Email?.Trim();
                local.Telephone = remote.Tel?.Trim();
                local.Fax = remote.Fax?.Trim();
                local.PostalCode = remote.ZipCode?.Trim();
                local.Details = remote.Details;
                local.RahkaranStateCode = remote.StateCode;
                local.RahkaranStateTitle = remote.StateTitle?.Trim();
                local.RahkaranTypeCode = remote.TypeCode;
                local.RahkaranTypeTitle = remote.TypeTitle?.Trim();

                // A failed detail request must never erase richer data from a previous sync.
                if (!detailFailureIds.Contains(remote.Id))
                {
                    local.RahkaranDLCode = remote.DLCode?.Trim();
                    local.CompanyName = remote.CompanyName?.Trim();
                    local.NationalId = remote.NationalId?.Trim();
                    local.EconomicCode = remote.EconomicCode?.Trim();
                    local.RegistrationCode = remote.RegistrationCode?.Trim();
                    local.WebsiteUrl = remote.WebsiteUrl?.Trim();
                    local.RahkaranStatus = remote.Status;
                    local.RahkaranPartyType = remote.PartyType ?? remote.Type;
                    local.RahkaranCreatedAt = remote.CreationDate?.UtcDateTime;
                    local.RahkaranLastModifiedAt = remote.LastModificationDate?.UtcDateTime;
                    local.RahkaranRawDataJson = JsonSerializer.Serialize(remote);
                    local.AddressesJson = remote.Addresses?.GetRawText();
                }
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
    public async Task<IActionResult> Search(string? term, CancellationToken cancellationToken)
    {
        if (await IsWorkshopMemberAsync(cancellationToken))
            return Forbid();

        term = NormalizeSearchTerm(term);
        if (term.Length < 2)
            return Json(new { results = Array.Empty<object>() });

        var matches = await db.Customers
            .AsNoTracking()
            .Where(x => x.IsActive &&
                (x.FirstName.Contains(term) ||
                 x.LastName.Contains(term) ||
                 x.Phone.Contains(term) ||
                 x.NormalizedPhone.Contains(term)))
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Take(12)
            .Select(x => new
            {
                x.Id,
                x.FirstName,
                x.LastName,
                x.Phone,
                IsRahkaranLinked = x.RahkaranCustomerId != null && x.RahkaranCustomerId != "",
                HasRahkaranDLCode = x.RahkaranDLCode != null && x.RahkaranDLCode != ""
            })
            .ToListAsync(cancellationToken);

        return Json(new
        {
            results = matches.Select(x => new
            {
                x.Id,
                x.FirstName,
                x.LastName,
                x.Phone,
                FullName = (x.FirstName + " " + x.LastName).Trim(),
                x.IsRahkaranLinked,
                x.HasRahkaranDLCode
            })
        });
    }

    private static string NormalizeSearchTerm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .Replace('ي', 'ی')
            .Replace('ك', 'ک')
            .Replace('٠', '0').Replace('١', '1').Replace('٢', '2')
            .Replace('٣', '3').Replace('٤', '4').Replace('٥', '5')
            .Replace('٦', '6').Replace('٧', '7').Replace('٨', '8')
            .Replace('٩', '9').Replace('۰', '0').Replace('۱', '1')
            .Replace('۲', '2').Replace('۳', '3').Replace('۴', '4')
            .Replace('۵', '5').Replace('۶', '6').Replace('۷', '7')
            .Replace('۸', '8').Replace('۹', '9');
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
                x.IsActive,
                IsRahkaranLinked = x.RahkaranCustomerId != null && x.RahkaranCustomerId != "",
                HasRahkaranDLCode = x.RahkaranDLCode != null && x.RahkaranDLCode != ""
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
                IsRahkaranLinked = customer.IsRahkaranLinked,
                HasRahkaranDLCode = customer.HasRahkaranDLCode,
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

        var hasMobile = !string.IsNullOrWhiteSpace(model.Phone);
        var normalizedPhone = string.Empty;
        if (hasMobile && !CustomerPhoneNormalizer.TryNormalize(model.Phone, out normalizedPhone))
            ModelState.AddModelError(nameof(model.Phone), "شماره موبایل معتبر نیست. شماره باید متعلق به موبایل ایران باشد.");

        if (!ModelState.IsValid)
            return BadRequest(new
            {
                success = false,
                message = ModelState.Values.SelectMany(x => x.Errors)
                    .Select(x => x.ErrorMessage)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "اطلاعات مشتری معتبر نیست."
            });

        Customer? customer = null;
        if (hasMobile)
        {
            customer = await db.Customers
                .SingleOrDefaultAsync(x => x.NormalizedPhone == normalizedPhone, cancellationToken);
        }
        else
        {
            // A phone-less customer with an unresolved remote-create marker is reused on retry.
            var phoneLessMatches = await db.Customers
                .Where(x => x.FirstName == model.FirstName.Trim() &&
                            x.LastName == model.LastName.Trim() &&
                            x.Phone == string.Empty &&
                            (x.RahkaranCustomerId != null ||
                             (x.RahkaranRawDataJson != null &&
                              (x.RahkaranRawDataJson.Contains("\"createAttemptPending\":true") ||
                               x.RahkaranRawDataJson.Contains("\"createAttemptFailed\":true")))))
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(2)
                .ToListAsync(cancellationToken);

            if (phoneLessMatches.Count > 1)
                return Conflict(new { success = false, message = "چند مشتری بدون شماره موبایل با این نام وجود دارد؛ برای جلوگیری از اتصال یا ایجاد مشتری اشتباه، لطفاً شماره موبایل وارد کنید یا ابتدا سوابق را بررسی کنید." });

            customer = phoneLessMatches.SingleOrDefault();
        }

        if (customer is not null && !customer.IsActive)
            return Conflict(new { success = false, message = "این مشخصات متعلق به مشتری غیرفعال است؛ ابتدا وضعیت مشتری را بررسی کنید." });

        var isNew = customer is null;
        if (customer is null)
        {
            customer = new Customer
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Phone = hasMobile ? normalizedPhone : string.Empty,
                NormalizedPhone = hasMobile
                    ? normalizedPhone
                    : "N" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Customers.Add(customer);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException) when (hasMobile)
            {
                customer = await db.Customers
                    .SingleOrDefaultAsync(x => x.NormalizedPhone == normalizedPhone, cancellationToken);
                if (customer is null)
                    throw;
                if (!customer.IsActive)
                    return Conflict(new { success = false, message = "این شماره متعلق به مشتری غیرفعال است." });
                isNew = false;
            }
        }

        var linkError = await EnsureRahkaranCustomerAsync(customer, cancellationToken);
        if (linkError is not null)
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                localCustomerSaved = true,
                message = linkError,
                customerId = customer.Id
            });

        return StatusCode(isNew ? StatusCodes.Status201Created : StatusCodes.Status200OK, new
        {
            success = true,
            alreadyExists = !isNew,
            customer = ToCustomerResponse(customer)
        });
    }

    private async Task<string?> EnsureRahkaranCustomerAsync(
        Customer customer,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(customer.RahkaranCustomerId))
            return await RefreshRahkaranCustomerDetailsAsync(customer, cancellationToken);

        // A previous request may have reached Rahkaran but lost its response.
        // Never issue a second PlaceCustomer call until that attempt is reconciled.
        if (customer.RahkaranRawDataJson?.Contains("\"createAttemptPending\":true", StringComparison.Ordinal) == true)
        {
            try
            {
                var pendingMatches = (await rahkaran.GetCustomersAsync(cancellationToken))
                    .Where(x =>
                    {
                        if (!string.IsNullOrWhiteSpace(customer.Phone))
                            return CustomerPhoneNormalizer.TryNormalize(x.Mobile, out var mobile) &&
                                   mobile == customer.Phone;

                        return string.Equals(x.FirstName?.Trim(), customer.FirstName, StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(x.LastName?.Trim(), customer.LastName, StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();

                if (pendingMatches.Count == 1)
                {
                    customer.RahkaranCustomerId = pendingMatches[0].Id.ToString(CultureInfo.InvariantCulture);
                    customer.RahkaranPartyId = pendingMatches[0].PartyId ?? pendingMatches[0].PartyRef;
                    customer.RahkaranRawDataJson = null;
                    customer.UpdatedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    return await RefreshRahkaranCustomerDetailsAsync(customer, cancellationToken);
                }

                if (pendingMatches.Count > 1)
                    return "چند مشتری مطابق درخواست قبلی در راهکاران پیدا شد؛ برای جلوگیری از اتصال اشتباه، تطبیق باید به‌صورت دستی انجام شود.";
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not reconcile pending Rahkaran customer creation for local customer {LocalCustomerId}.", customer.Id);
            }

            return "وضعیت درخواست قبلی ایجاد مشتری در راهکاران هنوز قابل تعیین نیست. برای جلوگیری از ایجاد مشتری تکراری، ارسال مجدد خودکار متوقف شده است؛ پس از بررسی راهکاران دوباره تلاش کنید.";
        }

        var settings = await db.RahkaranIntegrationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (settings?.SalesOfficeId is not long salesOfficeId ||
            settings.SalesCurrencyId is not long currencyId)
            return "برای ایجاد مشتری در راهکاران، «مرکز فروش» و «ارز فروش» را در تنظیمات راهکاران تکمیل کنید.";

        // Claim this local customer row before any remote create call. The SQL row lock
        // prevents two simultaneous requests from creating two Rahkaran customers.
        await using (var claimTransaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.Customers
                .FromSqlInterpolated($"SELECT * FROM [Customers] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {customer.Id}")
                .SingleAsync(cancellationToken);
            await db.Entry(customer).ReloadAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(customer.RahkaranCustomerId))
            {
                await claimTransaction.CommitAsync(cancellationToken);
                return await RefreshRahkaranCustomerDetailsAsync(customer, cancellationToken);
            }

            if (customer.RahkaranRawDataJson?.Contains("\"createAttemptPending\":true", StringComparison.Ordinal) == true)
            {
                await claimTransaction.CommitAsync(cancellationToken);
                return "درخواست ایجاد این مشتری در حال انجام یا در انتظار تطبیق است؛ برای جلوگیری از ثبت تکراری، درخواست جدید ارسال نشد. چند لحظه بعد دوباره تلاش کنید.";
            }

            customer.RahkaranRawDataJson = "{\"createAttemptPending\":true}";
            customer.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await claimTransaction.CommitAsync(cancellationToken);
        }

        // Reconcile by mobile before creating a new remote record.
        if (!string.IsNullOrWhiteSpace(customer.Phone))
        {
            IReadOnlyList<RahkaranCustomerDto> remoteCustomers;
            try
            {
                remoteCustomers = await rahkaran.GetCustomersAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not search Rahkaran customers before creating customer {LocalCustomerId}.", customer.Id);
                customer.RahkaranRawDataJson = "{\"createAttemptFailed\":true}";
                await db.SaveChangesAsync(cancellationToken);
                return "جست‌وجوی مشتری در راهکاران ناموفق بود؛ هیچ درخواست ایجاد مشتری ارسال نشد. پس از بررسی ارتباط دوباره تلاش کنید.";
            }

            var matches = remoteCustomers
                .Where(x => CustomerPhoneNormalizer.TryNormalize(x.Mobile, out var mobile) &&
                            mobile == customer.Phone)
                .ToList();

            if (matches.Count > 1)
                return "چند مشتری با این شماره در راهکاران پیدا شد؛ برای جلوگیری از اتصال اشتباه، انتخاب خودکار انجام نشد.";

            if (matches.Count == 1)
            {
                customer.RahkaranCustomerId = matches[0].Id.ToString(CultureInfo.InvariantCulture);
                customer.RahkaranPartyId = matches[0].PartyId ?? matches[0].PartyRef;
                customer.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return await RefreshRahkaranCustomerDetailsAsync(customer, cancellationToken);
            }
        }

        RahkaranCreateCustomerResult created;
        try
        {
            created = await rahkaranCustomer.PlaceCustomerAsync(
                new RahkaranPlaceCustomerRequest
                {
                    CurrencyRef = currencyId,
                    DeclarationDate = DateTimeOffset.UtcNow,
                    SalesOfficeIDs = [salesOfficeId],
                    Party = new RahkaranPartyData
                    {
                        Type = 0,
                        FirstName = customer.FirstName,
                        LastName = customer.LastName,
                        Mobile = string.IsNullOrWhiteSpace(customer.Phone) ? null : customer.Phone
                    }
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rahkaran PlaceCustomer failed for local customer {LocalCustomerId}; create attempt remains protected from duplicate retries.", customer.Id);
            return "ارتباط با راهکاران هنگام ایجاد مشتری قطع شد. وضعیت درخواست نامشخص است و برای جلوگیری از ثبت تکراری، ارسال مجدد خودکار انجام نمی‌شود؛ ابتدا راهکاران را بررسی کنید.";
        }

        if (!created.Succeeded || created.CustomerId is not long remoteCustomerId)
        {
            if (created.HttpStatusCode is >= 400 and < 500)
            {
                customer.RahkaranRawDataJson = "{\"createAttemptFailed\":true}";
                await db.SaveChangesAsync(cancellationToken);
            }

            logger.LogWarning(
                "Rahkaran customer creation was not confirmed. LocalCustomerId={LocalCustomerId}, HttpStatusCode={StatusCode}.",
                customer.Id,
                created.HttpStatusCode);
            return created.HttpStatusCode is >= 400 and < 500
                ? "راهکاران اطلاعات مشتری را نپذیرفت. تنظیمات و اطلاعات ارسالی را بررسی کنید."
                : "پاسخ قطعی از راهکاران برای ایجاد مشتری دریافت نشد؛ برای جلوگیری از ایجاد رکورد تکراری، ارسال مجدد خودکار متوقف شده است.";
        }

        // Save the remote identifiers immediately. A later DLCode/detail failure must
        // never cause another PlaceCustomer call.
        customer.RahkaranCustomerId = remoteCustomerId.ToString(CultureInfo.InvariantCulture);
        customer.RahkaranPartyId = created.PartyId;
        customer.RahkaranRawDataJson = null;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return await RefreshRahkaranCustomerDetailsAsync(customer, cancellationToken);
    }

    private async Task<string?> RefreshRahkaranCustomerDetailsAsync(
        Customer customer,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(customer.RahkaranCustomerId, NumberStyles.None,
                CultureInfo.InvariantCulture, out var remoteCustomerId))
            return "شناسه مشتری راهکاران معتبر نیست؛ اطلاعات اتصال باید بررسی شود.";

        try
        {
            var details = await rahkaran.GetCustomerChangesAsync(
                new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero),
                DateTimeOffset.UtcNow,
                remoteCustomerId,
                1,
                100,
                cancellationToken);

            var remote = details.FirstOrDefault(x => x.Id == remoteCustomerId)
                ?? details.FirstOrDefault();
            if (remote is null)
                return "مشتری در راهکاران ایجاد/شناسایی شد و شناسه آن ذخیره شد، اما دریافت جزئیات و کد تفصیلی هنوز موفق نبود. دوباره تلاش کنید.";

            customer.RahkaranPartyId = remote.PartyId ?? remote.PartyRef ?? customer.RahkaranPartyId;
            customer.RahkaranCustomerNumber = remote.Number?.Trim();
            customer.RahkaranSecondCode = remote.SecondCode?.Trim();
            customer.RahkaranDLCode = remote.DLCode?.Trim();
            customer.RahkaranPartyName = remote.PartyName?.Trim();
            customer.CompanyName = remote.CompanyName?.Trim();
            customer.Email = remote.Email?.Trim();
            customer.Telephone = remote.Tel?.Trim();
            customer.Fax = remote.Fax?.Trim();
            customer.PostalCode = remote.ZipCode?.Trim();
            customer.Details = remote.Details;
            customer.NationalId = remote.NationalId?.Trim();
            customer.EconomicCode = remote.EconomicCode?.Trim();
            customer.RegistrationCode = remote.RegistrationCode?.Trim();
            customer.WebsiteUrl = remote.WebsiteUrl?.Trim();
            customer.RahkaranStateCode = remote.StateCode;
            customer.RahkaranStateTitle = remote.StateTitle?.Trim();
            customer.RahkaranTypeCode = remote.Type;
            customer.RahkaranTypeTitle = remote.TypeTitle?.Trim();
            customer.RahkaranStatus = remote.Status;
            customer.RahkaranPartyType = remote.PartyType ?? remote.Type;
            customer.RahkaranCreatedAt = remote.CreationDate?.UtcDateTime;
            customer.RahkaranLastModifiedAt = remote.LastModificationDate?.UtcDateTime;
            customer.RahkaranRawDataJson = JsonSerializer.Serialize(remote);
            customer.AddressesJson = remote.Addresses?.GetRawText();
            customer.LastRahkaranSyncAtUtc = DateTime.UtcNow;
            customer.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(customer.RahkaranDLCode)
                ? "مشتری در راهکاران ایجاد و شناسه‌های آن ذخیره شد، اما کد تفصیلی هنوز در پاسخ راهکاران موجود نیست. با تلاش مجدد فقط دریافت جزئیات تکرار می‌شود و مشتری جدیدی ایجاد نخواهد شد."
                : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not retrieve Rahkaran details/DLCode for local customer {LocalCustomerId}, remote customer {RahkaranCustomerId}.",
                customer.Id, customer.RahkaranCustomerId);
            return "شناسه مشتری راهکاران ذخیره شد، اما دریافت کد تفصیلی ناموفق بود. دوباره تلاش کنید؛ مشتری تکراری ایجاد نخواهد شد.";
        }
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
        IsRahkaranLinked = !string.IsNullOrWhiteSpace(customer.RahkaranCustomerId),
        HasRahkaranDLCode = !string.IsNullOrWhiteSpace(customer.RahkaranDLCode),
        FullName = customer.FullName
    };
}
