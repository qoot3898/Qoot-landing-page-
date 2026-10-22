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
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [DisplayName] nvarchar(120) NOT NULL,
        [IsActive] bit NOT NULL,
        [PreferredLanguage] nvarchar(5) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLog] (
        [Id] bigint NOT NULL IDENTITY,
        [At] datetime2 NOT NULL,
        [UserId] uniqueidentifier NULL,
        [UserEmail] nvarchar(256) NOT NULL,
        [Action] nvarchar(60) NOT NULL,
        [EntityType] nvarchar(40) NOT NULL,
        [EntityId] nvarchar(80) NOT NULL,
        [Summary] nvarchar(500) NOT NULL,
        [IpAddress] nvarchar(64) NULL,
        CONSTRAINT [PK_AuditLog] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [ContactSubmissions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [Contact] nvarchar(160) NOT NULL,
        [Need] nvarchar(2000) NOT NULL,
        [People] int NULL,
        [Frequency] nvarchar(80) NOT NULL,
        [Organisation] nvarchar(160) NOT NULL,
        [Location] nvarchar(160) NOT NULL,
        [Language] nvarchar(5) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [AdminNotes] nvarchar(2000) NOT NULL,
        [IsRead] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [IpHash] nvarchar(64) NULL,
        [UserAgent] nvarchar(300) NULL,
        CONSTRAINT [PK_ContactSubmissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [ContentState] (
        [Id] int NOT NULL,
        [Version] bigint NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ContentState] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [MediaAssets] (
        [Id] int NOT NULL IDENTITY,
        [Kind] nvarchar(20) NOT NULL,
        [StoragePath] nvarchar(400) NOT NULL,
        [FileName] nvarchar(200) NOT NULL,
        [ContentType] nvarchar(80) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Width] int NOT NULL,
        [Height] int NOT NULL,
        [AltAr] nvarchar(300) NOT NULL,
        [AltEn] nvarchar(300) NOT NULL,
        [SourcesJson] nvarchar(max) NULL,
        [Sha256] nvarchar(64) NULL,
        [SeedKey] nvarchar(60) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MediaAssets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [NewsCategories] (
        [Id] int NOT NULL IDENTITY,
        [Slug] nvarchar(80) NOT NULL,
        [NameAr] nvarchar(80) NOT NULL,
        [NameEn] nvarchar(80) NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_NewsCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [Sections] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(40) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsVisible] bit NOT NULL,
        [DraftJson] nvarchar(max) NOT NULL,
        [PublishedJson] nvarchar(max) NULL,
        [HasUnpublishedChanges] bit NOT NULL,
        [DraftUpdatedAt] datetime2 NOT NULL,
        [DraftUpdatedById] uniqueidentifier NULL,
        [PublishedAt] datetime2 NULL,
        [PublishedById] uniqueidentifier NULL,
        [ConcurrencyStamp] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Sections] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [SiteSettings] (
        [Id] int NOT NULL,
        [Json] nvarchar(max) NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [ConcurrencyStamp] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SiteSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE TABLE [NewsArticles] (
        [Id] int NOT NULL IDENTITY,
        [Slug] nvarchar(120) NOT NULL,
        [TitleAr] nvarchar(200) NOT NULL,
        [TitleEn] nvarchar(200) NOT NULL,
        [SummaryAr] nvarchar(600) NOT NULL,
        [SummaryEn] nvarchar(600) NOT NULL,
        [BodyArJson] nvarchar(max) NOT NULL,
        [BodyEnJson] nvarchar(max) NOT NULL,
        [MetaTitleAr] nvarchar(200) NOT NULL,
        [MetaTitleEn] nvarchar(200) NOT NULL,
        [MetaDescriptionAr] nvarchar(320) NOT NULL,
        [MetaDescriptionEn] nvarchar(320) NOT NULL,
        [FeaturedImageId] int NULL,
        [CategoryId] int NULL,
        [Status] nvarchar(20) NOT NULL,
        [PublishedAt] datetime2 NULL,
        [IsFeatured] bit NOT NULL,
        [Priority] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [ConcurrencyStamp] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_NewsArticles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NewsArticles_MediaAssets_FeaturedImageId] FOREIGN KEY ([FeaturedImageId]) REFERENCES [MediaAssets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_NewsArticles_NewsCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [NewsCategories] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLog_At] ON [AuditLog] ([At]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContactSubmissions_Status_CreatedAt] ON [ContactSubmissions] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MediaAssets_CreatedAt] ON [MediaAssets] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MediaAssets_SeedKey] ON [MediaAssets] ([SeedKey]) WHERE [SeedKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MediaAssets_StoragePath] ON [MediaAssets] ([StoragePath]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_NewsArticles_CategoryId] ON [NewsArticles] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_NewsArticles_FeaturedImageId] ON [NewsArticles] ([FeaturedImageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NewsArticles_Slug] ON [NewsArticles] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_NewsArticles_Status_PublishedAt] ON [NewsArticles] ([Status], [PublishedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NewsCategories_Slug] ON [NewsCategories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Sections_Key] ON [Sections] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021103826_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261021103826_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

-- 20261022080000_ContactUs: quote requests become Contact us messages (name, phone, optional email, message).
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022080000_ContactUs'
)
BEGIN
    ALTER TABLE [ContactSubmissions] ADD [Email] nvarchar(254) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022080000_ContactUs'
)
BEGIN
    EXEC(N'UPDATE [ContactSubmissions] SET [Email] = LEFT([Contact], 254), [Contact] = N'''' WHERE [Contact] LIKE N''%@%'';
    UPDATE [ContactSubmissions] SET [Need] = LEFT(CONCAT([Need],
        CASE WHEN [Organisation] <> N'''' THEN CONCAT(NCHAR(10), N''Organisation: '', [Organisation]) ELSE N'''' END,
        CASE WHEN [Location] <> N'''' THEN CONCAT(NCHAR(10), N''Location: '', [Location]) ELSE N'''' END,
        CASE WHEN [People] IS NOT NULL THEN CONCAT(NCHAR(10), N''People: '', [People]) ELSE N'''' END,
        CASE WHEN [Frequency] <> N'''' THEN CONCAT(NCHAR(10), N''How often: '', [Frequency]) ELSE N'''' END), 2000);
    UPDATE [ContactSubmissions] SET [Contact] = LEFT([Contact], 40);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022080000_ContactUs'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContactSubmissions]') AND [c].[name] = N'Frequency');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [ContactSubmissions] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [ContactSubmissions] DROP COLUMN [Frequency];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022080000_ContactUs'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContactSubmissions]') AND [c].[name] = N'Location');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [ContactSubmissions] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [ContactSubmissions] DROP COLUMN [Location];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022080000_ContactUs'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContactSubmissions]') AND [c].[name] = N'Organisation');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [ContactSubmissions] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [ContactSubmissions] DROP COLUMN [Organisation];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022080000_ContactUs'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContactSubmissions]') AND [c].[name] = N'People');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [ContactSubmissions] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [ContactSubmissions] DROP COLUMN [People];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022080000_ContactUs'
)
BEGIN
    EXEC sp_rename N'[ContactSubmissions].[Need]', N'Message', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022080000_ContactUs'
)
BEGIN
    EXEC sp_rename N'[ContactSubmissions].[Contact]', N'Phone', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022080000_ContactUs'
)
BEGIN
    EXEC(N'ALTER TABLE [ContactSubmissions] ALTER COLUMN [Phone] nvarchar(40) NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022080000_ContactUs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022080000_ContactUs', N'10.0.12');
END;

COMMIT;
GO
