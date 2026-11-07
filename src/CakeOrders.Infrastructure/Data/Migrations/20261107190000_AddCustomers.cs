using CakeOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations;

[DbContext(typeof(CakeOrdersDbContext))]
[Migration("20261107190000_AddCustomers")]
public sealed class AddCustomers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                NormalizedPhone = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                RahkaranCustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Customers_NormalizedPhone",
            table: "Customers",
            column: "NormalizedPhone",
            unique: true);

        migrationBuilder.AddColumn<long>(
            name: "CustomerId",
            table: "Orders",
            type: "bigint",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Orders_CustomerId",
            table: "Orders",
            column: "CustomerId");

        migrationBuilder.AddForeignKey(
            name: "FK_Orders_Customers_CustomerId",
            table: "Orders",
            column: "CustomerId",
            principalTable: "Customers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        // Import customers from historical orders so existing phone numbers are recognized
        // immediately. Orders sharing a normalized mobile number point to one customer.
        migrationBuilder.Sql(@"
SELECT
    o.Id AS OrderId,
    LTRIM(RTRIM(o.CustomerName)) AS CustomerName,
    o.RahkaranCustomerId,
    o.CreatedAtUtc,
    CASE
        WHEN LEFT(cleaned.Digits, 4) = '0098' AND LEN(cleaned.Digits) = 14 THEN '0' + SUBSTRING(cleaned.Digits, 5, 10)
        WHEN LEFT(cleaned.Digits, 2) = '98' AND LEN(cleaned.Digits) = 12 THEN '0' + SUBSTRING(cleaned.Digits, 3, 10)
        WHEN LEFT(cleaned.Digits, 1) = '9' AND LEN(cleaned.Digits) = 10 THEN '0' + cleaned.Digits
        ELSE cleaned.Digits
    END AS NormalizedPhone
INTO #OrderCustomerPhones
FROM Orders o
CROSS APPLY
(
    SELECT REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(COALESCE(o.CustomerPhone, ''), N'۰', N'0'), N'۱', N'1'), N'۲', N'2'), N'۳', N'3'), N'۴', N'4'), N'۵', N'5'), N'۶', N'6'), N'۷', N'7'), N'۸', N'8'), N'۹', N'9'), N'٠', N'0'), N'١', N'1'), N'٢', N'2'), N'٣', N'3'), N'٤', N'4'), N'٥', N'5'), N'٦', N'6'), N'٧', N'7'), N'٨', N'8'), N'٩', N'9') AS Digits
) translated
CROSS APPLY
(
    SELECT REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
        translated.Digits, N' ', N''), N'-', N''), N'(', N''), N')', N''), N'+', N''), N'.', N''), N'_', N'') AS Digits
) cleaned
WHERE o.CustomerName IS NOT NULL
  AND LEN(LTRIM(RTRIM(o.CustomerName))) > 0;

;WITH RankedCustomers AS
(
    SELECT *,
        ROW_NUMBER() OVER (PARTITION BY NormalizedPhone ORDER BY CreatedAtUtc DESC, OrderId DESC) AS RowNumber
    FROM #OrderCustomerPhones
    WHERE LEN(NormalizedPhone) = 11
      AND LEFT(NormalizedPhone, 2) = '09'
)
INSERT INTO Customers
    (FirstName, LastName, Phone, NormalizedPhone, RahkaranCustomerId, IsActive, CreatedAtUtc)
SELECT
    LEFT(CASE WHEN CHARINDEX(N' ', CustomerName) > 0
        THEN LEFT(CustomerName, CHARINDEX(N' ', CustomerName) - 1)
        ELSE CustomerName END, 100),
    LEFT(CASE WHEN CHARINDEX(N' ', CustomerName) > 0
        THEN LTRIM(SUBSTRING(CustomerName, CHARINDEX(N' ', CustomerName) + 1, 200))
        ELSE N'' END, 100),
    NormalizedPhone,
    NormalizedPhone,
    RahkaranCustomerId,
    1,
    CreatedAtUtc
FROM RankedCustomers
WHERE RowNumber = 1;

UPDATE o
SET o.CustomerId = c.Id
FROM Orders o
INNER JOIN #OrderCustomerPhones p ON p.OrderId = o.Id
INNER JOIN Customers c ON c.NormalizedPhone = p.NormalizedPhone
WHERE o.CustomerId IS NULL;

DROP TABLE #OrderCustomerPhones;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Orders_Customers_CustomerId",
            table: "Orders");

        migrationBuilder.DropIndex(
            name: "IX_Orders_CustomerId",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "CustomerId",
            table: "Orders");

        migrationBuilder.DropTable(name: "Customers");
    }
}
