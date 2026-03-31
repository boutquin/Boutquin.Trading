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
(N'HKD', N'Hong Kong dollar', 344, N'HK$'),
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

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'Name', N'TimeZoneOffset', N'UsesDaylightSaving') AND [object_id] = OBJECT_ID(N'[TimeZones]'))
    SET IDENTITY_INSERT [TimeZones] ON;
INSERT INTO [TimeZones] ([Code], [Name], [TimeZoneOffset], [UsesDaylightSaving])
VALUES (N'AEST', N'Australian Eastern Standard Time', N'+10:00', CAST(0 AS bit)),
(N'CET', N'Central European Time', N'+01:00', CAST(0 AS bit)),
(N'CST', N'China Standard Time', N'+08:00', CAST(0 AS bit)),
(N'EST', N'Eastern Standard Time', N'-05:00', CAST(0 AS bit)),
(N'GMT', N'Greenwich Mean Time', N'GMT', CAST(0 AS bit)),
(N'HKT', N'Hong Kong Time', N'+08:00', CAST(0 AS bit)),
(N'JST', N'Japan Standard Time', N'+09:00', CAST(0 AS bit)),
(N'MSK', N'Moscow Standard Time', N'+04:00', CAST(0 AS bit)),
(N'UTC', N'Coordinated Universal Time', N'Z', CAST(0 AS bit));
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'Name', N'TimeZoneOffset', N'UsesDaylightSaving') AND [object_id] = OBJECT_ID(N'[TimeZones]'))
    SET IDENTITY_INSERT [TimeZones] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'ContinentCode', N'CurrencyCode', N'Name', N'NumericCode') AND [object_id] = OBJECT_ID(N'[Countries]'))
    SET IDENTITY_INSERT [Countries] ON;
INSERT INTO [Countries] ([Code], [ContinentCode], [CurrencyCode], [Name], [NumericCode])
VALUES (N'CA', N'NA', N'CAD', N'Canada', 124),
(N'CN', N'AS', N'CNY', N'China', 156),
(N'DE', N'EU', N'EUR', N'Germany', 276),
(N'FR', N'EU', N'EUR', N'France', 250),
(N'GB', N'EU', N'GBP', N'United Kingdom', 826),
(N'HK', N'AS', N'HKD', N'Hong Kong', 344),
(N'IN', N'AS', N'INR', N'India', 356),
(N'JP', N'AS', N'JPY', N'Japan', 392),
(N'KR', N'AS', N'KRW', N'South Korea', 410),
(N'RU', N'EU', N'RUB', N'Russia', 643),
(N'US', N'NA', N'USD', N'United States', 840);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'ContinentCode', N'CurrencyCode', N'Name', N'NumericCode') AND [object_id] = OBJECT_ID(N'[Countries]'))
    SET IDENTITY_INSERT [Countries] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CountryCode', N'Name', N'TimeZoneCode') AND [object_id] = OBJECT_ID(N'[Cities]'))
    SET IDENTITY_INSERT [Cities] ON;
INSERT INTO [Cities] ([Id], [CountryCode], [Name], [TimeZoneCode])
VALUES (1, N'US', N'New York', N'UTC'),
(2, N'JP', N'Tokyo', N'JST'),
(3, N'CN', N'Shanghai', N'CST'),
(4, N'HK', N'Hong Kong', N'HKT'),
(5, N'FR', N'Paris', N'CET'),
(6, N'GB', N'London', N'GMT'),
(7, N'DE', N'Frankfurt', N'CET'),
(8, N'RU', N'Moscow', N'MSK'),
(9, N'CA', N'Toronto', N'EST');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CountryCode', N'Name', N'TimeZoneCode') AND [object_id] = OBJECT_ID(N'[Cities]'))
    SET IDENTITY_INSERT [Cities] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'CityId', N'Name') AND [object_id] = OBJECT_ID(N'[Exchanges]'))
    SET IDENTITY_INSERT [Exchanges] ON;
INSERT INTO [Exchanges] ([Code], [CityId], [Name])
VALUES (N'XETR', 7, N'Deutsche Boerse XETRA'),
(N'XHKG', 4, N'Hong Kong Stock Exchange'),
(N'XLON', 6, N'London Stock Exchange'),
(N'XMOS', 8, N'Moscow Exchange'),
(N'XNAS', 1, N'NASDAQ Stock Market'),
(N'XNYS', 1, N'New York Stock Exchange'),
(N'XPAR', 5, N'Euronext Paris'),
(N'XSHG', 3, N'Shanghai Stock Exchange'),
(N'XTOR', 9, N'Toronto Stock Exchange'),
(N'XTSE', 2, N'Tokyo Stock Exchange');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'CityId', N'Name') AND [object_id] = OBJECT_ID(N'[Exchanges]'))
    SET IDENTITY_INSERT [Exchanges] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CloseTime', N'DayOfWeek', N'ExchangeCode', N'OpenTime') AND [object_id] = OBJECT_ID(N'[ExchangeSchedules]'))
    SET IDENTITY_INSERT [ExchangeSchedules] ON;
