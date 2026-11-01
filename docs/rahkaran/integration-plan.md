# نقشه اتصال CakeOrders به راهکاران

این سند بر اساس فایل‌های راهنمای موجود در پوشه `docs/rahkaran/` تهیه شده است. نسخه مستندات بررسی‌شده مربوط به سال ۱۴۰۳ است؛ بنابراین مسیرها و قراردادها باید در محیط واقعی راهکاران با نسخه نصب‌شده تطبیق داده شوند. هیچ فراخوانی واقعی در این مرحله فعال نیست.

## اصول طراحی

- دیتابیس CakeOrders منبع اصلی سفارش قنادی، جزئیات طراحی کیک، وزن، قیمت توافق‌شده، زمان تحویل و وضعیت داخلی سفارش باقی می‌ماند.
- شناسه داخلی سفارش (`Order.Id`) با شناسه‌های راهکاران یکسان نیست.
- ارتباط با راهکاران فقط از سمت سرور و از طریق وب‌سرویس انجام می‌شود؛ فروشگاه آنلاین مستقیماً به دیتابیس متصل نمی‌شود.
- فراخوانی‌های دارای اثر مالی یا ایجاد سند نباید خودکار و بدون کنترل کاربر اجرا شوند.
- نتیجه هر فراخوانی، شناسه برگشتی و خطاهای اعتبارسنجی باید ثبت شوند تا از ایجاد سند تکراری و گم‌شدن نتیجه درخواست جلوگیری شود.
- نشانی پایه سرویس، روش احراز هویت، مقادیر شناسه‌های پایه و دسترسی شبکه از مستندات حاضر به‌تنهایی قابل تعیین نیستند و باید از مدیر راهکاران دریافت شوند.

## سرویس‌های شناسایی‌شده در مستند فروش (WebServiceSLS.pdf)

| کاربرد | مسیر سرویس مستندشده | روش |
|---|---|---|
| ایجاد مشتری | `/Sales/PartyManagement/Services/PartyManagementService.svc/PlaceCustomer` | POST |
| ایجاد درخواست فروش در وضعیت بررسی | `/Sales/OrderManagement/Services/OrderManagementService.svc/PlaceSaleRequestInPendingState` | POST |
| دریافت اقلام درخواست فروش | `/Sales/OrderManagement/Services/OrderManagementService.svc/GetSaleRequestItemsData` | POST |

مسیرها از مستند استخراج شده‌اند و نسبت به نشانی پایه سرویس هستند. پیش از پیاده‌سازی باید بدنه کامل JSON، فیلدهای اجباری، وضعیت‌های مجاز، شناسه مرکز فروش و نحوه احراز هویت در نسخه نصب‌شده بررسی شوند.

### نگاشت پیشنهادی در سطح مفهومی

- اطلاعات مشتری: نام، شماره تماس و سایر داده‌های لازم برای ساخت مشتری در راهکاران. ابتدا باید تصمیم گرفته شود مشتری موجود چگونه پیدا می‌شود؛ ایجاد بی‌قیدوشرط مشتری جدید می‌تواند رکورد تکراری بسازد.
- درخواست فروش: شماره سفارش داخلی، مشخصات مشتری، اقلام و مقادیر مالی موردنیاز راهکاران. نگاشت محصول، واحد فروش، انبار/مرکز فروش، سیاست‌های فروش و قیمت نهایی هنوز نیازمند تأیید کسب‌وکار و تنظیمات واقعی است.
- اطلاعات تخصصی قنادی مانند نوع خامه/فوندانت، متن روی کیک، تصویر نمونه و جزئیات تزئین باید در CakeOrders باقی بماند؛ فقط خلاصه مناسب سفارش در شرح راهکاران ارسال شود، مگر آنکه قرارداد مورد توافق دیگری تعیین شود.

## دریافت اطلاعات پایه از راهکاران

این اطلاعات از وب‌سرویس دریافت می‌شوند و نباید به‌صورت شناسه‌های ثابت در کد نوشته شوند:

| اطلاعات | مسیر سرویس | روش |
|---|---|---|
| فهرست شعب | `/General/BranchManagement/Services/BranchService.svc/GetBranches` | GET |
| فهرست صندوق‌ها | `/ReceiptAndPayment/TreasuryStructure/Services/TreasuryStructureService.svc/GetAllCashes` | GET |
| فهرست حساب‌های بانکی | `/ReceiptAndPayment/TreasuryStructure/Services/TreasuryStructureService.svc/GetAllBankAccounts` | GET |
| عملیات حسابداری | `/ReceiptAndPayment/FinancialOperation/Services/FinancialOperationService.svc/GetAllAccountingOperations` | GET |
| عوامل گردش نقدینگی | `/ReceiptAndPayment/FinancialOperation/Services/FinancialOperationService.svc/GetAllCashFlowFactors` | GET |

شناسه‌های شعبه، صندوق، حساب بانکی، عملیات حسابداری و عامل گردش نقدینگی باید از خروجی این سرویس‌ها انتخاب شوند. مقادیر نمونه مستندات قابل استفاده در محیط پدیده نیستند.

## مشتریان: جست‌وجو، همگام‌سازی و ایجاد

| کاربرد | مسیر سرویس | روش |
|---|---|---|
| دریافت فهرست مشتریان | `/Sales/PartyManagement/Services/PartyManagementService.svc/GetCustomers` | GET |
| دریافت صفحه‌ای مشتریان/تغییرات | `/Sales/PartyManagement/Services/PartyManagementService.svc/GetCustomerList` | POST |
| ایجاد مشتری | `/Sales/PartyManagement/Services/PartyManagementService.svc/PlaceCustomer` | POST |

