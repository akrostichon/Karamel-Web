IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105092322_AddUserTracking'
)
BEGIN
    CREATE TABLE [Playlists] (
        [Id] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Playlists] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105092322_AddUserTracking'
)
BEGIN
    CREATE TABLE [Sessions] (
        [Id] uniqueidentifier NOT NULL,
        [LinkToken] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NULL,
        [RequireSingerName] bit NOT NULL,
        [PauseBetweenSongsSeconds] int NOT NULL,
        CONSTRAINT [PK_Sessions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105092322_AddUserTracking'
)
BEGIN
    CREATE TABLE [PlaylistItems] (
        [Id] uniqueidentifier NOT NULL,
        [PlaylistId] uniqueidentifier NOT NULL,
        [Position] int NOT NULL,
        [Artist] nvarchar(max) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [SingerName] nvarchar(max) NULL,
        CONSTRAINT [PK_PlaylistItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlaylistItems_Playlists_PlaylistId] FOREIGN KEY ([PlaylistId]) REFERENCES [Playlists] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105092322_AddUserTracking'
)
BEGIN
    CREATE INDEX [IX_PlaylistItems_PlaylistId] ON [PlaylistItems] ([PlaylistId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105092322_AddUserTracking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260105092322_AddUserTracking', N'10.0.1');
END;

COMMIT;
GO