INSERT INTO [ExchangeSchedules] ([Id], [CloseTime], [DayOfWeek], [ExchangeCode], [OpenTime])
VALUES (1, '16:00:00', 1, N'XNYS', '09:30:00'),
(2, '16:00:00', 2, N'XNYS', '09:30:00'),
(3, '16:00:00', 3, N'XNYS', '09:30:00'),
(4, '16:00:00', 4, N'XNYS', '09:30:00'),
(5, '16:00:00', 5, N'XNYS', '09:30:00'),
(6, '16:00:00', 1, N'XNAS', '09:30:00'),
(7, '16:00:00', 2, N'XNAS', '09:30:00'),
(8, '16:00:00', 3, N'XNAS', '09:30:00'),
(9, '16:00:00', 4, N'XNAS', '09:30:00'),
(10, '16:00:00', 5, N'XNAS', '09:30:00'),
(11, '15:00:00', 1, N'XTSE', '09:00:00'),
(12, '15:00:00', 2, N'XTSE', '09:00:00'),
(13, '15:00:00', 3, N'XTSE', '09:00:00'),
(14, '15:00:00', 4, N'XTSE', '09:00:00'),
(15, '15:00:00', 5, N'XTSE', '09:00:00'),
(16, '15:00:00', 1, N'XSHG', '09:30:00'),
(17, '15:00:00', 2, N'XSHG', '09:30:00'),
(18, '15:00:00', 3, N'XSHG', '09:30:00'),
(19, '15:00:00', 4, N'XSHG', '09:30:00'),
(20, '15:00:00', 5, N'XSHG', '09:30:00'),
(21, '16:00:00', 1, N'XHKG', '09:30:00'),
(22, '16:00:00', 2, N'XHKG', '09:30:00'),
(23, '16:00:00', 3, N'XHKG', '09:30:00'),
(24, '16:00:00', 4, N'XHKG', '09:30:00'),
(25, '16:00:00', 5, N'XHKG', '09:30:00'),
(26, '17:30:00', 1, N'XPAR', '09:00:00'),
(27, '17:30:00', 2, N'XPAR', '09:00:00'),
(28, '17:30:00', 3, N'XPAR', '09:00:00'),
(29, '17:30:00', 4, N'XPAR', '09:00:00'),
(30, '17:30:00', 5, N'XPAR', '09:00:00'),
(31, '16:30:00', 1, N'XLON', '08:00:00'),
(32, '16:30:00', 2, N'XLON', '08:00:00'),
(33, '16:30:00', 3, N'XLON', '08:00:00'),
(34, '16:30:00', 4, N'XLON', '08:00:00'),
(35, '16:30:00', 5, N'XLON', '08:00:00'),
(36, '17:30:00', 1, N'XETR', '09:00:00'),
(37, '17:30:00', 2, N'XETR', '09:00:00'),
(38, '17:30:00', 3, N'XETR', '09:00:00'),
(39, '17:30:00', 4, N'XETR', '09:00:00'),
(40, '17:30:00', 5, N'XETR', '09:00:00'),
(41, '18:45:00', 1, N'XMOS', '10:00:00'),
(42, '18:45:00', 2, N'XMOS', '10:00:00');
INSERT INTO [ExchangeSchedules] ([Id], [CloseTime], [DayOfWeek], [ExchangeCode], [OpenTime])
VALUES (43, '18:45:00', 3, N'XMOS', '10:00:00'),
(44, '18:45:00', 4, N'XMOS', '10:00:00'),
(45, '18:45:00', 5, N'XMOS', '10:00:00'),
(46, '16:00:00', 1, N'XTOR', '09:30:00'),
(47, '16:00:00', 2, N'XTOR', '09:30:00'),
(48, '16:00:00', 3, N'XTOR', '09:30:00'),
(49, '16:00:00', 4, N'XTOR', '09:30:00'),
(50, '16:00:00', 5, N'XTOR', '09:30:00');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CloseTime', N'DayOfWeek', N'ExchangeCode', N'OpenTime') AND [object_id] = OBJECT_ID(N'[ExchangeSchedules]'))
    SET IDENTITY_INSERT [ExchangeSchedules] OFF;
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
VALUES (N'20230417025851_InitialCreate', N'7.0.5');
GO

COMMIT;
GO

