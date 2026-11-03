using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeOrders.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashPaymentsAndUserNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                /* 1. User permission */
                IF COL_LENGTH(N'dbo.AspNetUsers', N'CanManuallyConfirmOrders') IS NULL
                BEGIN
                    ALTER TABLE dbo.AspNetUsers
                    ADD CanManuallyConfirmOrders bit NOT NULL
                        CONSTRAINT DF_AspNetUsers_CanManuallyConfirmOrders DEFAULT (0);
                END;

                /* 2. Cash payments table */
                IF OBJECT_ID(N'dbo.OrderCashPayments', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.OrderCashPayments
                    (
                        Id bigint IDENTITY(1,1) NOT NULL,
                        OrderId bigint NOT NULL,
                        Amount decimal(18,2) NOT NULL,
                        Purpose int NOT NULL,
                        ReceivedByUserId nvarchar(450) NOT NULL,
                        ReceivedAtUtc datetime2 NOT NULL,
                        Notes nvarchar(1000) NULL,
                        ReceiptReference nvarchar(100) NULL,
                        IdempotencyKey nvarchar(100) NOT NULL,

                        CONSTRAINT PK_OrderCashPayments
                            PRIMARY KEY (Id),

                        CONSTRAINT FK_OrderCashPayments_Orders_OrderId
                            FOREIGN KEY (OrderId)
                            REFERENCES dbo.Orders(Id)
                            ON DELETE NO ACTION
                    );
                END;

                /* Ensure cash payment foreign key exists */
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_OrderCashPayments_Orders_OrderId'
                AND parent_object_id = OBJECT_ID(N'dbo.OrderCashPayments')
                AND referenced_object_id = OBJECT_ID(N'dbo.Orders')
                )
                BEGIN
                    ALTER TABLE dbo.OrderCashPayments
                    ADD CONSTRAINT FK_OrderCashPayments_Orders_OrderId
                        FOREIGN KEY (OrderId)
                        REFERENCES dbo.Orders(Id)
                        ON DELETE NO ACTION;
                END;

                /* Cash payment indexes */
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.OrderCashPayments')
                      AND name = N'IX_OrderCashPayments_IdempotencyKey'
                )
                BEGIN
                    CREATE UNIQUE INDEX IX_OrderCashPayments_IdempotencyKey
                    ON dbo.OrderCashPayments(IdempotencyKey);
                END;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.OrderCashPayments')
                      AND name = N'IX_OrderCashPayments_OrderId_ReceivedAtUtc'
                )
                BEGIN
                    CREATE INDEX IX_OrderCashPayments_OrderId_ReceivedAtUtc
                    ON dbo.OrderCashPayments(OrderId, ReceivedAtUtc);
                END;

                /* 3. User notifications table */
                IF OBJECT_ID(N'dbo.UserNotifications', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.UserNotifications
                    (
                        Id bigint IDENTITY(1,1) NOT NULL,
                        RecipientUserId nvarchar(450) NOT NULL,
                        Title nvarchar(200) NOT NULL,
                        Message nvarchar(2000) NOT NULL,
                        Link nvarchar(500) NULL,
                        CreatedAtUtc datetime2 NOT NULL,
                        ReadAtUtc datetime2 NULL,

                        CONSTRAINT PK_UserNotifications
                            PRIMARY KEY (Id),

                        CONSTRAINT FK_UserNotifications_AspNetUsers_RecipientUserId
                            FOREIGN KEY (RecipientUserId)
                            REFERENCES dbo.AspNetUsers(Id)
                            ON DELETE CASCADE
                    );
                END;

                /* Ensure notification foreign key exists */
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_UserNotifications_AspNetUsers_RecipientUserId'
                AND parent_object_id = OBJECT_ID(N'dbo.UserNotifications')
                AND referenced_object_id = OBJECT_ID(N'dbo.AspNetUsers')
                )
                BEGIN
                    ALTER TABLE dbo.UserNotifications
                    ADD CONSTRAINT FK_UserNotifications_AspNetUsers_RecipientUserId
                        FOREIGN KEY (RecipientUserId)
                        REFERENCES dbo.AspNetUsers(Id)
                        ON DELETE CASCADE;
                END;

                /* Notification indexes */
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.UserNotifications')
                      AND name = N'IX_UserNotifications_RecipientUserId_CreatedAtUtc'
                )
                BEGIN
                    CREATE INDEX IX_UserNotifications_RecipientUserId_CreatedAtUtc
                    ON dbo.UserNotifications(RecipientUserId, CreatedAtUtc);
                END;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.UserNotifications')
                      AND name = N'IX_UserNotifications_RecipientUserId_ReadAtUtc'
                )
                BEGIN
                    CREATE INDEX IX_UserNotifications_RecipientUserId_ReadAtUtc
                    ON dbo.UserNotifications(RecipientUserId, ReadAtUtc);
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left empty.
            // This migration reconciles existing database objects with EF Core.
            // Dropping these objects could destroy existing payment and notification data.
        }
    }
}