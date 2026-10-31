using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchesAndCashRegisters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashRegisters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    PosTerminalId = table.Column<long>(type: "bigint", nullable: true),
                    BridgeInstallationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BridgeSecretHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashRegisters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashRegisters_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashRegisters_PosTerminals_PosTerminalId",
                        column: x => x.PosTerminalId,
                        principalTable: "PosTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Name",
                table: "Branches",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_BranchId_Name",
                table: "CashRegisters",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_BridgeInstallationId",
                table: "CashRegisters",
                column: "BridgeInstallationId",
                unique: true,
                filter: "[BridgeInstallationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_PosTerminalId",
                table: "CashRegisters",
                column: "PosTerminalId",
                unique: true,
                filter: "[PosTerminalId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashRegisters");

            migrationBuilder.DropTable(
                name: "Branches");
        }
    }
}
