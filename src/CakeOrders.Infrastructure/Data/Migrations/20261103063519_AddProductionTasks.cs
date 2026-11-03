using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionTasks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    WorkshopId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionTasks_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionTasks_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionTasks_Workshops_WorkshopId",
                        column: x => x.WorkshopId,
                        principalTable: "Workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionTaskHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionTaskId = table.Column<long>(type: "bigint", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionTaskHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionTaskHistory_ProductionTasks_ProductionTaskId",
                        column: x => x.ProductionTaskId,
                        principalTable: "ProductionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductionTaskItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionTaskId = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Unit = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionTaskItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionTaskItems_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionTaskItems_ProductionTasks_ProductionTaskId",
                        column: x => x.ProductionTaskId,
                        principalTable: "ProductionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTaskHistory_ProductionTaskId_ChangedAtUtc",
                table: "ProductionTaskHistory",
                columns: new[] { "ProductionTaskId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTaskItems_OrderItemId",
                table: "ProductionTaskItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTaskItems_ProductionTaskId",
                table: "ProductionTaskItems",
                column: "ProductionTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTasks_BranchId_WorkshopId_Status",
                table: "ProductionTasks",
                columns: new[] { "BranchId", "WorkshopId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTasks_OrderId_WorkshopId",
                table: "ProductionTasks",
                columns: new[] { "OrderId", "WorkshopId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionTasks_WorkshopId",
                table: "ProductionTasks",
                column: "WorkshopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionTaskHistory");

            migrationBuilder.DropTable(
                name: "ProductionTaskItems");

            migrationBuilder.DropTable(
                name: "ProductionTasks");
        }
    }
}
