# CakeOrders

سامانه مدیریت سفارش‌های قنادی با ASP.NET Core MVC، .NET 10 و SQL Server. این مخزن در مرحله توسعه اولیه است و هنوز برای استفاده عملیاتی آماده نیست.

## Architecture
- `CakeOrders.Domain`: مدل‌ها و قواعد دامنه
- `CakeOrders.Application`: لایه کاربردی
- `CakeOrders.Infrastructure`: EF Core و دسترسی داده
- `CakeOrders.Web`: رابط وب MVC
- `database/001_InitialCreate.sql`: ساخت اولیه جداول SQL Server

## Current features
- داشبورد فارسی و راست‌چین
- فرم اولیه ثبت سفارش حضوری و آنلاین
- اعتبارسنجی ورودی‌ها و محاسبه مبلغ نهایی سمت سرور
- ذخیره سفارش و تاریخچه وضعیت در SQL Server
- فهرست سفارش‌ها و صفحه جزئیات

## Requirements
- .NET 10 SDK
- SQL Server

## Initialize database
1. فایل `database/001_InitialCreate.sql` را در SQL Server Management Studio باز کنید.
2. با کاربری که مجوز ایجاد دیتابیس دارد اجرا کنید.
3. برای اجرای برنامه، connection string را متناسب با محیط خود تنظیم کنید.

## Run locally
```powershell
dotnet restore CakeOrders.sln
dotnet build CakeOrders.sln
dotnet run --project src/CakeOrders.Web
```

Connection strings and secrets must not be committed. For development, use User Secrets; on the server, use a secure environment variable or protected configuration. The sample connection string in `appsettings.json` is only a local-development example and may need adjustment for your SQL Server authentication mode.

## Not production-ready yet
Authentication and authorization, secure sample-image uploads, automated tests, status transition workflows, daily delivery scheduling, online-order approval/rejection actions, backup/restore verification, and Rahkaran integration remain future work. Do not expose the SQL Server port publicly.
