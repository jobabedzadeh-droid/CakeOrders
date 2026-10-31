BEGIN TRANSACTION;
CREATE TABLE [Branches] (
    [Id] bigint NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Address] nvarchar(300) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Branches] PRIMARY KEY ([Id])
);

CREATE TABLE [CashRegisters] (
    [Id] bigint NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [BranchId] bigint NOT NULL,
    [PosTerminalId] bigint NULL,
    [BridgeInstallationId] nvarchar(100) NULL,
    [BridgeSecretHash] nvarchar(128) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_CashRegisters] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CashRegisters_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CashRegisters_PosTerminals_PosTerminalId] FOREIGN KEY ([PosTerminalId]) REFERENCES [PosTerminals] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_Branches_Name] ON [Branches] ([Name]);

CREATE UNIQUE INDEX [IX_CashRegisters_BranchId_Name] ON [CashRegisters] ([BranchId], [Name]);

CREATE UNIQUE INDEX [IX_CashRegisters_BridgeInstallationId] ON [CashRegisters] ([BridgeInstallationId]) WHERE [BridgeInstallationId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_CashRegisters_PosTerminalId] ON [CashRegisters] ([PosTerminalId]) WHERE [PosTerminalId] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029163723_AddBranchesAndCashRegisters', N'10.0.14');

COMMIT;
GO