در مستند SLS، `GetCustomers` پارامتر جست‌وجوی نام/موبایل ندارد؛ `GetCustomerList` ورودی صفحه‌بندی و بازه تاریخ/شناسه دارد. بنابراین راهبرد پیشنهادی، همگام‌سازی اولیه فهرست مشتریان از راهکاران و به‌روزرسانی دوره‌ای/افزایشی آن در دیتابیس CakeOrders است. جست‌وجوی autocomplete پس از سه حرف نام یا شماره موبایل روی نسخه محلی انجام می‌شود. ایجاد مشتری جدید از طریق `PlaceCustomer` است و شناسه `CustomerID` برگشتی باید روی مشتری داخلی و سفارش ذخیره شود.

## سرویس‌های شناسایی‌شده در مستند مالی (WebServiceFIN.pdf)

| کاربرد | مسیر سرویس مستندشده | روش |
|---|---|---|
| ایجاد درخواست دریافت با وضعیت موقت یا تأیید | `/ReceiptAndPayment/RequestManagement/Services/RequestService.svc/RegisterReceiptRequest?approveAfterSave={true|false}` | POST |
| مشاهده درخواست دریافت با شناسه | `/ReceiptAndPayment/RequestManagement/Services/RequestService.svc/ViewReceiptRequest?id={ID}` | GET |
| حذف درخواست دریافت موقت | `/ReceiptAndPayment/RequestManagement/Services/RequestService.svc/DeleteReceiptRequest?receiptRequestId={ID}` | POST |
| ابطال درخواست دریافت تأییدشده | `/ReceiptAndPayment/RequestManagement/Services/RequestService.svc/CancelReceiptRequest?receiptRequestId={ID}` | POST |
| ایجاد رسید دریافت وجه نقد | `/ReceiptAndPayment/ReceiptManagement/Services/ReceiptManagementService.svc/RegisterReceipt` با `ReceiptCashMoneys` | POST |\n| ایجاد رسید دریافت واریز بانکی/کارت‌خوان | همان مسیر با `ReceiptDeposits` | POST |
| ایجاد رسید دریافت چکی | `/ReceiptAndPayment/ReceiptManagement/Services/ReceiptManagementService.svc/RegisterReceipt` | POST |

در سند، برای درخواست دریافت فیلدهایی مانند `BranchRef`، `CounterPartRef`، `CurrencyRef`، `FiscalYearRef` و مجموعه `ReceiptRequestOrdinaryItems` دیده می‌شود. برای رسید نقدی، واریز بانکی و چک، مجموعه‌های متفاوت `ReceiptCashMoneys`، `ReceiptDeposits` و `ReceiptReceivableCheques` وجود دارد. برای دریافت کارت‌خوانی که در حساب بانکی مقصد تسویه می‌شود، مسیر واریز بانکی باید با تأیید واحد مالی استفاده شود. این شناسه‌ها و حساب‌های مالی باید از پیکربندی واقعی راهکاران گرفته شوند؛ نمونه‌های PDF مقادیر عمومی قابل استفاده در محیط عملیاتی نیستند.

## تصمیم مهم درباره بیعانه

وجود فیلد `DepositAmount` در CakeOrders به‌تنهایی مشخص نمی‌کند که دریافت وجه باید در راهکاران به صورت «درخواست دریافت»، «رسید دریافت نقدی»، «رسید چک» یا فرایند دیگری ثبت شود. این انتخاب باید با مسئول مالی تعیین شود. تا آن زمان هیچ رسید یا سند مالی به‌صورت خودکار ایجاد نمی‌شود.

## نگاشت شناسه‌ها در CakeOrders

فیلدهای فعلی `RahkaranCustomerId`، `RahkaranSalesRequestId` و `RahkaranReceiptId` در موجودیت سفارش وجود دارند. پیش از استفاده عملیاتی باید معنای دقیق `RahkaranReceiptId` روشن شود: شناسه درخواست دریافت، شناسه رسید دریافت یا هر دو به صورت جداگانه نگهداری شوند. پیشنهاد ایمن این است که شناسه درخواست دریافت و شناسه رسید نهایی دو فیلد جدا داشته باشند.

## گام‌های اجرایی

1. تأیید نسخه و نشانی پایه وب‌سرویس‌ها، احراز هویت و دسترسی شبکه با مدیر راهکاران.
2. دریافت مقادیر معتبر مرکز فروش، شعبه، سال مالی، ارز، حساب/صندوق و شناسه‌های محصول و واحد فروش.
3. نهایی‌کردن فرایند مالی بیعانه با مسئول مالی.
4. تکمیل و بازبینی Migration اولیه CakeOrders پیش از استفاده از دیتابیس عملیاتی.
5. پیاده‌سازی لایه مستقل `Rahkaran` در Infrastructure/Application، با تنظیمات امن، timeout، ثبت نتیجه و جلوگیری از ارسال تکراری.
6. آزمایش فقط در محیط تست/با داده آزمایشی؛ فعال‌سازی محیط عملیاتی پس از تأیید مسئولان فروش و مالی.

## خارج از محدوده این مرحله

اتصال‌ها و کلاینت‌های HTTP در شاخه توسعه اضافه شده‌اند، اما هنوز از طریق UI یا جریان ثبت سفارش فراخوانی نمی‌شوند. تنظیم `Rahkaran:Enabled` خاموش است؛ هیچ مشتری، درخواست فروش یا رسید مالی در راهکاران به‌صورت خودکار ساخته نمی‌شود.
