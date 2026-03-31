# Boutquin.Trading.Data.FamaFrench

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading.data.famafrench?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)

## Fama-French Factor Data Provider

Academic factor return data fetcher for the [Boutquin.Trading](https://github.com/boutquin/Boutquin.Trading) quantitative trading framework. Downloads factor return series from the Kenneth R. French Data Library.

### Features

- Implements `IFactorDataFetcher` from `Boutquin.Trading.Domain`
- Returns `IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>` — all factors from a dataset in one async stream
- No API key required (public data library)
- Supports 3-factor, 5-factor, and momentum datasets in both daily and monthly frequencies
- Values in percentage (caller transforms) — missing values (`-99.99`, `-999`) silently skipped
- Monthly annual summary section excluded automatically
- Downloads ZIP/CSV directly from the Ken French Data Library
- Supports `CancellationToken` for cooperative cancellation
- Compatible with L1/L2 caching decorators

### Supported Datasets

The `FamaFrenchDataset` enum constrains valid identifiers:

| Dataset | Factors |
|---------|---------|
| `ThreeFactors` | `Mkt-RF`, `SMB`, `HML`, `RF` |
| `FiveFactors` | `Mkt-RF`, `SMB`, `HML`, `RMW`, `CMA`, `RF` |
| `Momentum` | `Mom` |

Factor names are defined in `FamaFrenchConstants` (in `Boutquin.Trading.Domain`).

### Usage

```csharp
var fetcher = new FamaFrenchFetcher(httpClient);
await foreach (var (date, factors) in fetcher.FetchFactorDataAsync(
    FamaFrenchDataset.FiveFactors, startDate, endDate))
{
    var marketPremium = factors[FamaFrenchConstants.MktRf]; // in percent
}
```

## Installation

```sh
dotnet add package Boutquin.Trading.Data.FamaFrench
```

## Disclaimer

Boutquin.Trading is open-source software provided under the Apache 2.0 License. It is a general-purpose research and backtesting tool intended for educational purposes only.

**This software does not constitute financial advice.** All historical performance data represents backtested results computed using actual historical index and ETF return data. Backtested performance is hypothetical and does not represent actual trading. Actual investment results may differ materially. Past performance is not indicative of future results.

The software authors are not registered investment advisers, portfolio managers, or financial planners. Use of this software to make investment decisions is entirely at your own risk. Before making any investment decision, consult with a qualified financial professional who understands your individual circumstances, goals, and risk tolerance.

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE file](https://github.com/boutquin/Boutquin.Trading/blob/main/LICENSE.txt) for details.
