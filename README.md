# CakeOrders

سامانه مدیریت سفارش‌های قنادی با ASP.NET Core MVC، .NET 10، EF Core و SQL Server. پروژه در مرحله توسعه اولیه است و هنوز برای استفاده عملیاتی آماده نیست.

## Architecture
- `CakeOrders.Domain`: مدل‌ها و قواعد دامنه
- `CakeOrders.Application`: لایه کاربردی
- `CakeOrders.Infrastructure`: EF Core، نگاشت‌ها و Migrationها
- `CakeOrders.Web`: رابط وب MVC

## Current features
- داشبورد فارسی و راست‌چین
- فرم اولیه ثبت سفارش حضوری و آنلاین
- اعتبارسنجی ورودی‌ها و محاسبه مبلغ نهایی سمت سرور
- ذخیره سفارش و تاریخچه وضعیت با EF Core
- فهرست سفارش‌ها و صفحه جزئیات

## Requirements
- .NET 10 SDK
- SQL Server
- EF Core CLI (`dotnet-ef`) نسخه 10

## Database schema (Code First)
مدل‌های C# و `CakeOrdersDbContext` منبع اصلی تعریف ساختار دیتابیس هستند. تغییرات ساختار باید با EF Core Migration ثبت شوند؛ اسکریپت دستی SQL روش اصلی مدیریت schema پروژه نیست.

برای ساخت اولین Migration از ریشه مخزن:
```powershell
dotnet tool install --global dotnet-ef --version 10.0.0
dotnet restore CakeOrders.sln
dotnet ef migrations add InitialCreate --project src/CakeOrders.Infrastructure --startup-project src/CakeOrders.Web --output-dir Data/Migrations
dotnet ef database update --project src/CakeOrders.Infrastructure --startup-project src/CakeOrders.Web
```

اگر ابزار `dotnet-ef` از قبل نصب است، به‌جای نصب دوباره از `dotnet tool update --global dotnet-ef --version 10.0.0` استفاده کنید. دستور `database update` دیتابیس را روی SQL Server مشخص‌شده در connection string ایجاد یا به‌روزرسانی می‌کند. قبل از اجرا روی سرور، connection string و مجوزهای دیتابیس را با تنظیمات محیط خود هماهنگ کنید.

## Run locally
```powershell
dotnet restore CakeOrders.sln
dotnet build CakeOrders.sln
dotnet run --project src/CakeOrders.Web
```

Connection strings and secrets must not be committed. Use User Secrets in development or a protected environment variable on the server. The sample connection string in `appsettings.json` is for local development only.

## POS terminal settings and SadadBridge

- Admin-only management page: `/PosTerminals` (create, edit, activate/deactivate terminals).
- Configure admin credentials on the server with environment variables `CakeOrdersAdmin__Username` and `CakeOrdersAdmin__Password`. Do not commit credentials to appsettings or source control.
- Configure a long, random bridge API secret on the web server using `SadadBridge__ApiKey`. Configure the same secret locally for SadadBridge. Requests must send `Authorization: Bearer <secret>`.
- Bridge configuration endpoint: `GET /api/bridge/terminals/{id}`. It returns the selected active terminal's connection settings. This endpoint is a configuration API only; it does not itself start a payment or confirm a transaction.
- The terminal settings page uses sample values as form defaults only. No sample terminal is inserted automatically.
- Apply the migration before using the page:
  ```powershell
  dotnet ef database update --project src/CakeOrders.Infrastructure --startup-project src/CakeOrders.Web
  ```
- The current bridge API key is a shared secret. Restrict it to the private network, use HTTPS, and rotate it if exposed. A production deployment should use separate credentials per bridge/workstation and audit payment-result callbacks before recording payments as successful.

## Not production-ready yet
Full user/role administration, automated tests, complete payment initiation and result callbacks, complete status transition workflows, daily delivery scheduling, online-order approval/rejection actions, backup/restore verification, and Rahkaran integration remain future work. Do not expose the SQL Server port publicly.
