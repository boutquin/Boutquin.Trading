# Boutquin.Trading

> **Language conventions:** `~/.claude/conventions/dotnet-conventions.md`

Quantitative trading framework in C# .NET. Pre-release.

## Remotes

- `origin` → `boutquin/Boutquin.Trading.Dev` (private, full history)
- `public` → `boutquin/Boutquin.Trading` (private, release repo — not yet public)

## Financial Calculation Conventions

- **Sample divisor (N-1) for all deviation/variance calculations** — `DownsideDeviation`, `StandardDeviation`, covariance in `Beta` all use sample-based divisor (`Length - 1`), not population (`Length`). This is the standard for financial time series where we're estimating from a sample.
- **Zero-denominator guards throw `CalculationException`** — When a denominator is zero in ratio calculations (Sharpe, Sortino, Beta, InformationRatio), throw `Boutquin.Trading.Domain.Exceptions.CalculationException` rather than returning `Infinity` or `NaN`. Zero denominators indicate degenerate inputs (constant returns, zero variance) that should be surfaced, not silently propagated.
- **Array length matching in paired calculations** — `Beta` and other methods that take paired arrays (portfolio vs benchmark) must guard `portfolioDailyReturns.Length != benchmarkDailyReturns.Length` with `ArgumentException`. Silent truncation via `Zip` hides data misalignment bugs.
- **`CalculationException` namespace** — Use fully qualified `Boutquin.Trading.Domain.Exceptions.CalculationException` in test assertions to avoid CS0104 ambiguity with `Boutquin.Domain.Exceptions.ExceptionMessages`. Do not add `global using Boutquin.Trading.Domain.Exceptions` to `GlobalUsings.cs`.

## Event Pipeline Architecture

- **FillEvent includes `TradeAction`** — The `FillEvent` record carries `TradeAction` (Buy/Sell) so that `FillEventHandler` can correctly branch: Buy deducts `(price * qty + commission)`, Sell credits `(price * qty - commission)`.
- **MarketEventHandler feeds signals to event processor** — `GenerateSignals` return value must be captured and each signal fed into `portfolio.HandleEventAsync(signal)`. Discarding the return value silently drops all trading signals.
- **SimulatedBrokerage filters by order timestamp** — `FetchMarketDataAsync` results must be filtered to match `order.Timestamp`. Without this, backtests use wrong-date market data (e.g., latest close instead of the order's historical date).

## Data Fetcher Architecture

- **Composite pattern for `IMarketDataFetcher`** — `CompositeMarketDataFetcher` delegates `FetchMarketDataAsync` → `TiingoFetcher` (equities) and `FetchFxRatesAsync` → `FrankfurterFetcher` (FX rates). Single-responsibility fetchers that each throw `NotSupportedException` for the method they don't handle. Consumers use the composite.
- **Deprecating a data provider project** — Checklist: (1) delete project directory, (2) `dotnet sln remove`, (3) remove `ProjectReference` entries from all consuming `.csproj` files, (4) update `GlobalUsings.cs` and source code to use replacement, (5) add new `ProjectReference` entries for replacement projects, (6) grep for old namespace to catch stragglers.
