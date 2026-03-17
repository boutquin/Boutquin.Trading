# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

#### Domain Layer
- **24 domain interfaces** — `IBrokerage`, `IPortfolio` (14 methods), `IStrategy`, `IPositionSizer`, `ICovarianceEstimator`, `IPortfolioConstructionModel`, `IRebalancingTrigger`, `IRegimeClassifier`, `IRiskManager`, `IRiskRule`, `IIndicator`, `IMacroIndicator`, `IUniverseSelector`, and more.
- **13 domain enums** — `AssetClassCode`, `CurrencyCode`, `OrderType`, `TradeAction`, `RebalancingFrequency`, `EconomicRegime`, and others.
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
- **DI registration** — `ServiceCollectionExtensions.AddBoutquinTrading()` with `IOptions<T>` configuration (`BacktestOptions`, `CostModelOptions`, `RiskManagementOptions`).
- **Structured logging** — `ILogger<T>` on `Portfolio`, `BackTest`, `ConstructionModelStrategy` with backward-compatible constructors.
- **`CancellationToken`** — All async APIs accept `CancellationToken cancellationToken = default`.

#### Data Layer
- **Composite data fetcher** — `CompositeMarketDataFetcher` delegating to `TiingoFetcher` (equities) and `FrankfurterFetcher` (FX rates).
- **FrankfurterFetcher** — ECB-sourced FX rates with optional date range filtering.
- **CSV reader** — `CsvSymbolReader` for symbol list ingestion.
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
