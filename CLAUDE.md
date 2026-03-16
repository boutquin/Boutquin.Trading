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

## Portfolio Construction Architecture (Phase 2)

- **`IPortfolioConstructionModel`** — Core interface: `ComputeTargetWeights(assets, returns) → Dictionary<Asset, decimal>`. All models guarantee: weights ≥ 0, weights sum to 1.0, empty assets → empty weights.
- **Six construction models** in `Application/PortfolioConstruction/`: `EqualWeightConstruction`, `InverseVolatilityConstruction`, `RiskParityConstruction`, `MeanVarianceConstruction`, `MinimumVarianceConstruction`, `BlackLittermanConstruction`. The folder name `PortfolioConstruction` matches the roadmap naming convention.
- **`ICovarianceEstimator`** — Three implementations: `SampleCovarianceEstimator` (N-1 divisor), `ExponentiallyWeightedCovarianceEstimator` (EWMA with configurable lambda), `LedoitWolfShrinkageEstimator` (shrinkage toward scaled identity). All use `SampleCovarianceEstimator.ValidateReturns()` for input validation.
- **`RollingWindow<T>`** — Generic circular buffer in `Domain/Helpers/`. Fixed capacity, drops oldest on add, chronological iteration. Used for windowed return series.
- **`IRebalancingTrigger`** — Two implementations: `CalendarRebalancingTrigger` (always true, calendar logic in strategy), `ThresholdRebalancingTrigger` (fires when any asset drifts beyond band).
- **`ConstructionModelStrategy`** — Wires `IPortfolioConstructionModel` + `IRebalancingTrigger` + `RebalancingFrequency` into the strategy pipeline. Extracts rolling returns from `historicalMarketData`, computes target weights dynamically at each rebalance. Stores `LastComputedWeights` for `DynamicWeightPositionSizer` to read.
- **`DynamicWeightPositionSizer`** — Reads `LastComputedWeights` from `ConstructionModelStrategy` to compute position sizes. Falls back to equal weight if no computed weights available.
- **Optimization approach** — `MeanVarianceConstruction` and `MinimumVarianceConstruction` use projected gradient descent with line search and simplex projection. `RiskParityConstruction` uses iterative inverse-MRC algorithm.

## Analytics & Attribution Architecture (Phase 3)

- **`BrinsonFachlerAttributor`** — Static class implementing Brinson-Fachler single-period performance attribution. Decomposes active return into allocation effect `(Wp-Wb)(Rb_sector-Rb_total)`, selection effect `Wb(Rp_sector-Rb_sector)`, and interaction effect `(Wp-Wb)(Rp_sector-Rb_sector)`. Returns per-asset and total effects. Effects sum to total active return.
- **`FactorRegressor`** — Multi-factor OLS regression via normal equations with Gaussian elimination + partial pivoting. Regresses portfolio returns against Fama-French (or custom) factors. Returns alpha, per-factor betas, R², and residual standard error.
- **`CorrelationAnalyzer`** — Computes full N×N correlation matrix from return series (sample covariance, N-1 divisor). Computes diversification ratio = weighted avg vol / portfolio vol. Also provides rolling pairwise correlation time series.
- **`DrawdownAnalyzer`** — Identifies discrete drawdown periods from an equity curve: tracks peak → trough → recovery transitions. Each `DrawdownPeriod` record includes start date, trough date, recovery date (nullable if ongoing), depth, duration days, and recovery days.
- **`HtmlReportGenerator`** — Generates self-contained HTML tearsheet with embedded SVG charts (equity curve, drawdown area), metrics table, and monthly returns heatmap. No external JS dependencies.
- **`BenchmarkComparisonReport`** — Generates side-by-side HTML comparison of portfolio vs benchmark. Includes dual equity curve SVG (normalized to 100), metrics comparison table, and annualized tracking error calculation.
- **Domain records** in `Domain/Analytics/`: `BrinsonFachlerResult`, `FactorRegressionResult`, `CorrelationAnalysisResult`, `DrawdownPeriod` — all `sealed record` types.

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
| IStrategy interface | `Domain/Interfaces/IStrategy.cs` |
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
| RollingWindow\<T\> (circular buffer) | `Domain/Helpers/RollingWindow.cs` |
| ICovarianceEstimator interface | `Domain/Interfaces/ICovarianceEstimator.cs` |
| IPortfolioConstructionModel interface | `Domain/Interfaces/IPortfolioConstructionModel.cs` |
| IRebalancingTrigger interface | `Domain/Interfaces/IRebalancingTrigger.cs` |
| Covariance estimators (Sample, EWMA, Ledoit-Wolf) | `Application/CovarianceEstimators/` |
| Portfolio construction models (6 models) | `Application/PortfolioConstruction/` |
| Rebalancing triggers (Calendar, Threshold) | `Application/Rebalancing/` |
| ConstructionModelStrategy | `Application/Strategies/ConstructionModelStrategy.cs` |
| DynamicWeightPositionSizer | `Application/PositionSizing/DynamicWeightPositionSizer.cs` |
| Analytics domain records (4) | `Domain/Analytics/` |
| BrinsonFachlerAttributor | `Application/Analytics/BrinsonFachlerAttributor.cs` |
| FactorRegressor | `Application/Analytics/FactorRegressor.cs` |
| CorrelationAnalyzer | `Application/Analytics/CorrelationAnalyzer.cs` |
| DrawdownAnalyzer | `Application/Analytics/DrawdownAnalyzer.cs` |
| HtmlReportGenerator | `Application/Reporting/HtmlReportGenerator.cs` |
| BenchmarkComparisonReport | `Application/Reporting/BenchmarkComparisonReport.cs` |

### Domain Interfaces (17)

`IBrokerage`, `ICapitalAllocationStrategy`, `ICovarianceEstimator`, `ICurrencyConversionService`, `IEventHandler`, `IEventProcessor`, `IFinancialEvent`, `IMarketDataFetcher`, `IMarketDataProcessor`, `IMarketDataStorage`, `IOrderPriceCalculationStrategy`, `IPortfolio`, `IPortfolioConstructionModel`, `IPositionSizer`, `IRebalancingTrigger`, `IStrategy`, `ISymbolReader`

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
