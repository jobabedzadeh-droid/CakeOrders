using CakeOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations;

[DbContext(typeof(CakeOrdersDbContext))]
[Migration("20261108121500_AddSettlementReceiptMappings")]
public sealed class AddSettlementReceiptMappings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "SettlementCashAccountingOperationId",
            table: "RahkaranIntegrationSettings",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "SettlementCashFlowFactorId",
            table: "RahkaranIntegrationSettings",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "SettlementPosBankAccountId",
            table: "RahkaranIntegrationSettings",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "SettlementPosAccountingOperationId",
            table: "RahkaranIntegrationSettings",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "SettlementPosCashFlowFactorId",
            table: "RahkaranIntegrationSettings",
            type: "bigint",
            nullable: true);

        // Legacy outbox rows are treated as deposit receipts so their existing
        // reconciliation path remains unchanged.
        migrationBuilder.AddColumn<int>(
            name: "Purpose",
            table: "RahkaranReceiptOutbox",
            type: "int",
            nullable: false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SettlementCashAccountingOperationId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "SettlementCashFlowFactorId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "SettlementPosBankAccountId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "SettlementPosAccountingOperationId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "SettlementPosCashFlowFactorId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "Purpose", table: "RahkaranReceiptOutbox");
    }
}
