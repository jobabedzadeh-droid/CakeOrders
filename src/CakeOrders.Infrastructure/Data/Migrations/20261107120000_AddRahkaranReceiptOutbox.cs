using CakeOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations;

[DbContext(typeof(CakeOrdersDbContext))]
[Migration("20261107120000_AddRahkaranReceiptOutbox")]
public sealed class AddRahkaranReceiptOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RahkaranReceiptOutbox",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                OrderId = table.Column<long>(type: "bigint", nullable: false),
                OrderCashPaymentId = table.Column<long>(type: "bigint", nullable: true),
                PaymentTransactionId = table.Column<long>(type: "bigint", nullable: true),
                Source = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ProcessingStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                RemoteReceiptId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                RemoteReceiptNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RahkaranReceiptOutbox", x => x.Id);
                table.CheckConstraint(
                    "CK_RahkaranReceiptOutbox_SourcePayment",
                    "([Amount] > 0) AND (([Source] = 1 AND [OrderCashPaymentId] IS NOT NULL AND [PaymentTransactionId] IS NULL) OR ([Source] = 2 AND [PaymentTransactionId] IS NOT NULL AND [OrderCashPaymentId] IS NULL))");
                table.ForeignKey("FK_RahkaranReceiptOutbox_Orders_OrderId", x => x.OrderId, "Orders", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_RahkaranReceiptOutbox_OrderCashPayments_OrderCashPaymentId", x => x.OrderCashPaymentId, "OrderCashPayments", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_RahkaranReceiptOutbox_PaymentTransactions_PaymentTransactionId", x => x.PaymentTransactionId, "PaymentTransactions", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_RahkaranReceiptOutbox_OrderId", "RahkaranReceiptOutbox", "OrderId");
        migrationBuilder.CreateIndex("IX_RahkaranReceiptOutbox_OrderCashPaymentId", "RahkaranReceiptOutbox", "OrderCashPaymentId", unique: true, filter: "[OrderCashPaymentId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_RahkaranReceiptOutbox_PaymentTransactionId", "RahkaranReceiptOutbox", "PaymentTransactionId", unique: true, filter: "[PaymentTransactionId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_RahkaranReceiptOutbox_Status_CreatedAtUtc", "RahkaranReceiptOutbox", new[] { "Status", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "RahkaranReceiptOutbox");
}
