using CakeOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations;

[DbContext(typeof(CakeOrdersDbContext))]
[Migration("20261104190000_AddOrderReferrerRelationship")]
public sealed partial class AddOrderReferrerRelationship : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "ReferrerId",
            table: "Orders",
            type: "bigint",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Orders_ReferrerId",
            table: "Orders",
            column: "ReferrerId");

        migrationBuilder.AddForeignKey(
            name: "FK_Orders_Referrers_ReferrerId",
            table: "Orders",
            column: "ReferrerId",
            principalTable: "Referrers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Orders_Referrers_ReferrerId",
            table: "Orders");

        migrationBuilder.DropIndex(
            name: "IX_Orders_ReferrerId",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "ReferrerId",
            table: "Orders");
    }
}
