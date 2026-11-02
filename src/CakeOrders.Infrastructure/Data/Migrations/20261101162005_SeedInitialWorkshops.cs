using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations
{
    public partial class SeedInitialWorkshops : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var createdAt = new DateTime(
                2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData(
                table: "Workshops",
                columns: new[]
                {
                    "Id",
                    "BranchId",
                    "Name",
                    "IsActive",
                    "CreatedAtUtc"
                },
                values: new object[,]
                {
                    { 1L, 2L, "کیک‌سازی", true, createdAt },
                    { 2L, 2L, "خشک‌کاری", true, createdAt },
                    { 3L, 2L, "بیکری", true, createdAt },
                    { 4L, 1L, "ترکاری", true, createdAt },
                    { 5L, 1L, "خشک‌کاری", true, createdAt },
                    { 6L, 1L, "دسرزنی", true, createdAt },
                    { 7L, 1L, "کیک‌سازی", true, createdAt }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Workshops",
                keyColumn: "Id",
                keyValues: new object[]
                {
                    1L, 2L, 3L, 4L, 5L, 6L, 7L
                });
        }
    }
}