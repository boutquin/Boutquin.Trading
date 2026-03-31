# Boutquin.Trading.Data.Tiingo

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading.data.tiingo?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)

## Tiingo Data Provider

Equity and ETF market data fetcher for the [Boutquin.Trading](https://github.com/boutquin/Boutquin.Trading) quantitative trading framework. Provides historical OHLCV, adjusted prices, dividends, and splits via the Tiingo REST API.

### Features

- Implements `IMarketDataFetcher` from `Boutquin.Trading.Domain`
- Historical daily OHLCV data with adjusted close prices
- Multi-symbol batch fetching
- Supports `CancellationToken` for cooperative cancellation
- Compatible with L1/L2 caching decorators

### Usage

```csharp
var fetcher = new TiingoFetcher(httpClient, apiKey);
var data = await fetcher.FetchMarketDataAsync(symbols, cancellationToken);
```

Typically used via `CompositeMarketDataFetcher` in `Boutquin.Trading.Application`, which routes equity requests to this fetcher and FX requests to `FrankfurterFetcher`.

### API Key

Requires a free Tiingo API key. Pass via constructor or configuration.

## Installation

```sh
dotnet add package Boutquin.Trading.Data.Tiingo
```

## Disclaimer

Boutquin.Trading is open-source software provided under the Apache 2.0 License. It is a general-purpose research and backtesting tool intended for educational purposes only.

**This software does not constitute financial advice.** All historical performance data represents backtested results computed using actual historical index and ETF return data. Backtested performance is hypothetical and does not represent actual trading. Actual investment results may differ materially. Past performance is not indicative of future results.

The software authors are not registered investment advisers, portfolio managers, or financial planners. Use of this software to make investment decisions is entirely at your own risk. Before making any investment decision, consult with a qualified financial professional who understands your individual circumstances, goals, and risk tolerance.

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE file](https://github.com/boutquin/Boutquin.Trading/blob/main/LICENSE.txt) for details.
