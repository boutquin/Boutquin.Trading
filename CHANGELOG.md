# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-03-19

Initial public release of the Boutquin.Trading quantitative trading framework.

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
- **Caching** — Transparent L1 memory cache + L2 CSV write-through decorators for market data, economic data, and factor data fetchers. Backtest prefetch with buffered market data.
- **DI registration** — `ServiceCollectionExtensions.AddBoutquinTrading()` with `IOptions<T>` configuration (`BacktestOptions`, `CostModelOptions`, `RiskManagementOptions`, `CacheOptions`).
- **Structured logging** — `ILogger<T>` on `Portfolio`, `BackTest`, `ConstructionModelStrategy` with backward-compatible constructors.
- **`CancellationToken`** — All async APIs accept `CancellationToken cancellationToken = default`.

#### Data Layer
- **Composite data fetcher** — `CompositeMarketDataFetcher` delegating to provider-specific fetchers.
- **Tiingo** — Historical equity/ETF price data fetcher.
- **Twelve Data** — Equity market data fetcher combining time series, dividends, and splits endpoints.
- **Frankfurter** — ECB-sourced FX rate fetcher with optional date range filtering.
- **FRED** — Federal Reserve Economic Data fetcher for treasury yields, inflation, GDP, macro indicators.
- **Fama-French** — Academic factor return series (3-factor, 5-factor, momentum) from the Ken French Data Library.
- **CSV** — Market data, economic data, factor data, and symbol list storage/ingestion with atomic writes.

#### Infrastructure
- **EF Core data access** — `SecurityMasterContext` with 14 entity configurations.
- **Architecture tests** — NetArchTest fitness functions for layer dependency enforcement.
- **CI/CD** — GitHub Actions workflows for PR verification and NuGet publishing.
- **Deterministic builds** — SourceLink, symbol packages, MinVer semantic versioning.
- .NET 10 / C# 14 with `TreatWarningsAsErrors` enabled.
- 744 tests (740 unit + 4 architecture), 0 failures.
