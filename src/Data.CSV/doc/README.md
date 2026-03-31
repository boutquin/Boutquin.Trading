# Boutquin.Trading.Data.CSV

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading.data.csv?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)

## CSV Data Provider

CSV-based storage and retrieval for the [Boutquin.Trading](https://github.com/boutquin/Boutquin.Trading) quantitative trading framework. Reads and writes daily OHLCV, FX rate, economic, and factor data in CSV format.

Part of the framework's data layer and used as the L2 disk cache backing store.

### Components

| Class | Description |
|-------|-------------|
| `CsvMarketDataFetcher` | Reads daily OHLCV + dividend/split data from per-symbol CSV files; aggregates by date for multi-symbol fetches (one entry per date with all symbols) |
| `CsvMarketDataStorage` | Writes market data to per-symbol CSV files (atomic tmp + rename) |
| `CsvEconomicDataFetcher` | Reads scalar economic time series from `fred_{seriesId}.csv` |
| `CsvEconomicDataStorage` | Writes economic data to CSV |
| `CsvFactorDataFetcher` | Reads multi-factor return series from `ff_{dataset}_{frequency}.csv` |
| `CsvFactorDataStorage` | Writes factor data to CSV with header-driven factor names |
| `CsvSymbolReader` | Reads ticker symbol lists from CSV |
| `MarketDataFileNameHelper` | Generates consistent file names for per-symbol CSV storage |

### Usage

```csharp
var fetcher = new CsvMarketDataFetcher(dataDirectory);
var data = await fetcher.FetchMarketDataAsync(symbols, cancellationToken);
```

Typically used indirectly via the L2 write-through cache decorators in `Boutquin.Trading.Application`.

## Installation

```sh
dotnet add package Boutquin.Trading.Data.CSV
```

## Disclaimer

Boutquin.Trading is open-source software provided under the Apache 2.0 License. It is a general-purpose research and backtesting tool intended for educational purposes only.

**This software does not constitute financial advice.** All historical performance data represents backtested results computed using actual historical index and ETF return data. Backtested performance is hypothetical and does not represent actual trading. Actual investment results may differ materially. Past performance is not indicative of future results.

The software authors are not registered investment advisers, portfolio managers, or financial planners. Use of this software to make investment decisions is entirely at your own risk. Before making any investment decision, consult with a qualified financial professional who understands your individual circumstances, goals, and risk tolerance.

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE file](https://github.com/boutquin/Boutquin.Trading/blob/main/LICENSE.txt) for details.
