/*
  CakeOrders initial database schema
  Run in SQL Server Management Studio with an account allowed to create/use CakeOrdersDb.
  This script is idempotent for the initial tables.
*/
IF DB_ID(N'CakeOrdersDb') IS NULL
BEGIN
    CREATE DATABASE [CakeOrdersDb];
END;
GO
USE [CakeOrdersDb];
GO

IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Orders
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
        OrderNumber NVARCHAR(32) NOT NULL,
        Channel INT NOT NULL,
        Status INT NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL,
        DeliveryAt DATETIME2 NOT NULL,
        CustomerName NVARCHAR(200) NULL,
        CustomerPhone NVARCHAR(30) NULL,
        OrderType NVARCHAR(100) NOT NULL,
        CakeType INT NOT NULL,
        RequestedWeightKg DECIMAL(8,2) NOT NULL,
        PricePerKg DECIMAL(18,2) NOT NULL,
        SecondaryPricePerKg DECIMAL(18,2) NULL,
        PrintType NVARCHAR(MAX) NULL,
        BackgroundColor NVARCHAR(100) NULL,
        MoldType NVARCHAR(MAX) NULL,
        HasBananaFilling BIT NOT NULL,
        HasWalnutFilling BIT NOT NULL,
        WritingLocation NVARCHAR(MAX) NULL,
        DesignNotes NVARCHAR(MAX) NULL,
        SampleImagePath NVARCHAR(MAX) NULL,
        MacaronCost DECIMAL(18,2) NOT NULL,
        FlowerCost DECIMAL(18,2) NOT NULL,
        DummyCost DECIMAL(18,2) NOT NULL,
        PrintingCost DECIMAL(18,2) NOT NULL,
        ShippingCost DECIMAL(18,2) NOT NULL,
        OtherCost DECIMAL(18,2) NOT NULL,
        Discount DECIMAL(18,2) NOT NULL,
        FinalAmount DECIMAL(18,2) NOT NULL,
        DepositAmount DECIMAL(18,2) NOT NULL,
        ReferrerCode NVARCHAR(50) NULL,
        RejectionReason NVARCHAR(MAX) NULL,
        RahkaranCustomerId NVARCHAR(MAX) NULL,
        RahkaranSalesRequestId NVARCHAR(MAX) NULL,
        RahkaranReceiptId NVARCHAR(MAX) NULL
    );
    CREATE UNIQUE INDEX IX_Orders_OrderNumber ON dbo.Orders(OrderNumber);
END;
GO

IF OBJECT_ID(N'dbo.OrderStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderStatusHistory
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderStatusHistory PRIMARY KEY,
        OrderId BIGINT NOT NULL,
        PreviousStatus INT NOT NULL,
        NewStatus INT NOT NULL,
        ChangedAtUtc DATETIME2 NOT NULL,
        ChangedByUserId NVARCHAR(MAX) NULL,
        Note NVARCHAR(1000) NULL,
        CONSTRAINT FK_OrderStatusHistory_Orders_OrderId
            FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_OrderStatusHistory_OrderId_ChangedAtUtc
        ON dbo.OrderStatusHistory(OrderId, ChangedAtUtc);
END;
GO
