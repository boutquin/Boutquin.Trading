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
GO

CREATE TABLE [AssetClasses] (
    [Code] nvarchar(3) NOT NULL,
    [Description] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_AssetClasses] PRIMARY KEY ([Code])
);
GO

CREATE TABLE [Cities] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(50) NOT NULL,
    [TimeZoneCode] nvarchar(10) NOT NULL,
    [CountryCode] nvarchar(2) NOT NULL,
    CONSTRAINT [PK_Cities] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Continents] (
    [Code] nvarchar(2) NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Continents] PRIMARY KEY ([Code])
);
GO

CREATE TABLE [Countries] (
    [Code] nvarchar(2) NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [NumericCode] int NOT NULL,
    [CurrencyCode] nvarchar(3) NOT NULL,
    [ContinentCode] nvarchar(2) NOT NULL,
    CONSTRAINT [PK_Countries] PRIMARY KEY ([Code])
);
GO

CREATE TABLE [Currencies] (
    [Code] nvarchar(3) NOT NULL,
    [NumericCode] int NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [Symbol] nvarchar(5) NOT NULL,
    CONSTRAINT [PK_Currencies] PRIMARY KEY ([Code])
);
GO

CREATE TABLE [FxRates] (
    [Id] int NOT NULL IDENTITY,
    [RateDate] Date NOT NULL,
    [BaseCurrencyCode] nvarchar(3) NOT NULL,
    [QuoteCurrencyCode] nvarchar(3) NOT NULL,
    [Rate] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_FxRates] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [TimeZones] (
    [Code] nvarchar(10) NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [TimeZoneOffset] nvarchar(6) NOT NULL,
    [UsesDaylightSaving] bit NOT NULL,
    CONSTRAINT [PK_TimeZones] PRIMARY KEY ([Code])
);
GO

CREATE TABLE [Exchanges] (
    [Code] nvarchar(4) NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [City_id] int NOT NULL,
    CONSTRAINT [PK_Exchanges] PRIMARY KEY ([Code]),
    CONSTRAINT [FK_Exchanges_Cities_City_id] FOREIGN KEY ([City_id]) REFERENCES [Cities] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ExchangeHolidays] (
    [Id] int NOT NULL IDENTITY,
    [ExchangeCode] nvarchar(4) NOT NULL,
    [HolidayDate] datetime2 NOT NULL,
    [Description] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_ExchangeHolidays] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExchangeHolidays_Exchanges_ExchangeCode] FOREIGN KEY ([ExchangeCode]) REFERENCES [Exchanges] ([Code]) ON DELETE CASCADE
);
GO

CREATE TABLE [ExchangeSchedules] (
    [Id] int NOT NULL IDENTITY,
    [ExchangeCode] nvarchar(4) NOT NULL,
    [DayOfWeek] int NOT NULL,
    [OpenTime] time NOT NULL,
    [CloseTime] time NOT NULL,
    CONSTRAINT [PK_ExchangeSchedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExchangeSchedules_Exchanges_ExchangeCode] FOREIGN KEY ([ExchangeCode]) REFERENCES [Exchanges] ([Code]) ON DELETE CASCADE
);
GO

CREATE TABLE [Securities] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [AssetClassCode] nvarchar(3) NOT NULL,
    [ExchangeCode] nvarchar(4) NOT NULL,
    CONSTRAINT [PK_Securities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Securities_Exchanges_ExchangeCode] FOREIGN KEY ([ExchangeCode]) REFERENCES [Exchanges] ([Code]) ON DELETE CASCADE
);
GO

CREATE TABLE [SecurityPrices] (
    [Id] int NOT NULL IDENTITY,
    [TradeDate] Date NOT NULL,
    [SecurityId] int NOT NULL,
    [OpenPrice] decimal(18,2) NOT NULL,
    [HighPrice] decimal(18,2) NOT NULL,
    [LowPrice] decimal(18,2) NOT NULL,
    [ClosePrice] decimal(18,2) NOT NULL,
    [Volume] int NOT NULL,
    [Dividend] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_SecurityPrices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecurityPrices_Securities_SecurityId] FOREIGN KEY ([SecurityId]) REFERENCES [Securities] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SecuritySymbols] (
    [Id] int NOT NULL IDENTITY,
    [SecurityId] int NOT NULL,
    [Symbol] nvarchar(50) NOT NULL,
    [Standard] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_SecuritySymbols] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecuritySymbols_Securities_SecurityId] FOREIGN KEY ([SecurityId]) REFERENCES [Securities] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX [IX_AssetClasses_Description] ON [AssetClasses] ([Description]);
GO

CREATE UNIQUE INDEX [IX_Cities_CountryCode_Name] ON [Cities] ([CountryCode], [Name]);
GO

CREATE UNIQUE INDEX [IX_Continents_Name] ON [Continents] ([Name]);
GO

CREATE UNIQUE INDEX [IX_Countries_Name] ON [Countries] ([Name]);
GO

CREATE UNIQUE INDEX [IX_Currencies_Name] ON [Currencies] ([Name]);
GO

CREATE UNIQUE INDEX [IX_Currencies_NumericCode] ON [Currencies] ([NumericCode]);
GO

CREATE UNIQUE INDEX [IX_ExchangeHolidays_Description] ON [ExchangeHolidays] ([Description]);
GO

CREATE UNIQUE INDEX [IX_ExchangeHolidays_ExchangeCode_HolidayDate] ON [ExchangeHolidays] ([ExchangeCode], [HolidayDate]);
GO

CREATE INDEX [IX_Exchanges_City_id] ON [Exchanges] ([City_id]);
GO

CREATE UNIQUE INDEX [IX_Exchanges_Name] ON [Exchanges] ([Name]);
GO

CREATE UNIQUE INDEX [IX_ExchangeSchedules_ExchangeCode_DayOfWeek] ON [ExchangeSchedules] ([ExchangeCode], [DayOfWeek]);
GO

CREATE UNIQUE INDEX [IX_FxRates_RateDate_BaseCurrencyCode_QuoteCurrencyCode] ON [FxRates] ([RateDate], [BaseCurrencyCode], [QuoteCurrencyCode]);
GO

CREATE INDEX [IX_Securities_ExchangeCode] ON [Securities] ([ExchangeCode]);
GO

CREATE UNIQUE INDEX [IX_Securities_Name] ON [Securities] ([Name]);
GO

CREATE INDEX [IX_SecurityPrices_SecurityId] ON [SecurityPrices] ([SecurityId]);
GO

CREATE UNIQUE INDEX [IX_SecurityPrices_TradeDate_SecurityId] ON [SecurityPrices] ([TradeDate], [SecurityId]);
GO

CREATE UNIQUE INDEX [IX_SecuritySymbols_SecurityId_Standard] ON [SecuritySymbols] ([SecurityId], [Standard]);
GO

CREATE UNIQUE INDEX [IX_TimeZones_Name] ON [TimeZones] ([Name]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20230404012227_InitialCreate', N'7.0.4');
GO

COMMIT;
GO

