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

## Codebase Map

### Project Structure

| Project | Purpose | Key Dependencies |
|---------|---------|-----------------|
| `Boutquin.Trading.Domain` | Core domain: interfaces, events, value objects, enums, extensions | Boutquin.Domain 0.7.0, EF Core Relational 10.0.5, Logging.Abstractions 10.0.5 |
| `Boutquin.Trading.Application` | Backtest engine, portfolio, strategies, event handlers, brokers | Domain, System.Linq.Async 7.0.0 |
| `Boutquin.Trading.Data.Tiingo` | Equity data fetcher (Tiingo API) | Domain |
| `Boutquin.Trading.Data.Frankfurter` | FX rate fetcher (Frankfurter API, ECB-sourced) | Domain |
| `Boutquin.Trading.Data.CSV` | CSV data reader | Domain |
| `Boutquin.Trading.Data.Processor` | Data processing pipeline | Domain |
| `Boutquin.Trading.DataAccess` | EF Core data access (SecurityMaster) | Domain |
| `Boutquin.Trading.BackTest` | Backtest runner entry point | Application |
| `Boutquin.Trading.BenchMark` | BenchmarkDotNet performance benchmarks | Domain |
| `Boutquin.Trading.Sample` | Sample usage | Application |
| `Boutquin.Trading.UnitTests` | xUnit tests | xUnit 2.9.3, FluentAssertions 8.8.0, Moq 4.20.70 |
| `Tests/ArchitectureTests` | Architecture fitness functions (NetArchTest) | Domain, Application |

### Key File Locations

| What | Path |
|------|------|
| Financial metrics (extension methods on `decimal[]`) | `Domain/Extensions/DecimalArrayExtensions.cs` |
| Equity curve drawdown analysis | `Domain/Extensions/EquityCurveExtensions.cs` |
| Tearsheet record (performance summary) | `Domain/Helpers/TearSheet.cs` |
| IStrategy interface (with default impls) | `Domain/Interfaces/IStrategy.cs` |
| IPortfolio interface (14 methods) | `Domain/Interfaces/IPortfolio.cs` |
| IPositionSizer interface | `Domain/Interfaces/IPositionSizer.cs` |
| IBrokerage interface | `Domain/Interfaces/IBrokerage.cs` |
| ICapitalAllocationStrategy (no impls yet) | `Domain/Interfaces/ICapitalAllocationStrategy.cs` |
| MarketData record | `Domain/Data/MarketData.cs` |
| Event records (Market/Signal/Order/Fill) | `Domain/Events/` |
| CalculationException | `Domain/Exceptions/CalculationException.cs` |
| Portfolio implementation | `Application/Portfolio.cs` |
| Backtest engine | `Application/Backtest.cs` |
| SimulatedBrokerage | `Application/Brokers/SimulatedBrokerage.cs` |
| BuyAndHoldStrategy | `Application/Strategies/BuyAndHoldStrategy.cs` |
| RebalancingBuyAndHoldStrategy | `Application/Strategies/RebalancingBuyAndHoldStrategy.cs` |
| FixedWeightPositionSizer | `Application/PositionSizing/FixedWeightPositionSizer.cs` |
| Event handlers | `Application/EventHandlers/` |
| CompositeMarketDataFetcher | `Application/CompositeMarketDataFetcher.cs` |

### Domain Interfaces (14)

`IBrokerage`, `ICapitalAllocationStrategy`, `ICurrencyConversionService`, `IEventHandler`, `IEventProcessor`, `IFinancialEvent`, `IMarketDataFetcher`, `IMarketDataProcessor`, `IMarketDataStorage`, `IOrderPriceCalculationStrategy`, `IPortfolio`, `IPositionSizer`, `IStrategy`, `ISymbolReader`

### Domain Enums (12)

`AssetClassCode`, `AssetType`, `ContinentCode`, `CountryCode`, `CurrencyCode`, `ExchangeCode`, `OrderType`, `RebalancingFrequency`, `SecuritySymbolStandard`, `SignalType`, `TimeZoneCode`, `TradeAction`

### Test Patterns

- **Framework:** xUnit + FluentAssertions + Moq
- **Precision:** `private const decimal Precision = 1e-12m;` for financial metric comparisons
- **Data pattern:** `[Theory]` + `[MemberData(nameof(TestDataClass.Prop), MemberType = typeof(TestDataClass))]`
- **Data classes:** Separate `*TestData.cs` files with `public static IEnumerable<object[]>` properties using collection initializer `[...]`
- **Naming:** `MethodName_Scenario_ExpectedBehavior` or `MethodName_ShouldReturnCorrectResult`
- **Namespace:** `Boutquin.Trading.Tests.UnitTests.{Layer}` (Domain, Application, Data, DataAccess)
- **All test classes are `public sealed`**
- **CalculationException:** Always use fully qualified `Boutquin.Trading.Domain.Exceptions.CalculationException` in test assertions (not in GlobalUsings)
