# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

#### Domain Layer
- **26 domain interfaces** — `IBrokerage`, `IPortfolio` (14 methods), `IStrategy`, `IPositionSizer`, `ICovarianceEstimator`, `IPortfolioConstructionModel`, `IRebalancingTrigger`, `IRegimeClassifier`, `IRiskManager`, `IRiskRule`, `IIndicator`, `IMacroIndicator`, `IUniverseSelector`, `IEconomicDataFetcher`, `IFactorDataFetcher`, and more.
- **14 domain enums** — `AssetClassCode`, `CurrencyCode`, `OrderType`, `TradeAction`, `RebalancingFrequency`, `EconomicRegime`, `FamaFrenchDataset`, and others.
- **`FamaFrenchConstants`** — Well-known factor names (`Mkt-RF`, `SMB`, `HML`, `RMW`, `CMA`, `RF`, `Mom`) for consumer code.
- **Event pipeline** — `MarketEvent`, `SignalEvent`, `OrderEvent`, `FillEvent` records driving the event-driven backtesting engine.
- **Financial metrics** — `DecimalArrayExtensions` with Sharpe ratio, annualized return, standard deviation, downside deviation, Sortino ratio, max drawdown, Beta, and more (all sample-based with N-1 divisor).
- **Equity curve analysis** — `EquityCurveExtensions` for drawdown tracking and `TearSheet` record for performance summaries.
- **`RollingWindow<T>`** — Generic circular buffer for windowed return series.
- **Analytics domain records** — `BrinsonFachlerResult`, `FactorRegressionResult`, `CorrelationAnalysisResult`, `DrawdownPeriod`, `WalkForwardResult`, `MonteCarloResult`, `AssetMetadata` (7 sealed records).

#### Application Layer
- **Backtest engine** — Event-driven backtesting with `Portfolio`, `BackTest`, and `SimulatedBrokerage`.
- **Strategies** — `BuyAndHoldStrategy`, `RebalancingBuyAndHoldStrategy`, `ConstructionModelStrategy`.
- **Portfolio construction** — 8 models: `EqualWeight`, `InverseVolatility`, `MinimumVariance`, `MeanVariance`, `RiskParity`, `BlackLitterman`, `TacticalOverlay`, `VolatilityTargeting`.
- **Covariance estimators** — `SampleCovarianceEstimator`, `ExponentiallyWeightedCovarianceEstimator`, `LedoitWolfShrinkageEstimator`.
- **Position sizing** — `FixedWeightPositionSizer`, `DynamicWeightPositionSizer`, `FixedDollarPositionSizer`.
- **Indicators** — `SimpleMovingAverage`, `ExponentialMovingAverage`, `RealizedVolatility`, `MomentumScore`, `SpreadIndicator`, `RateOfChangeIndicator`.
- **Regime detection** — `GrowthInflationRegimeClassifier` with configurable deadband for hysteresis.
- **Universe filtering** — `MinAumFilter`, `MinAgeFilter`, `LiquidityFilter`, `CompositeUniverseSelector`.
- **Analytics** — `BrinsonFachlerAttributor`, `FactorRegressor`, `CorrelationAnalyzer`, `DrawdownAnalyzer`, `WalkForwardOptimizer`, `MonteCarloSimulator`.
- **Reporting** — `HtmlReportGenerator` (self-contained SVG tearsheet), `BenchmarkComparisonReport` (dual equity curve).
- **Risk management** — `RiskManager` with `MaxDrawdownRule`, `MaxPositionSizeRule`, `MaxSectorExposureRule`.
- **Caching** — Transparent 3-layer caching architecture:
  - **L1 memory cache** — `CachingMarketDataFetcher`, `CachingEconomicDataFetcher`, `CachingFactorDataFetcher` decorators using `ConcurrentDictionary<string, Lazy<Task<List<...>>>>` for thread-safe exactly-once materialization. Superset date filtering for economic/factor data.
  - **L2 CSV write-through** — `WriteThroughMarketDataFetcher`, `WriteThroughEconomicDataFetcher`, `WriteThroughFactorDataFetcher` decorators. Per-symbol CSV existence check (partial cache). Atomic write protocol (write to `.tmp`, rename on success).
  - **Backtest prefetch** — `BackTest.RunAsync` materializes market data into a buffer dictionary. `SimulatedBrokerage.SetBufferedMarketData` enables O(1) dictionary lookups instead of re-streaming `IAsyncEnumerable`. Default interface method on `IBrokerage` (no-op for non-simulated brokerages).
  - **DI wiring** — `AddBoutquinTradingCaching(IConfiguration)` auto-decorates pre-registered fetchers based on `CacheOptions`. L1 and L2 independently toggleable.
- **`CacheOptions`** — `DataDirectory` (null = L2 disabled), `EnableMemoryCache` (default true). Bound from `"Cache"` config section.
- **DI registration** — `ServiceCollectionExtensions.AddBoutquinTrading()` with `IOptions<T>` configuration (`BacktestOptions`, `CostModelOptions`, `RiskManagementOptions`, `CacheOptions`).
- **Structured logging** — `ILogger<T>` on `Portfolio`, `BackTest`, `ConstructionModelStrategy` with backward-compatible constructors.
- **`CancellationToken`** — All async APIs accept `CancellationToken cancellationToken = default`.
- **Risk-free rate** — `BackTest` supports daily risk-free rate parameter for Sharpe/Sortino calculations in tearsheet generation.

#### Data Layer
- **Composite data fetcher** — `CompositeMarketDataFetcher` delegating to `TiingoFetcher` (equities) and `FrankfurterFetcher` (FX rates).
- **FrankfurterFetcher** — ECB-sourced FX rates with optional date range filtering.
- **FredFetcher** — FRED REST API fetcher for economic time series (treasury yields, inflation, GDP). API key required. Returns raw values; caller transforms units. Missing values (`"."`) silently skipped.
- **FredSeriesConstants** — Well-known FRED series IDs for treasury yields, inflation, and growth indicators.
- **FamaFrenchFetcher** — Downloads ZIP/CSV from the Kenneth R. French Data Library. Supports 3-factor, 5-factor, and momentum datasets in daily and monthly frequencies. No API key required. Values in percentage form. Missing values (`-99.99`/`-999`) silently skipped. Monthly annual summary sections excluded.
- **CSV reader/storage** — `CsvSymbolReader` for symbol list ingestion. `CsvMarketDataFetcher`/`CsvMarketDataStorage` for market data. `CsvEconomicDataFetcher`/`CsvEconomicDataStorage` for FRED-style series. `CsvFactorDataFetcher`/`CsvFactorDataStorage` for Fama-French-style factor data. All storage classes use atomic write (tmp + rename).
- **Data processor** — Pipeline for market data processing.

#### Infrastructure
- **EF Core data access** — `SecurityMasterContext` with 14 entity configurations and migrations.
- **Architecture tests** — NetArchTest fitness functions for layer dependency enforcement.
- **CI/CD** — GitHub Actions workflows for PR verification and NuGet publishing.
- **Deterministic builds** — SourceLink, symbol packages, MinVer semantic versioning.

### Changed
- Upgraded to .NET 10 / C# 14 with `TreatWarningsAsErrors` enabled.
- Reorganized solution into `src/`, `tests/`, `benchmarks/`, `docs/`, `specs/` directory structure.
- Fixed 80+ code review findings across domain, application, quant, and data layers.
