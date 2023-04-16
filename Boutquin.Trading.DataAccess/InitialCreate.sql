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
    [Id] int NOT NULL,
    [Description] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_AssetClasses] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Continents] (
    [Code] nvarchar(2) NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Continents] PRIMARY KEY ([Code])
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

CREATE TABLE [SymbolStandards] (
    [Id] int NOT NULL,
    [Description] nvarchar(20) NOT NULL,
    CONSTRAINT [PK_SymbolStandards] PRIMARY KEY ([Id])
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

CREATE TABLE [Countries] (
    [Code] nvarchar(2) NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [NumericCode] int NOT NULL,
    [CurrencyCode] nvarchar(3) NOT NULL,
    [ContinentCode] nvarchar(2) NOT NULL,
    CONSTRAINT [PK_Countries] PRIMARY KEY ([Code]),
    CONSTRAINT [FK_Countries_Continents_ContinentCode] FOREIGN KEY ([ContinentCode]) REFERENCES [Continents] ([Code]) ON DELETE CASCADE,
    CONSTRAINT [FK_Countries_Currencies_CurrencyCode] FOREIGN KEY ([CurrencyCode]) REFERENCES [Currencies] ([Code]) ON DELETE CASCADE
);
GO

CREATE TABLE [FxRates] (
    [Id] int NOT NULL IDENTITY,
    [RateDate] Date NOT NULL,
    [BaseCurrencyCode] nvarchar(3) NOT NULL,
    [QuoteCurrencyCode] nvarchar(3) NOT NULL,
    [Rate] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_FxRates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FxRates_Currencies_BaseCurrencyCode] FOREIGN KEY ([BaseCurrencyCode]) REFERENCES [Currencies] ([Code]) ON DELETE CASCADE,
    CONSTRAINT [FK_FxRates_Currencies_QuoteCurrencyCode] FOREIGN KEY ([QuoteCurrencyCode]) REFERENCES [Currencies] ([Code]) ON DELETE CASCADE
);
GO

CREATE TABLE [Cities] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(50) NOT NULL,
    [TimeZoneCode] nvarchar(10) NOT NULL,
    [CountryCode] nvarchar(2) NOT NULL,
    CONSTRAINT [PK_Cities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Cities_Countries_CountryCode] FOREIGN KEY ([CountryCode]) REFERENCES [Countries] ([Code]) ON DELETE CASCADE,
    CONSTRAINT [FK_Cities_TimeZones_TimeZoneCode] FOREIGN KEY ([TimeZoneCode]) REFERENCES [TimeZones] ([Code]) ON DELETE CASCADE
);
GO

CREATE TABLE [Exchanges] (
    [Code] nvarchar(4) NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [CityId] int NOT NULL,
    CONSTRAINT [PK_Exchanges] PRIMARY KEY ([Code]),
    CONSTRAINT [FK_Exchanges_Cities_CityId] FOREIGN KEY ([CityId]) REFERENCES [Cities] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ExchangeHolidays] (
    [Id] int NOT NULL IDENTITY,
    [ExchangeCode] nvarchar(4) NOT NULL,
    [HolidayDate] Date NOT NULL,
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
    [AssetClassCode] int NOT NULL,
    [ExchangeCode] nvarchar(4) NOT NULL,
    CONSTRAINT [PK_Securities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Securities_AssetClasses_AssetClassCode] FOREIGN KEY ([AssetClassCode]) REFERENCES [AssetClasses] ([Id]) ON DELETE CASCADE,
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
    [Standard] int NOT NULL,
    CONSTRAINT [PK_SecuritySymbols] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SecuritySymbols_Securities_SecurityId] FOREIGN KEY ([SecurityId]) REFERENCES [Securities] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SecuritySymbols_SymbolStandards_Standard] FOREIGN KEY ([Standard]) REFERENCES [SymbolStandards] ([Id]) ON DELETE CASCADE
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Description') AND [object_id] = OBJECT_ID(N'[AssetClasses]'))
    SET IDENTITY_INSERT [AssetClasses] ON;
INSERT INTO [AssetClasses] ([Id], [Description])
VALUES (0, N'Cash or Cash Equivalents'),
(1, N'Fixed Income Securities'),
(2, N'Equity Securities'),
(3, N'Real Estate'),
(4, N'Commodities'),
(5, N'Alternative Investments'),
(6, N'Crypto-Currencies'),
(7, N'Other');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Description') AND [object_id] = OBJECT_ID(N'[AssetClasses]'))
    SET IDENTITY_INSERT [AssetClasses] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'Name') AND [object_id] = OBJECT_ID(N'[Continents]'))
    SET IDENTITY_INSERT [Continents] ON;
INSERT INTO [Continents] ([Code], [Name])
VALUES (N'AF', N'Africa'),
(N'AN', N'Antarctica'),
(N'AS', N'Asia'),
(N'EU', N'Europe'),
(N'NA', N'North America'),
(N'OC', N'Oceania'),
(N'SA', N'South America');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'Name') AND [object_id] = OBJECT_ID(N'[Continents]'))
    SET IDENTITY_INSERT [Continents] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'Name', N'NumericCode', N'Symbol') AND [object_id] = OBJECT_ID(N'[Currencies]'))
    SET IDENTITY_INSERT [Currencies] ON;
