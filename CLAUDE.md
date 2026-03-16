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
- **Six base construction models** in `Application/PortfolioConstruction/`: `EqualWeightConstruction`, `InverseVolatilityConstruction`, `RiskParityConstruction`, `MeanVarianceConstruction`, `MinimumVarianceConstruction`, `BlackLittermanConstruction`. The folder name `PortfolioConstruction` matches the roadmap naming convention.
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

## Tactical Enhancements Architecture (Phase 4)

- **`IIndicator`** — Core interface: `Compute(decimal[] values) → decimal`. Single indicator value from a time series. Implementations: `SimpleMovingAverage` (last N values), `ExponentialMovingAverage` (α = 2/(period+1), SMA seed), `RealizedVolatility` (rolling annualized std dev), `MomentumScore` (12-1 month cumulative return, excludes most recent month).
- **`IMacroIndicator`** — Dual-series interface: `Compute(series1, series2) → decimal`. Implementations: `SpreadIndicator` (latest difference), `RateOfChangeIndicator` (spread momentum with lookback).
- **`EconomicRegime` enum** — Four quadrants: `RisingGrowthRisingInflation`, `RisingGrowthFallingInflation`, `FallingGrowthRisingInflation`, `FallingGrowthFallingInflation`.
- **`IRegimeClassifier`** — `Classify(growthSignal, inflationSignal) → EconomicRegime`. Implementation: `GrowthInflationRegimeClassifier` with configurable deadband for hysteresis (ambiguous signals within deadband use prior regime).
- **`TacticalOverlayConstruction`** — `IPortfolioConstructionModel` that wraps a base model and applies regime-specific tilts (additive) plus optional momentum scoring. Re-normalizes weights to sum to 1.0. Floors negative weights at zero.
- **`VolatilityTargetingConstruction`** — `IPortfolioConstructionModel` that scales base model weights by `targetVol / realizedVol`, capped at `maxLeverage`. Computes realized portfolio vol from weighted return series. Falls back to base weights if insufficient data.
- **`WalkForwardOptimizer`** — Rolling in-sample/out-of-sample validation. Selects best parameter set in-sample (by Sharpe), evaluates out-of-sample. Returns `WalkForwardResult` per fold. No look-ahead bias (OOS start always after IS end).
- **`MonteCarloSimulator`** — Bootstrap resampling of daily returns. Produces distribution of Sharpe ratios across N simulations. Reports median, 5th/95th percentile, and mean. Supports deterministic seed for reproducibility.
- **`IUniverseSelector`** — `Select(candidates) → filtered list`. Implementations: `MinAumFilter`, `MinAgeFilter` (inception date), `LiquidityFilter` (average daily volume). `CompositeUniverseSelector` composes with AND logic.
- **`AssetMetadata`** — Domain record in `Domain/Analytics/` with `Asset`, `AumMillions`, `InceptionDate`, `AverageDailyVolume`. Used by universe filters.
- **Domain records** in `Domain/Analytics/`: `WalkForwardResult` (per-fold IS/OOS results), `MonteCarloResult` (simulation distribution) — both `sealed record` types.
- **Indicators** in `Application/Indicators/`: `SimpleMovingAverage`, `ExponentialMovingAverage`, `RealizedVolatility`, `MomentumScore`, `SpreadIndicator`, `RateOfChangeIndicator`.
- **Regime** in `Application/Regime/`: `GrowthInflationRegimeClassifier`.
- **Universe** in `Application/Universe/`: `MinAumFilter`, `MinAgeFilter`, `LiquidityFilter`, `CompositeUniverseSelector`.

## Infrastructure Polish Architecture (Phase 5)

