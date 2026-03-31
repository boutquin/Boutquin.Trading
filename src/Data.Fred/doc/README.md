# Boutquin.Trading.Data.Fred

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading.data.fred?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)

## FRED Economic Data Provider

Federal Reserve Economic Data fetcher for the [Boutquin.Trading](https://github.com/boutquin/Boutquin.Trading) quantitative trading framework. Provides treasury yields, inflation indicators, GDP, and macro time series via the FRED REST API.

### Features

- Implements `IEconomicDataFetcher` from `Boutquin.Trading.Domain`
- Returns `IAsyncEnumerable<KeyValuePair<DateOnly, decimal>>` for a given FRED series ID
- Raw values as FRED provides them (e.g., yields in percent, not decimal) — caller transforms
- Missing values (`"."`) silently skipped
- Supports `CancellationToken` for cooperative cancellation
- Compatible with L1/L2 caching decorators

### Well-Known Series

`FredSeriesConstants` provides constants for common series:

| Constant | FRED Series | Description |
|----------|-------------|-------------|
| Treasury yields | `DGS1MO` through `DGS30` | 1-month to 30-year treasury yields |
| Inflation | `CPIAUCSL`, `T10YIE` | CPI, 10Y breakeven inflation |
| Growth | `GDP`, `GDPC1` | Nominal and real GDP |

### Usage

```csharp
var fetcher = new FredFetcher(httpClient, apiKey);
await foreach (var (date, value) in fetcher.FetchAsync(FredSeriesConstants.DGS10))
{
    // value is in percent (e.g., 4.25 for 4.25%)
}
```

### API Key

Requires a free FRED API key from [fred.stlouisfed.org](https://fred.stlouisfed.org/docs/api/api_key.html).

## Installation

```sh
dotnet add package Boutquin.Trading.Data.Fred
```

## Disclaimer

Boutquin.Trading is open-source software provided under the Apache 2.0 License. It is a general-purpose research and backtesting tool intended for educational purposes only.

**This software does not constitute financial advice.** All historical performance data represents backtested results computed using actual historical index and ETF return data. Backtested performance is hypothetical and does not represent actual trading. Actual investment results may differ materially. Past performance is not indicative of future results.

The software authors are not registered investment advisers, portfolio managers, or financial planners. Use of this software to make investment decisions is entirely at your own risk. Before making any investment decision, consult with a qualified financial professional who understands your individual circumstances, goals, and risk tolerance.

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE file](https://github.com/boutquin/Boutquin.Trading/blob/main/LICENSE.txt) for details.