INSERT INTO [Currencies] ([Code], [Name], [NumericCode], [Symbol])
VALUES (N'AUD', N'Australian dollar', 36, N'$'),
(N'BRL', N'Brazilian real', 986, N'R$'),
(N'CAD', N'Canadian dollar', 124, N'$'),
(N'CNY', N'Chinese yuan', 156, N'¥'),
(N'EUR', N'Euro', 978, N'€'),
(N'GBP', N'British pound', 826, N'£'),
(N'INR', N'Indian rupee', 356, N'₹'),
(N'JPY', N'Japanese yen', 392, N'¥'),
(N'KRW', N'South Korean won', 410, N'₩'),
(N'MXN', N'Mexican peso', 484, N'$'),
(N'RUB', N'Russian ruble', 643, N'₽'),
(N'USD', N'United States dollar', 840, N'$');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'Name', N'NumericCode', N'Symbol') AND [object_id] = OBJECT_ID(N'[Currencies]'))
    SET IDENTITY_INSERT [Currencies] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Description') AND [object_id] = OBJECT_ID(N'[SymbolStandards]'))
    SET IDENTITY_INSERT [SymbolStandards] ON;
INSERT INTO [SymbolStandards] ([Id], [Description])
VALUES (0, N'CUSIP'),
(1, N'ISIN'),
(2, N'SEDOL'),
(3, N'RIC'),
(4, N'Bloomberg Ticker');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Description') AND [object_id] = OBJECT_ID(N'[SymbolStandards]'))
    SET IDENTITY_INSERT [SymbolStandards] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'ContinentCode', N'CurrencyCode', N'Name', N'NumericCode') AND [object_id] = OBJECT_ID(N'[Countries]'))
    SET IDENTITY_INSERT [Countries] ON;
INSERT INTO [Countries] ([Code], [ContinentCode], [CurrencyCode], [Name], [NumericCode])
VALUES (N'CA', N'NA', N'CAD', N'Canada', 124),
(N'CN', N'AS', N'CNY', N'China', 156),
(N'DE', N'EU', N'EUR', N'Germany', 276),
(N'FR', N'EU', N'EUR', N'France', 250),
(N'GB', N'EU', N'GBP', N'United Kingdom', 826),
(N'IN', N'AS', N'INR', N'India', 356),
(N'JP', N'AS', N'JPY', N'Japan', 392),
(N'KR', N'AS', N'KRW', N'South Korea', 410),
(N'RU', N'EU', N'RUB', N'Russia', 643),
(N'US', N'NA', N'USD', N'United States', 840);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'ContinentCode', N'CurrencyCode', N'Name', N'NumericCode') AND [object_id] = OBJECT_ID(N'[Countries]'))
    SET IDENTITY_INSERT [Countries] OFF;
GO

CREATE UNIQUE INDEX [IX_AssetClasses_Description] ON [AssetClasses] ([Description]);
GO

CREATE UNIQUE INDEX [IX_Cities_CountryCode_Name] ON [Cities] ([CountryCode], [Name]);
GO

CREATE INDEX [IX_Cities_TimeZoneCode] ON [Cities] ([TimeZoneCode]);
GO

CREATE UNIQUE INDEX [IX_Continents_Name] ON [Continents] ([Name]);
GO

CREATE INDEX [IX_Countries_ContinentCode] ON [Countries] ([ContinentCode]);
GO

CREATE INDEX [IX_Countries_CurrencyCode] ON [Countries] ([CurrencyCode]);
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

CREATE INDEX [IX_Exchanges_CityId] ON [Exchanges] ([CityId]);
GO

CREATE UNIQUE INDEX [IX_Exchanges_Name] ON [Exchanges] ([Name]);
GO

CREATE UNIQUE INDEX [IX_ExchangeSchedules_ExchangeCode_DayOfWeek] ON [ExchangeSchedules] ([ExchangeCode], [DayOfWeek]);
GO

CREATE INDEX [IX_FxRates_BaseCurrencyCode] ON [FxRates] ([BaseCurrencyCode]);
GO

CREATE INDEX [IX_FxRates_QuoteCurrencyCode] ON [FxRates] ([QuoteCurrencyCode]);
GO

CREATE UNIQUE INDEX [IX_FxRates_RateDate_BaseCurrencyCode_QuoteCurrencyCode] ON [FxRates] ([RateDate], [BaseCurrencyCode], [QuoteCurrencyCode]);
GO

CREATE INDEX [IX_Securities_AssetClassCode] ON [Securities] ([AssetClassCode]);
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

CREATE INDEX [IX_SecuritySymbols_Standard] ON [SecuritySymbols] ([Standard]);
GO

CREATE UNIQUE INDEX [IX_SymbolStandards_Description] ON [SymbolStandards] ([Description]);
GO

CREATE UNIQUE INDEX [IX_TimeZones_Name] ON [TimeZones] ([Name]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20230416214556_InitialCreate', N'7.0.5');
GO

COMMIT;
GO

