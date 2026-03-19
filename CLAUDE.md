# Boutquin.Trading

> **Language conventions:** `~/.claude/conventions/dotnet-conventions.md`

Quantitative trading framework in C# .NET. Pre-release.

## Remotes

- `origin` → `boutquin/Boutquin.Trading.Dev` (private, full history)
- `public` → `boutquin/Boutquin.Trading` (private, release repo — not yet public)

## Financial Calculation Conventions

- **Sample divisor (N-1) for all deviation/variance calculations** — `DownsideDeviation`, `StandardDeviation`, covariance in `Beta` all use sample-based divisor (`Length - 1`), not population (`Length`). This is the standard for financial time series where we're estimating from a sample.
- **Degenerate-input guards throw `CalculationException`** — When a computation would produce `NaN`, `Infinity`, or meaningless results due to degenerate inputs, throw `Boutquin.Trading.Domain.Exceptions.CalculationException`. This covers: zero denominators in ratio calculations (Sharpe, Sortino, Beta, InformationRatio), non-positive bases in `Math.Pow` (e.g., `AnnualizedReturn` with cumulative return ≤ -100%), and any other mathematically undefined scenario. Degenerate inputs should be surfaced, not silently propagated.
- **All return/ratio metrics use raw decimals, not percentages** — `AnnualizedReturn`, `CompoundAnnualGrowthRate`, `MaxDrawdown`, and all other financial metrics must return raw decimal ratios (e.g., `0.125` for 12.5%). Never multiply by 100 inside a computation method. Derived metrics like `CalmarRatio` (`CAGR / |MaxDrawdown|`) depend on consistent units; mixing raw and percentage produces ~100x errors.
- **DownsideDeviation returns 0 for no downside risk** — When all returns are above the risk-free rate, `DownsideDeviation` is 0 (no downside risk exists). This is a valid result, not a degenerate input. The zero-denominator guard belongs in `SortinoRatio` (which divides by downside deviation), not in `DownsideDeviation` itself.
- **MonteCarloSimulator Sharpe ratio must be annualized** — Daily Sharpe (`mean / stdDev`) is ~15.9x smaller than the conventional annualized Sharpe (`mean / stdDev * sqrt(252)`). The simulator must accept a `tradingDaysPerYear` parameter and annualize.
- **Equity curve helpers must throw on zero equity** — `MonthlyReturns` and `AnnualReturns` in `EquityCurveExtensions` must throw `CalculationException` when the previous-period equity is zero (total loss), not silently return 0% (which implies "no change").
- **Array length matching in paired calculations** — `Beta` and other methods that take paired arrays (portfolio vs benchmark) must guard `portfolioDailyReturns.Length != benchmarkDailyReturns.Length` with `ArgumentException`. Silent truncation via `Zip` hides data misalignment bugs.
- **`CalculationException` namespace** — Use fully qualified `Boutquin.Trading.Domain.Exceptions.CalculationException` in test assertions to avoid CS0104 ambiguity with `Boutquin.Domain.Exceptions.ExceptionMessages`. Do not add `global using Boutquin.Trading.Domain.Exceptions` to `GlobalUsings.cs`.

## Event Pipeline Architecture

