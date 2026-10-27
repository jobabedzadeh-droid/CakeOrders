# CakeOrders

سامانه مدیریت سفارش‌های قنادی با ASP.NET Core MVC، .NET 10 و SQL Server. این مخزن در مرحله اسکلت اولیه توسعه است و هنوز برای استفاده عملیاتی آماده نیست.

## Architecture
- `CakeOrders.Domain`: موجودیت‌ها و قواعد دامنه
- `CakeOrders.Application`: سرویس‌ها و قراردادهای کاربردی
- `CakeOrders.Infrastructure`: EF Core و دسترسی داده
- `CakeOrders.Web`: رابط وب MVC
- `tests`: تست‌ها

## Requirements
- .NET 10 SDK
- SQL Server

## Run locally
```powershell
dotnet restore
dotnet build
dotnet run --project src/CakeOrders.Web
```

Connection strings and secrets must not be committed. Configure `ConnectionStrings:CakeOrders` through User Secrets for development or a secure environment variable on the server.

This is an initial scaffold. Authentication, migrations, order workflows, secure image uploads, online-order approval/rejection, automated tests, and Rahkaran integration are future implementation steps.
