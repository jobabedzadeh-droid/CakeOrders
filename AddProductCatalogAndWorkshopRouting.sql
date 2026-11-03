BEGIN TRANSACTION;
CREATE TABLE [ProductCategories] (
    [Id] bigint NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([Id])
);

CREATE TABLE [Products] (
    [Id] bigint NOT NULL IDENTITY,
    [CategoryId] bigint NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [Unit] int NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Products_ProductCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [ProductCategories] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ProductWorkshopRoutings] (
    [Id] bigint NOT NULL IDENTITY,
    [ProductId] bigint NOT NULL,
    [BranchId] bigint NOT NULL,
    [WorkshopId] bigint NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_ProductWorkshopRoutings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductWorkshopRoutings_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ProductWorkshopRoutings_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ProductWorkshopRoutings_Workshops_WorkshopId] FOREIGN KEY ([WorkshopId]) REFERENCES [Workshops] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_ProductCategories_Name] ON [ProductCategories] ([Name]);

CREATE UNIQUE INDEX [IX_Products_CategoryId_Name] ON [Products] ([CategoryId], [Name]);

CREATE UNIQUE INDEX [IX_Products_Code] ON [Products] ([Code]);

CREATE INDEX [IX_ProductWorkshopRoutings_BranchId] ON [ProductWorkshopRoutings] ([BranchId]);

CREATE UNIQUE INDEX [IX_ProductWorkshopRoutings_ProductId_BranchId] ON [ProductWorkshopRoutings] ([ProductId], [BranchId]);

CREATE INDEX [IX_ProductWorkshopRoutings_WorkshopId] ON [ProductWorkshopRoutings] ([WorkshopId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261102100856_AddProductCatalogAndWorkshopRouting', N'10.0.14');

COMMIT;
GO
