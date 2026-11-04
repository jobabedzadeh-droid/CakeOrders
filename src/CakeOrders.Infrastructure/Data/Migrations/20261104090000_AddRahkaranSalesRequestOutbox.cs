using CakeOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations;

public sealed partial class AddRahkaranSalesRequestOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RahkaranSalesRequestOutbox",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                OrderId = table.Column<long>(type: "bigint", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                ProcessingStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                RemoteSalesRequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                RemoteSalesRequestNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RahkaranSalesRequestOutbox", x => x.Id);
                table.ForeignKey(
                    name: "FK_RahkaranSalesRequestOutbox_Orders_OrderId",
                    column: x => x.OrderId,
                    principalTable: "Orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RahkaranSalesRequestOutbox_OrderId",
            table: "RahkaranSalesRequestOutbox",
            column: "OrderId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RahkaranSalesRequestOutbox_Status_NextAttemptAtUtc_CreatedAtUtc",
            table: "RahkaranSalesRequestOutbox",
            columns: new[] { "Status", "NextAttemptAtUtc", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RahkaranSalesRequestOutbox");
    }
}
