using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionBranchAndWorkshops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProductionBranchId",
                table: "Orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Workshops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workshops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workshops_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ProductionBranchId",
                table: "Orders",
                column: "ProductionBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Workshops_BranchId_Name",
                table: "Workshops",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Branches_ProductionBranchId",
                table: "Orders",
                column: "ProductionBranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Branches_ProductionBranchId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "Workshops");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ProductionBranchId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ProductionBranchId",
                table: "Orders");
        }
    }
}
