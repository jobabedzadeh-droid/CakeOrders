using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkshopMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "WorkshopId",
                table: "AspNetUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkshopRole",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_WorkshopManager",
                table: "AspNetUsers",
                column: "WorkshopId",
                unique: true,
                filter: "[WorkshopId] IS NOT NULL AND [WorkshopRole] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_WorkshopAssignment",
                table: "AspNetUsers",
                sql: "([WorkshopId] IS NULL AND [WorkshopRole] IS NULL) OR ([WorkshopId] IS NOT NULL AND [WorkshopRole] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Workshops_WorkshopId",
                table: "AspNetUsers",
                column: "WorkshopId",
                principalTable: "Workshops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Workshops_WorkshopId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_WorkshopManager",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_WorkshopAssignment",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WorkshopId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WorkshopRole",
                table: "AspNetUsers");
        }
    }
}