- **FillEvent includes `TradeAction`** — The `FillEvent` record carries `TradeAction` (Buy/Sell) so that `FillEventHandler` can correctly branch: Buy deducts `(price * qty + commission)`, Sell credits `(price * qty - commission)`. Position updates must also branch: Buy → `UpdatePositions(asset, quantity)`, Sell → `UpdatePositions(asset, -quantity)`. Since `Quantity` is always positive (`Math.Abs` from `SignalEventHandler`), the sell path must negate it.
- **MarketEventHandler feeds signals to event processor** — `GenerateSignals` return value must be captured and each signal fed into `portfolio.HandleEventAsync(signal)`. Discarding the return value silently drops all trading signals.
- **SimulatedBrokerage filters by order timestamp** — `FetchMarketDataAsync` results must be filtered to match `order.Timestamp`. Without this, backtests use wrong-date market data (e.g., latest close instead of the order's historical date).
- **Stop and StopLimit orders use High/Low for trigger evaluation** — `SimulatedBrokerage.HandleStopOrder` checks `marketData.High >= stopPrice` (buy) and `marketData.Low <= stopPrice` (sell), not Close. This correctly models intraday stop triggering. StopLimit orders must also use High/Low for the stop-trigger portion; only the limit-fill check uses Close.
- **Thread-safe delegate invocation in SimulatedBrokerage** — All `FillOccurred` invocations use the copy-to-local pattern (`var handler = FillOccurred; if (handler != null)`) to prevent race conditions.
- **OrderEventHandler logs instead of throwing on failed orders** — `OrderEventHandler.HandleEventAsync` logs `LogWarning` when `SubmitOrderAsync` returns false, since order rejection is a normal backtest condition. Uses backward-compatible constructor with `NullLogger<T>.Instance` default.
- **StrategyBase defensive copies cash** — Constructor creates `new SortedDictionary<CurrencyCode, decimal>(cash)` to prevent external mutation.
- **Position sizing uses `Math.Round(MidpointRounding.AwayFromZero)`** — Both `FixedWeightPositionSizer` and `DynamicWeightPositionSizer` round share quantities instead of truncating with `(int)` cast, avoiding systematic downward bias.
- **Portfolio.AdjustPositionForSplit uses `Math.Round`** — Reverse splits (e.g., 1:3) that produce fractional positions are rounded via `MidpointRounding.AwayFromZero` instead of truncated.
- **Backtest fetches FX rates for benchmark assets** — `RunAsync` includes `_benchmarkPortfolio.Strategies.Values` in the currency pair collection for FX rate fetching. Previously only portfolio currencies were fetched.
- **Position.Sell does not subtract fee from BookValue** — Transaction fees are realized costs, not reductions of remaining cost basis. `Sell` computes `soldBookValue = BookValue * (shares / originalQuantity)` and sets `BookValue -= soldBookValue`. The fee is not tracked in `Position` (follow-up: add `TotalFees` property).
- **SecurityPrice.Volume is `long`** — Matches domain `MarketData.Volume` type. High-volume securities can exceed `int.MaxValue` (2.1B). EF Core maps `long` to `bigint` automatically.
- **SecurityPrice price scale must be ≥ 4** — `ColumnConstants.SecurityPrice_Price_Scale` of 2 silently truncates precision for penny stocks, crypto, FX pairs, and adjusted close prices. Use scale 4+ for price columns in EF Core configuration.
- **TieredCostModel sorts tiers on construction** — Constructor sorts input tiers by `MaxTradeValue` ascending, ensuring correct tier matching regardless of input order.

## Data Fetcher Architecture

- **Composite pattern for `IMarketDataFetcher`** — `CompositeMarketDataFetcher` delegates `FetchMarketDataAsync` → `TiingoFetcher` or `TwelveDataFetcher` (equities) and `FetchFxRatesAsync` → `FrankfurterFetcher` (FX rates). Single-responsibility fetchers that each throw `NotSupportedException` for the method they don't handle. Consumers use the composite.
- **TwelveDataFetcher merges three endpoints** — Combines `/time_series` (OHLCV), `/dividends`, and `/splits` per symbol to produce full `MarketData` records. Dividend/split fetch failures are non-fatal (returns empty); time series failure throws `MarketDataRetrievalException`. API key is passed as query parameter, not header.
- **FrankfurterFetcher supports date range filtering** — Constructor accepts optional `DateOnly? startDate` and `DateOnly? endDate` parameters. Defaults to `1999-01-04..` (full history) for backward compatibility. Currency codes and base currency are URL-encoded via `Uri.EscapeDataString` for defense-in-depth.
- **`IEconomicDataFetcher`** — New interface for scalar economic time series (treasury yields, macro indicators). Returns `IAsyncEnumerable<KeyValuePair<DateOnly, decimal>>` for a given series ID. Not part of `IMarketDataFetcher` because FRED data is scalar, not OHLCV/FX.
- **`FredFetcher`** — Fetches from FRED REST API. API key required (free). Returns raw values as FRED provides them (e.g., yields in percent, not decimal). Caller transforms. Missing values (`"."`) are silently skipped.
- **`FredSeriesConstants`** — Well-known FRED series IDs for treasury yields, inflation, and growth indicators.
- **`IFactorDataFetcher`** — New interface for multi-factor return series (Fama-French, etc.). Returns `IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>` — all factors from a dataset in one async stream. Not part of `IMarketDataFetcher` because factor returns are not OHLCV/FX data.
- **`FamaFrenchFetcher`** — Downloads ZIP/CSV from Ken French Data Library. No API key required. Supports 3-factor, 5-factor, and momentum datasets in both daily and monthly frequencies. Values in percentage (caller transforms). Missing values (-99.99/-999) silently skipped. Monthly annual summary section excluded.
- **`FamaFrenchDataset` enum** — Constrains valid dataset identifiers: `ThreeFactors`, `FiveFactors`, `Momentum`.
- **`FamaFrenchConstants`** — Well-known factor names matching CSV headers: `Mkt-RF`, `SMB`, `HML`, `RMW`, `CMA`, `RF`, `Mom`.
- **Deprecating a data provider project** — Checklist: (1) delete project directory, (2) `dotnet sln remove`, (3) remove `ProjectReference` entries from all consuming `.csproj` files, (4) update `GlobalUsings.cs` and source code to use replacement, (5) add new `ProjectReference` entries for replacement projects, (6) grep for old namespace to catch stragglers.

## Portfolio Construction Architecture (Phase 2)

- **`IPortfolioConstructionModel`** — Core interface: `ComputeTargetWeights(assets, returns) → Dictionary<Asset, decimal>`. All models guarantee: weights ≥ 0, weights sum to 1.0, empty assets → empty weights.
- **Six base construction models** in `Application/PortfolioConstruction/`: `EqualWeightConstruction`, `InverseVolatilityConstruction`, `RiskParityConstruction`, `MeanVarianceConstruction`, `MinimumVarianceConstruction`, `BlackLittermanConstruction`. The folder name `PortfolioConstruction` matches the roadmap naming convention.
- **`ICovarianceEstimator`** — Three implementations: `SampleCovarianceEstimator` (N-1 divisor), `ExponentiallyWeightedCovarianceEstimator` (EWMA with configurable lambda), `LedoitWolfShrinkageEstimator` (shrinkage toward scaled identity). All use `SampleCovarianceEstimator.ValidateReturns()` for input validation.
- **LedoitWolfShrinkageEstimator must include rho correction** — The standard Ledoit-Wolf (2004) shrinkage intensity formula requires three terms: `pi` (sum of asymptotic variances), `rho` (sum of asymptotic covariances with target), and `gamma` (`||S - F||_F^2`). Omitting the `rho` term biases shrinkage intensity upward (over-shrinks toward identity).
- **`RollingWindow<T>`** — Generic circular buffer in `Domain/Helpers/`. Fixed capacity, drops oldest on add, chronological iteration. Used for windowed return series.
- **`IRebalancingTrigger`** — Two implementations: `CalendarRebalancingTrigger` (always true, calendar logic in strategy), `ThresholdRebalancingTrigger` (fires when any asset drifts beyond band).
- **`ConstructionModelStrategy`** — Wires `IPortfolioConstructionModel` + `IRebalancingTrigger` + `RebalancingFrequency` into the strategy pipeline. Extracts rolling returns from `historicalMarketData`, computes target weights dynamically at each rebalance. Stores `LastComputedWeights` for `DynamicWeightPositionSizer` to read.
- **`ConstructionModelStrategy.ComputeCurrentWeights` must throw on missing FX rate** — When computing current weights for multi-currency portfolios, a missing FX rate must throw `InvalidOperationException`, not silently use the unconverted asset value. Consistent with `StrategyBase.ComputeTotalValue` behavior. The caller `GenerateSignals` wraps this in a try-catch (M9 resilience) that logs the failure and falls back to empty weights, triggering a full rebalance. Both layers are intentional: the computation never returns silently wrong values, and the caller has an explicit recovery path. Do not remove the M9 try-catch.
- **`DynamicWeightPositionSizer`** — Reads `LastComputedWeights` from `ConstructionModelStrategy` to compute position sizes. Falls back to equal weight if no computed weights available.
- **Optimization approach** — `MeanVarianceConstruction` and `MinimumVarianceConstruction` use projected gradient descent with line search and simplex projection. `RiskParityConstruction` uses iterative inverse-MRC algorithm.
- **Line search acceptance must require strict improvement** — MeanVariance: `newUtility > oldUtility`. MinimumVariance: `newVar < oldVar`. Conditions like `newVar <= oldVar + tolerance` accept worsening steps on every iteration, causing divergence.
- **RiskParity is undefined for negative MRC** — Negative marginal risk contribution is valid for hedging assets but risk parity (inverse-MRC weighting) cannot produce meaningful weights. Throw `CalculationException` when any MRC is ≤ 0, rather than silently clamping to tolerance.
- **BlackLitterman no-views case returns equilibrium weights directly** — When no views are provided, return `_equilibriumWeights` directly rather than round-tripping through matrix inversion. The round-trip fails for singular covariance matrices and introduces numerical error.

## Analytics & Attribution Architecture (Phase 3)

- **`BrinsonFachlerAttributor`** — Static class implementing Brinson-Fachler single-period performance attribution. Decomposes active return into allocation effect `(Wp-Wb)(Rb_sector-Rb_total)`, selection effect `Wb(Rp_sector-Rb_sector)`, and interaction effect `(Wp-Wb)(Rp_sector-Rb_sector)`. Returns per-asset and total effects. Effects sum to total active return.
- **`FactorRegressor`** — Multi-factor OLS regression via normal equations with Gaussian elimination + partial pivoting. Regresses portfolio returns against Fama-French (or custom) factors. Returns alpha, per-factor betas, R², and residual standard error.
- **`CorrelationAnalyzer`** — Computes full N×N correlation matrix from return series (sample covariance, N-1 divisor). Computes diversification ratio = weighted avg vol / portfolio vol. Also provides rolling pairwise correlation time series.
- **`DrawdownAnalyzer`** — Identifies discrete drawdown periods from an equity curve: tracks peak → trough → recovery transitions. Each `DrawdownPeriod` record includes start date, trough date, recovery date (nullable if ongoing), depth, duration days, and recovery days.
- **`HtmlReportGenerator`** — Generates self-contained HTML tearsheet with embedded SVG charts (equity curve, drawdown area), metrics table, and monthly returns heatmap. No external JS dependencies.
- **`BenchmarkComparisonReport`** — Generates side-by-side HTML comparison of portfolio vs benchmark. Includes dual equity curve SVG (normalized to 100), metrics comparison table, and annualized tracking error calculation.
- **Benchmark comparison must align dates** — When portfolio and benchmark equity curves have different date ranges, tracking error must be computed on date-aligned inner-join data, not silently set to 0. Dual SVG charts must share a date-based x-axis (`(date - minDate) / (maxDate - minDate)`) rather than independent index-based scaling, to avoid visually misleading comparisons.
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
- **DI registration** — `ServiceCollectionExtensions.AddBoutquinTrading(IServiceCollection, IConfiguration)` registers all services. Construction model, cost model, slippage model, and risk manager are factory-based from `IOptions<T>`. All switch expressions use explicit cases with `_ => throw new ArgumentOutOfRangeException(...)` — no silent defaults. Valid construction models: `EqualWeight`, `InverseVolatility`, `MinimumVariance`, `MeanVariance`, `RiskParity`, `BlackLitterman`. Slippage models with non-zero `SlippageAmount` required for `FixedSlippage`/`PercentageSlippage`.
- **BlackLitterman must not be registered with empty equilibrium weights** — The DI factory should throw `InvalidOperationException` rather than creating a `BlackLittermanConstruction(equilibriumWeights: [])` instance that will produce nonsensical results at runtime. Require manual configuration outside DI.
- **All `RiskManagementOptions` properties must be wired** — `MaxSectorExposurePercent` must create a `MaxSectorExposureRule` in the DI factory, same as `MaxDrawdownPercent` and `MaxPositionSizePercent`. Configuration options that exist but aren't wired are silent traps.
- **`IOptions<T>` configuration** — Four options classes: `BacktestOptions` (dates, currency, rebalancing frequency, construction model choice), `CostModelOptions` (transaction cost type, commission rate, slippage type/amount), `RiskManagementOptions` (max drawdown %, max position size %, max sector exposure %), `CacheOptions` (data directory for L2, enable memory cache for L1). Each has `SectionName` constant for `IConfiguration.GetSection()`.
- **Structured logging** — `ILogger<T>` added to `Portfolio`, `BackTest`, `ConstructionModelStrategy` via backward-compatible constructor overloads (old constructor chains to new via `this(...)`). Logs computed weights, rebalance decisions, backtest start/end. Default `NullLogger<T>.Instance` ensures no exceptions when logger not provided.
- **`CancellationToken` on all async APIs** — Every async interface method (`IBrokerage`, `IPortfolio`, `IEventProcessor`, `IEventHandler`, `IMarketDataFetcher`, `IMarketDataStorage`, `ICurrencyConversionService`, `IMarketDataProcessor`, `ISymbolReader`) accepts `CancellationToken cancellationToken = default`. Implementations call `ThrowIfCancellationRequested()` and forward token to inner calls. `IAsyncEnumerable` methods use `[EnumeratorCancellation]`.
- **New packages** — `Application.csproj`: `Microsoft.Extensions.DependencyInjection` 10.0.5, `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.5.

## Caching Architecture

- **Decorator pattern for transparent caching** — L1/L2 caches implement the same interface as the inner fetcher (`IMarketDataFetcher`, `IEconomicDataFetcher`, `IFactorDataFetcher`). Composable: L1 wraps L2 wraps base fetcher. No consumer code changes required.
- **L1 memory cache** — `CachingMarketDataFetcher`, `CachingEconomicDataFetcher`, `CachingFactorDataFetcher` use `ConcurrentDictionary<string, Lazy<Task<List<...>>>>` for thread-safe exactly-once materialization. Cache key: sorted+joined tickers for market data (`"AAPL|MSFT"`), `"{seriesId}|{startDate}|{endDate}"` for economic data (with `*` for null dates), `"{dataset}|{startDate}|{endDate}"` for factor data. Superset filtering: if full range (`*|*`) is cached, subset requests filter in-memory instead of re-fetching.
- **L2 CSV write-through** — `WriteThroughMarketDataFetcher`, `WriteThroughEconomicDataFetcher`, `WriteThroughFactorDataFetcher`. On L2 miss: fetch from API, write to CSV (atomic: tmp + rename), return data. On L2 hit: read from CSV. Per-symbol existence check for market data (partial cache — only missing symbols hit API).
- **CSV storage classes** — `CsvEconomicDataFetcher`/`CsvEconomicDataStorage` (format: `fred_{seriesId}.csv` with `Date,Value`), `CsvFactorDataFetcher`/`CsvFactorDataStorage` (format: `ff_{dataset}_{frequency}.csv` with header-driven factor names). Located in `src/Domain/Data/`.
- **Backtest prefetch** — `BackTest.RunAsync` materializes market data into `SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>` before the event loop. `SimulatedBrokerage.SetBufferedMarketData` enables O(1) dictionary lookups. Default interface method on `IBrokerage` (no-op) keeps contract backward-compatible.
- **`CacheOptions`** — `DataDirectory` (string?, null = L2 disabled), `EnableMemoryCache` (bool, default true). Section name: `"Cache"`. Bound via `IOptions<CacheOptions>`.
- **DI wiring** — `AddBoutquinTradingCaching(IConfiguration)` finds pre-registered base fetchers, removes them, re-registers with decorator chain based on `CacheOptions`. If no base fetcher registered, decorator is skipped. Called automatically from `AddBoutquinTrading`. Can also be called standalone.
- **Application depends on Data.CSV** — `Application.csproj` has `<ProjectReference>` to `Data.CSV` for L2 write-through (uses `CsvMarketDataFetcher`, `CsvMarketDataStorage`, `MarketDataFileNameHelper`). Alias `CsvFileNameHelper = Boutquin.Trading.Data.CSV.MarketDataFileNameHelper` resolves namespace ambiguity with `Domain.Helpers.MarketDataFileNameHelper`.

## Codebase Map

### Project Structure

| Project | Path | Key Dependencies |
|---------|------|-----------------|
| `Boutquin.Trading.Domain` | `src/Domain/` | Boutquin.Domain 0.7.0, EF Core Relational 10.0.5, Logging.Abstractions 10.0.5 |
| `Boutquin.Trading.Application` | `src/Application/` | Domain, Data.CSV, System.Linq.Async 7.0.0, M.E.DependencyInjection 10.0.5, M.E.Options.ConfigurationExtensions 10.0.5 |
| `Boutquin.Trading.Data.Tiingo` | `src/Data.Tiingo/` | Domain |
| `Boutquin.Trading.Data.Frankfurter` | `src/Data.Frankfurter/` | Domain |
| `Boutquin.Trading.Data.Fred` | `src/Data.Fred/` | Domain |
| `Boutquin.Trading.Data.FamaFrench` | `src/Data.FamaFrench/` | Domain |
| `Boutquin.Trading.Data.TwelveData` | `src/Data.TwelveData/` | Domain |
| `Boutquin.Trading.Data.CSV` | `src/Data.CSV/` | Domain |
| `Boutquin.Trading.Data.Processor` | `src/Data.Processor/` | Domain, Application |
| `Boutquin.Trading.DataAccess` | `src/DataAccess/` | Domain |
| `Boutquin.Trading.BackTest` | `src/BackTest/` | Application |
| `Boutquin.Trading.Sample` | `src/Sample/` | Application |
| `Boutquin.Trading.BenchMark` | `benchmarks/BenchMark/` | Domain |
| `Boutquin.Trading.Tests.UnitTests` | `tests/UnitTests/` | xUnit 2.9.3, FluentAssertions 8.8.0, Moq 4.20.70 |
| `Boutquin.Trading.Tests.ArchitectureTests` | `tests/ArchitectureTests/` | Domain, Application, NetArchTest |

### Key File Locations

| What | Path |
|------|------|
| Financial metrics (extension methods on `decimal[]`) | `src/Domain/Extensions/DecimalArrayExtensions.cs` |
| Equity curve drawdown analysis | `src/Domain/Extensions/EquityCurveExtensions.cs` |
| Tearsheet record (performance summary) | `src/Domain/Helpers/TearSheet.cs` |
| IStrategy interface | `src/Domain/Interfaces/IStrategy.cs` |
| IPortfolio interface (14 methods) | `src/Domain/Interfaces/IPortfolio.cs` |
| IPositionSizer interface | `src/Domain/Interfaces/IPositionSizer.cs` |
| IBrokerage interface | `src/Domain/Interfaces/IBrokerage.cs` |
| ICapitalAllocationStrategy (no impls yet) | `src/Domain/Interfaces/ICapitalAllocationStrategy.cs` |
| MarketData record | `src/Domain/Data/MarketData.cs` |
| Event records (Market/Signal/Order/Fill) | `src/Domain/Events/` |
| CalculationException | `src/Domain/Exceptions/CalculationException.cs` |
| Portfolio implementation | `src/Application/Portfolio.cs` |
| Backtest engine | `src/Application/Backtest.cs` |
| SimulatedBrokerage | `src/Application/Brokers/SimulatedBrokerage.cs` |
| BuyAndHoldStrategy | `src/Application/Strategies/BuyAndHoldStrategy.cs` |
| RebalancingBuyAndHoldStrategy | `src/Application/Strategies/RebalancingBuyAndHoldStrategy.cs` |
| FixedWeightPositionSizer | `src/Application/PositionSizing/FixedWeightPositionSizer.cs` |
| Event handlers | `src/Application/EventHandlers/` |
| CompositeMarketDataFetcher | `src/Application/CompositeMarketDataFetcher.cs` |
| IEconomicDataFetcher interface | `src/Domain/Interfaces/IEconomicDataFetcher.cs` |
| FredFetcher | `src/Data.Fred/FredFetcher.cs` |
| FRED series constants | `src/Data.Fred/FredSeriesConstants.cs` |
| IFactorDataFetcher interface | `src/Domain/Interfaces/IFactorDataFetcher.cs` |
| FamaFrenchDataset enum | `src/Domain/Enums/FamaFrenchDataset.cs` |
| FamaFrenchFetcher | `src/Data.FamaFrench/FamaFrenchFetcher.cs` |
| Fama-French factor constants | `src/Domain/Helpers/FamaFrenchConstants.cs` |
| RollingWindow\<T\> (circular buffer) | `src/Domain/Helpers/RollingWindow.cs` |
| ICovarianceEstimator interface | `src/Domain/Interfaces/ICovarianceEstimator.cs` |
| IPortfolioConstructionModel interface | `src/Domain/Interfaces/IPortfolioConstructionModel.cs` |
| ILeveragedConstructionModel interface | `src/Domain/Interfaces/ILeveragedConstructionModel.cs` |
| IRebalancingTrigger interface | `src/Domain/Interfaces/IRebalancingTrigger.cs` |
| Covariance estimators (Sample, EWMA, Ledoit-Wolf) | `src/Application/CovarianceEstimators/` |
| Portfolio construction models (6 models) | `src/Application/PortfolioConstruction/` |
| Rebalancing triggers (Calendar, Threshold) | `src/Application/Rebalancing/` |
| ConstructionModelStrategy | `src/Application/Strategies/ConstructionModelStrategy.cs` |
| DynamicWeightPositionSizer | `src/Application/PositionSizing/DynamicWeightPositionSizer.cs` |
| Analytics domain records (Phase 3) | `src/Domain/Analytics/` |
| BrinsonFachlerAttributor | `src/Application/Analytics/BrinsonFachlerAttributor.cs` |
| FactorRegressor | `src/Application/Analytics/FactorRegressor.cs` |
| CorrelationAnalyzer | `src/Application/Analytics/CorrelationAnalyzer.cs` |
| DrawdownAnalyzer | `src/Application/Analytics/DrawdownAnalyzer.cs` |
| HtmlReportGenerator | `src/Application/Reporting/HtmlReportGenerator.cs` |
| BenchmarkComparisonReport | `src/Application/Reporting/BenchmarkComparisonReport.cs` |
| IIndicator interface | `src/Domain/Interfaces/IIndicator.cs` |
| IMacroIndicator interface | `src/Domain/Interfaces/IMacroIndicator.cs` |
| IRegimeClassifier interface | `src/Domain/Interfaces/IRegimeClassifier.cs` |
| IUniverseSelector interface | `src/Domain/Interfaces/IUniverseSelector.cs` |
| Core indicators (SMA, EMA, RealizedVol, Momentum) | `src/Application/Indicators/` |
| Macro indicators (Spread, RateOfChange) | `src/Application/Indicators/` |
| GrowthInflationRegimeClassifier | `src/Application/Regime/GrowthInflationRegimeClassifier.cs` |
| TacticalOverlayConstruction | `src/Application/PortfolioConstruction/TacticalOverlayConstruction.cs` |
| VolatilityTargetingConstruction | `src/Application/PortfolioConstruction/VolatilityTargetingConstruction.cs` |
| WalkForwardOptimizer | `src/Application/Analytics/WalkForwardOptimizer.cs` |
| MonteCarloSimulator | `src/Application/Analytics/MonteCarloSimulator.cs` |
| Universe filters (MinAum, MinAge, Liquidity, Composite) | `src/Application/Universe/` |
| Analytics domain records (7) | `src/Domain/Analytics/` |
| IRiskManager interface | `src/Domain/Interfaces/IRiskManager.cs` |
| IRiskRule interface | `src/Domain/Interfaces/IRiskRule.cs` |
| RiskEvaluation value object | `src/Domain/ValueObjects/RiskEvaluation.cs` |
| Risk rules (MaxDrawdown, MaxPositionSize, MaxSectorExposure) | `src/Application/RiskManagement/` |
| RiskManager (composite) | `src/Application/RiskManagement/RiskManager.cs` |
| L1 memory cache decorators (3) | `src/Application/Caching/CachingMarketDataFetcher.cs`, `CachingEconomicDataFetcher.cs`, `CachingFactorDataFetcher.cs` |
| L2 write-through decorators (3) | `src/Application/Caching/WriteThroughMarketDataFetcher.cs`, `WriteThroughEconomicDataFetcher.cs`, `WriteThroughFactorDataFetcher.cs` |
| CSV economic data fetcher/storage | `src/Domain/Data/CsvEconomicDataFetcher.cs`, `CsvEconomicDataStorage.cs` |
| CSV factor data fetcher/storage | `src/Domain/Data/CsvFactorDataFetcher.cs`, `CsvFactorDataStorage.cs` |
| DI registration (ServiceCollectionExtensions) | `src/Application/Configuration/ServiceCollectionExtensions.cs` |
| BacktestOptions, CostModelOptions, RiskManagementOptions, CacheOptions | `src/Application/Configuration/` |

### Domain Interfaces (26)

`IBrokerage`, `ICapitalAllocationStrategy`, `ICovarianceEstimator`, `ICurrencyConversionService`, `IEconomicDataFetcher`, `IEventHandler`, `IEventProcessor`, `IFactorDataFetcher`, `IFinancialEvent`, `IIndicator`, `ILeveragedConstructionModel`, `IMacroIndicator`, `IMarketDataFetcher`, `IMarketDataProcessor`, `IMarketDataStorage`, `IOrderPriceCalculationStrategy`, `IPortfolio`, `IPortfolioConstructionModel`, `IPositionSizer`, `IRebalancingTrigger`, `IRegimeClassifier`, `IRiskManager`, `IRiskRule`, `IStrategy`, `ISymbolReader`, `IUniverseSelector`

### Domain Enums (14)

`AssetClassCode`, `ContinentCode`, `CountryCode`, `CurrencyCode`, `EconomicRegime`, `ExchangeCode`, `FamaFrenchDataset`, `OrderType`, `RebalancingFrequency`, `SecuritySymbolStandard`, `SignalType`, `TimeZoneCode`, `TradeAction`

### Test Patterns

- **Framework:** xUnit + FluentAssertions + Moq
- **Precision:** `private const decimal Precision = 1e-12m;` for financial metric comparisons
- **Data pattern:** `[Theory]` + `[MemberData(nameof(TestDataClass.Prop), MemberType = typeof(TestDataClass))]`
- **Data classes:** Separate `*TestData.cs` files with `public static IEnumerable<object[]>` properties using collection initializer `[...]`
- **Naming:** `MethodName_Scenario_ExpectedBehavior` or `MethodName_ShouldReturnCorrectResult`
- **Namespace:** `Boutquin.Trading.Tests.UnitTests.{Layer}` (Domain, Application, Data, DataAccess)
- **All test classes are `public sealed`**
- **CalculationException:** Always use fully qualified `Boutquin.Trading.Domain.Exceptions.CalculationException` in test assertions (not in GlobalUsings)
- **Required test coverage:** The following production classes currently lack tests and must be tested before release: `CsvMarketDataFetcher`, `CsvMarketDataStorage`, `MarketDataProcessor`, `CsvSymbolReader`, `EventProcessor`, `SignalEventHandler`, `ClosePriceOrderPriceCalculationStrategy`, `SpreadSlippage`. All data fetcher tests must include cancellation token and null/empty input validation tests.
- **`MockHttpMessageHandler` must support per-URL responses** — The test mock should accept a URL → response map (or response queue) instead of a single fixed response. Single-response mocks mask per-symbol URL construction bugs when testing multi-symbol fetchers.
