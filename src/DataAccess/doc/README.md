# Boutquin.Trading.DataAccess

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading.dataaccess?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)

## Security Master Data Access Layer

EF Core data access layer for the Security Master system.

The `SecurityMasterDbContext` encompasses the following 14 entities:

1. AssetClass
2. City
3. Continent
4. Country
5. Currency
6. Exchange
7. ExchangeHoliday
8. ExchangeSchedule
9. FxRate
10. Security
11. SecurityPrice
12. SecuritySymbol
13. SymbolStandard
14. TimeZone

## Getting Started

1. Install the NuGet package: `dotnet add package Boutquin.Trading.DataAccess`
2. Configure your connection string via `IConfiguration`
3. Run EF Core migrations: `dotnet ef database update`

## Configuration Classes

The configuration classes set up entity relationships, column types, lengths, and constraints for each database table.

## Disclaimer

Boutquin.Trading is open-source software provided under the Apache 2.0 License. It is a general-purpose research and backtesting tool intended for educational purposes only.

**This software does not constitute financial advice.** All historical performance data represents backtested results computed using actual historical index and ETF return data. Backtested performance is hypothetical and does not represent actual trading. Actual investment results may differ materially. Past performance is not indicative of future results.

The software authors are not registered investment advisers, portfolio managers, or financial planners. Use of this software to make investment decisions is entirely at your own risk. Before making any investment decision, consult with a qualified financial professional who understands your individual circumstances, goals, and risk tolerance.

## Contributing

Please read the [contributing guidelines](https://github.com/boutquin/Boutquin.Trading/blob/main/CONTRIBUTING.md) first.

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE file](https://github.com/boutquin/Boutquin.Trading/blob/main/LICENSE.txt) for details.
