# Boutquin.Trading.Data.TwelveData

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading.data.twelvedata?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)

## Twelve Data Provider

Equity market data fetcher for the [Boutquin.Trading](https://github.com/boutquin/Boutquin.Trading) quantitative trading framework. Provides OHLCV, adjusted prices, dividends, and splits via the Twelve Data REST API.

### Features

- Implements `IMarketDataFetcher` from `Boutquin.Trading.Domain`
- Merges three Twelve Data endpoints per symbol: `/time_series` (OHLCV), `/dividends`, and `/splits`
- Dividend/split fetch failures are non-fatal (returns empty); time series failure throws `MarketDataRetrievalException`
- Multi-symbol batch fetching
- Supports `CancellationToken` for cooperative cancellation
- Compatible with L1/L2 caching decorators

### Usage

```csharp
var fetcher = new TwelveDataFetcher(httpClient, apiKey);
var data = await fetcher.FetchMarketDataAsync(symbols, cancellationToken);
```

Typically used via `CompositeMarketDataFetcher` in `Boutquin.Trading.Application`, which routes equity requests to this fetcher and FX requests to `FrankfurterFetcher`.

### API Key

Requires a Twelve Data API key. Pass via constructor or configuration.

## Installation

```sh
dotnet add package Boutquin.Trading.Data.TwelveData
```

## Disclaimer

Boutquin.Trading is open-source software provided under the Apache 2.0 License. It is a general-purpose research and backtesting tool intended for educational purposes only.

**This software does not constitute financial advice.** All historical performance data represents backtested results computed using actual historical index and ETF return data. Backtested performance is hypothetical and does not represent actual trading. Actual investment results may differ materially. Past performance is not indicative of future results.

The software authors are not registered investment advisers, portfolio managers, or financial planners. Use of this software to make investment decisions is entirely at your own risk. Before making any investment decision, consult with a qualified financial professional who understands your individual circumstances, goals, and risk tolerance.

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE file](https://github.com/boutquin/Boutquin.Trading/blob/main/LICENSE.txt) for details.
