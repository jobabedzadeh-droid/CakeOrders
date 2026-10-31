using CakeOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations;

[DbContext(typeof(CakeOrdersDbContext))]
[Migration("20261031150000_SeparateRequestedAndPaidDeposit")]
public partial class SeparateRequestedAndPaidDeposit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "RequestedDepositAmount",
            table: "Orders",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        // Preserve the old requested amount, then rebuild the paid amount only
        // from successful deposit transactions. This avoids treating an
        // uncollected requested deposit as money received.
        migrationBuilder.Sql(@"
UPDATE [Orders]
SET [RequestedDepositAmount] = [DepositAmount];

UPDATE o
SET [DepositAmount] = COALESCE((
    SELECT SUM(p.[Amount])
    FROM [PaymentTransactions] p
    WHERE p.[OrderId] = o.[Id]
      AND p.[Purpose] = 1
      AND p.[Status] = 3
), 0)
FROM [Orders] o;

UPDATE [Orders]
SET [Status] = 1
WHERE [Status] = 2
  AND [RequestedDepositAmount] > [DepositAmount];
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RequestedDepositAmount",
            table: "Orders");
    }
}
