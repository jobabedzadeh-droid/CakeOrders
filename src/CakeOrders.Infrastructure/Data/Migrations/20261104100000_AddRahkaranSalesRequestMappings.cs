using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations;

public partial class AddRahkaranSalesRequestMappings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(name: "SalesOfficeId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "SalesAreaId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "SalesTypeId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "SalesInventoryId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "SalesPlantId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "SalesCurrencyId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "SalesPaymentAgreementId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "CreamCakeProductId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "CreamCakeUnitId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "SemiFondantCakeProductId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "SemiFondantCakeUnitId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "FullFondantCakeProductId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "FullFondantCakeUnitId", table: "RahkaranIntegrationSettings", type: "bigint", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SalesOfficeId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "SalesAreaId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "SalesTypeId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "SalesInventoryId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "SalesPlantId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "SalesCurrencyId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "SalesPaymentAgreementId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "CreamCakeProductId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "CreamCakeUnitId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "SemiFondantCakeProductId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "SemiFondantCakeUnitId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "FullFondantCakeProductId", table: "RahkaranIntegrationSettings");
        migrationBuilder.DropColumn(name: "FullFondantCakeUnitId", table: "RahkaranIntegrationSettings");
    }
}
