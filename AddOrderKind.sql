BEGIN TRANSACTION;
ALTER TABLE [Orders] ADD [Kind] int NOT NULL DEFAULT 1;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261103053033_AddOrderKind', N'10.0.14');

COMMIT;
GO
