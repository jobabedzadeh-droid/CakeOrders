using CakeOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations;

[DbContext(typeof(CakeOrdersDbContext))]
[Migration("20261107210000_ExpandRahkaranCustomerData")]
public sealed class ExpandRahkaranCustomerData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CompanyName", table: "Customers", type: "nvarchar(300)", maxLength: 300, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "Details", table: "Customers", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "EconomicCode", table: "Customers", type: "nvarchar(50)", maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "Email", table: "Customers", type: "nvarchar(254)", maxLength: 254, nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "LastRahkaranSyncAtUtc", table: "Customers", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "NationalId", table: "Customers", type: "nvarchar(50)", maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RahkaranCustomerNumber", table: "Customers", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RahkaranSecondCode", table: "Customers", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RahkaranDLCode", table: "Customers", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<long>(
            name: "RahkaranPartyId", table: "Customers", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RahkaranPartyName", table: "Customers", type: "nvarchar(300)", maxLength: 300, nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "RahkaranPartyType", table: "Customers", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "RahkaranStatus", table: "Customers", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "RahkaranStateCode", table: "Customers", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RahkaranStateTitle", table: "Customers", type: "nvarchar(150)", maxLength: 150, nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "RahkaranTypeCode", table: "Customers", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RahkaranTypeTitle", table: "Customers", type: "nvarchar(150)", maxLength: 150, nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "RahkaranCreatedAt", table: "Customers", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "RahkaranLastModifiedAt", table: "Customers", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RahkaranRawDataJson", table: "Customers", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RegistrationCode", table: "Customers", type: "nvarchar(50)", maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "Telephone", table: "Customers", type: "nvarchar(50)", maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "WebsiteUrl", table: "Customers", type: "nvarchar(500)", maxLength: 500, nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Customers_RahkaranCustomerId",
            table: "Customers",
            column: "RahkaranCustomerId",
            unique: true,
            filter: "[RahkaranCustomerId] IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_Customers_RahkaranDLCode",
            table: "Customers",
            column: "RahkaranDLCode");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Customers_RahkaranCustomerId", table: "Customers");
        migrationBuilder.DropIndex(name: "IX_Customers_RahkaranDLCode", table: "Customers");

        migrationBuilder.DropColumn(name: "CompanyName", table: "Customers");
        migrationBuilder.DropColumn(name: "Details", table: "Customers");
        migrationBuilder.DropColumn(name: "EconomicCode", table: "Customers");
        migrationBuilder.DropColumn(name: "Email", table: "Customers");
        migrationBuilder.DropColumn(name: "LastRahkaranSyncAtUtc", table: "Customers");
        migrationBuilder.DropColumn(name: "NationalId", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranCustomerNumber", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranSecondCode", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranDLCode", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranPartyId", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranPartyName", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranPartyType", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranStatus", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranStateCode", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranStateTitle", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranTypeCode", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranTypeTitle", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranCreatedAt", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranLastModifiedAt", table: "Customers");
        migrationBuilder.DropColumn(name: "RahkaranRawDataJson", table: "Customers");
        migrationBuilder.DropColumn(name: "RegistrationCode", table: "Customers");
        migrationBuilder.DropColumn(name: "Telephone", table: "Customers");
        migrationBuilder.DropColumn(name: "WebsiteUrl", table: "Customers");
    }
}
