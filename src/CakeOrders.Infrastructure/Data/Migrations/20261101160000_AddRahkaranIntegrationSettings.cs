using CakeOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations;

[DbContext(typeof(CakeOrdersDbContext))]
[Migration("20261101160000_AddRahkaranIntegrationSettings")]
public partial class AddRahkaranIntegrationSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RahkaranIntegrationSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false),
                BranchId = table.Column<long>(type: "bigint", nullable: true),
                CashId = table.Column<long>(type: "bigint", nullable: true),
                CashAccountingOperationId = table.Column<long>(type: "bigint", nullable: true),
                CashFlowFactorId = table.Column<long>(type: "bigint", nullable: true),
                PosBankAccountId = table.Column<long>(type: "bigint", nullable: true),
                PosAccountingOperationId = table.Column<long>(type: "bigint", nullable: true),
                PosCashFlowFactorId = table.Column<long>(type: "bigint", nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RahkaranIntegrationSettings", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RahkaranIntegrationSettings");
    }
}