- **`IRiskManager` / `IRiskRule`** — Composite risk management. `IRiskRule.Evaluate(Order, IPortfolio) → RiskEvaluation`. Three built-in rules: `MaxDrawdownRule` (rejects when equity curve drawdown exceeds limit), `MaxPositionSizeRule` (rejects when single position exceeds % of portfolio), `MaxSectorExposureRule` (rejects when asset class exposure exceeds %, uses `IReadOnlyDictionary<Asset, AssetClassCode>` mapping). `RiskManager` evaluates all rules; first rejection short-circuits.
- **`RiskEvaluation`** — Sealed record value object with `IsAllowed` and `RejectionReason`. Static factories: `RiskEvaluation.Allowed`, `RiskEvaluation.Rejected(reason)`.
- **DI registration** — `ServiceCollectionExtensions.AddBoutquinTrading(IServiceCollection, IConfiguration)` registers all services. Construction model, cost model, slippage model, and risk manager are factory-based from `IOptions<T>`. Switch expressions on config strings select implementations (e.g., `"RiskParity"` → `RiskParityConstruction`).
- **`IOptions<T>` configuration** — Three options classes: `BacktestOptions` (dates, currency, rebalancing frequency, construction model choice), `CostModelOptions` (transaction cost type, commission rate, slippage type/amount), `RiskManagementOptions` (max drawdown %, max position size %, max sector exposure %). Each has `SectionName` constant for `IConfiguration.GetSection()`.
- **Structured logging** — `ILogger<T>` added to `Portfolio`, `BackTest`, `ConstructionModelStrategy` via backward-compatible constructor overloads (old constructor chains to new via `this(...)`). Logs computed weights, rebalance decisions, backtest start/end. Default `NullLogger<T>.Instance` ensures no exceptions when logger not provided.
- **`CancellationToken` on all async APIs** — Every async interface method (`IBrokerage`, `IPortfolio`, `IEventProcessor`, `IEventHandler`, `IMarketDataFetcher`, `IMarketDataStorage`, `ICurrencyConversionService`, `IMarketDataProcessor`, `ISymbolReader`) accepts `CancellationToken cancellationToken = default`. Implementations call `ThrowIfCancellationRequested()` and forward token to inner calls. `IAsyncEnumerable` methods use `[EnumeratorCancellation]`.
- **New packages** — `Application.csproj`: `Microsoft.Extensions.DependencyInjection` 10.0.5, `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.5.

## Codebase Map

### Project Structure

| Project | Purpose | Key Dependencies |
|---------|---------|-----------------|
| `Boutquin.Trading.Domain` | Core domain: interfaces, events, value objects, enums, extensions | Boutquin.Domain 0.7.0, EF Core Relational 10.0.5, Logging.Abstractions 10.0.5 |
| `Boutquin.Trading.Application` | Backtest engine, portfolio, strategies, event handlers, brokers, DI registration | Domain, System.Linq.Async 7.0.0, M.E.DependencyInjection 10.0.5, M.E.Options.ConfigurationExtensions 10.0.5 |
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
| Analytics domain records (Phase 3) | `Domain/Analytics/` |
| BrinsonFachlerAttributor | `Application/Analytics/BrinsonFachlerAttributor.cs` |
| FactorRegressor | `Application/Analytics/FactorRegressor.cs` |
| CorrelationAnalyzer | `Application/Analytics/CorrelationAnalyzer.cs` |
| DrawdownAnalyzer | `Application/Analytics/DrawdownAnalyzer.cs` |
| HtmlReportGenerator | `Application/Reporting/HtmlReportGenerator.cs` |
| BenchmarkComparisonReport | `Application/Reporting/BenchmarkComparisonReport.cs` |
| IIndicator interface | `Domain/Interfaces/IIndicator.cs` |
| IMacroIndicator interface | `Domain/Interfaces/IMacroIndicator.cs` |
| IRegimeClassifier interface | `Domain/Interfaces/IRegimeClassifier.cs` |
| IUniverseSelector interface | `Domain/Interfaces/IUniverseSelector.cs` |
| Core indicators (SMA, EMA, RealizedVol, Momentum) | `Application/Indicators/` |
| Macro indicators (Spread, RateOfChange) | `Application/Indicators/` |
| GrowthInflationRegimeClassifier | `Application/Regime/GrowthInflationRegimeClassifier.cs` |
| TacticalOverlayConstruction | `Application/PortfolioConstruction/TacticalOverlayConstruction.cs` |
| VolatilityTargetingConstruction | `Application/PortfolioConstruction/VolatilityTargetingConstruction.cs` |
| WalkForwardOptimizer | `Application/Analytics/WalkForwardOptimizer.cs` |
| MonteCarloSimulator | `Application/Analytics/MonteCarloSimulator.cs` |
| Universe filters (MinAum, MinAge, Liquidity, Composite) | `Application/Universe/` |
| Analytics domain records (7) | `Domain/Analytics/` |
| IRiskManager interface | `Domain/Interfaces/IRiskManager.cs` |
| IRiskRule interface | `Domain/Interfaces/IRiskRule.cs` |
| RiskEvaluation value object | `Domain/ValueObjects/RiskEvaluation.cs` |
| Risk rules (MaxDrawdown, MaxPositionSize, MaxSectorExposure) | `Application/RiskManagement/` |
| RiskManager (composite) | `Application/RiskManagement/RiskManager.cs` |
| DI registration (ServiceCollectionExtensions) | `Application/Configuration/ServiceCollectionExtensions.cs` |
| BacktestOptions, CostModelOptions, RiskManagementOptions | `Application/Configuration/` |

### Domain Interfaces (23)

`IBrokerage`, `ICapitalAllocationStrategy`, `ICovarianceEstimator`, `ICurrencyConversionService`, `IEventHandler`, `IEventProcessor`, `IFinancialEvent`, `IIndicator`, `IMacroIndicator`, `IMarketDataFetcher`, `IMarketDataProcessor`, `IMarketDataStorage`, `IOrderPriceCalculationStrategy`, `IPortfolio`, `IPortfolioConstructionModel`, `IPositionSizer`, `IRebalancingTrigger`, `IRegimeClassifier`, `IRiskManager`, `IRiskRule`, `IStrategy`, `ISymbolReader`, `IUniverseSelector`

### Domain Enums (13)

`AssetClassCode`, `ContinentCode`, `CountryCode`, `CurrencyCode`, `EconomicRegime`, `ExchangeCode`, `OrderType`, `RebalancingFrequency`, `SecuritySymbolStandard`, `SignalType`, `TimeZoneCode`, `TradeAction`

### Test Patterns

- **Framework:** xUnit + FluentAssertions + Moq
- **Precision:** `private const decimal Precision = 1e-12m;` for financial metric comparisons
- **Data pattern:** `[Theory]` + `[MemberData(nameof(TestDataClass.Prop), MemberType = typeof(TestDataClass))]`
- **Data classes:** Separate `*TestData.cs` files with `public static IEnumerable<object[]>` properties using collection initializer `[...]`
- **Naming:** `MethodName_Scenario_ExpectedBehavior` or `MethodName_ShouldReturnCorrectResult`
- **Namespace:** `Boutquin.Trading.Tests.UnitTests.{Layer}` (Domain, Application, Data, DataAccess)
- **All test classes are `public sealed`**
- **CalculationException:** Always use fully qualified `Boutquin.Trading.Domain.Exceptions.CalculationException` in test assertions (not in GlobalUsings)
