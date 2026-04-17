# Repository Map

Quick navigation guide to the Boutquin.Trading codebase, organized by layer.

## Domain Layer (`src/Domain/`)

The contract layer. Zero project references — depends only on NuGet packages.

| Directory | Contents |
|-----------|----------|
| `Interfaces/` | 39 domain interfaces defining all contracts |
| `Events/` | `MarketEvent`, `SignalEvent`, `OrderEvent`, `FillEvent` records |
| `Enums/` | 9 backtest-specific enums: `OrderType`, `TradeAction`, `SignalType`, `EconomicRegime`, `RebalancingFrequency`, `AccountType`, `HoldingPeriod`, `CanadianProvince`, `UsState` |
| `Extensions/` | `DecimalArrayExtensions` (28+ financial metrics), `EquityCurveExtensions` (drawdown tracking, monthly/annual returns) |
| `Analytics/` | Result records: `BrinsonFachlerResult`, `FactorRegressionResult`, `CorrelationAnalysisResult`, `DrawdownPeriod`, `WalkForwardResult`, `MonteCarloResult`, `CurrencyReturnDecomposition` |
| `TaxEngine/` | Tax records: `TaxLot`, `LotDisposal`, `TaxImpact`, `DividendRecord`, `DividendTaxImpact`, `LossHarvestingResult`, `TradeRecord`, `PositionSnapshot`, `RocAdjustmentResult` |
| `ValueObjects/` | `RiskEvaluation`, `BatchRiskEvaluation`, `AssetWeightConstraints` |
| `Helpers/` | `TearSheet` (21 metrics), `RebalanceOrder` |
| `Exceptions/` | `CalculationException` |

> Market data types (`Symbol`, `CurrencyCode`, `AssetClassCode`, etc.) are consumed from `Boutquin.MarketData.Abstractions.ReferenceData` — they are not duplicated in the Domain layer.

## Application Layer (`src/Application/`)

All implementations. Depends on Domain, Boutquin.MarketData.Abstractions, and Boutquin.Numerics.

| Directory | Contents |
|-----------|----------|
| `Portfolio.cs` | Core portfolio: multi-currency cash, positions, equity curve, DRIP |
| `Backtest.cs` | Event-driven backtest runner with data quality gate and expense ratio |
| `Brokers/` | `SimulatedBrokerage` — market, limit, stop, stop-limit orders |
| `Strategies/` | `BuyAndHoldStrategy`, `RebalancingBuyAndHoldStrategy`, `ConstructionModelStrategy` |
| `PortfolioConstruction/` | 19 base models + 2 decorators (see [guide](portfolio-construction-guide.md)) |
| `CovarianceEstimators/` | 13 estimators: Sample, EWMA, LedoitWolfShrinkage, LedoitWolfConstantCorrelation, LedoitWolfSingleFactor, OAS, QIS, Denoised (RMT), TracyWidomDenoised, Detoned, POET, NERCOME, DoublySparse |
| `DownsideRisk/` | `CVaRRiskMeasure`, `DownsideDeviationRiskMeasure`, `CDaRRiskMeasure` |
| `Analytics/` | BrinsonFachler, FactorRegressor, CorrelationAnalyzer, DrawdownAnalyzer, WalkForwardOptimizer, MonteCarloSimulator, EffectiveNumberOfBets, PrincipalPortfolioAnalyzer, PcaRegimeSignal |
| `Indicators/` | SMA, EMA, RealizedVolatility, MomentumScore, SpreadIndicator, RateOfChangeIndicator |
| `Regime/` | `GrowthInflationRegimeClassifier` (4-quadrant with deadband hysteresis) |
| `Universe/` | MinAum, MinAge, Liquidity, Supersession filters; Composite and Dynamic selectors |
| `Rebalancing/` | `CalendarRebalancingTrigger`, `ThresholdRebalancingTrigger` |
| `PositionSizing/` | `FixedWeightPositionSizer`, `DynamicWeightPositionSizer` |
| `RiskManagement/` | `RiskManager` (composite), MaxDrawdown/MaxPositionSize/MaxSectorExposure rules, `DrawdownCircuitBreaker` |
| `CostModels/` | Fixed, PerShare, Percentage, Tiered, Composite transaction cost models |
| `SlippageModels/` | Fixed, No, Percentage, Spread, VolumeShare slippage models |
| `Reporting/` | `HtmlReportGenerator` (SVG tearsheet), `BenchmarkComparisonReport` |
| `EventHandlers/` | Market, Signal, Order, Fill, RebalancingSignal handlers |
| `Configuration/` | `ServiceCollectionExtensions`, `BacktestOptions`, `CostModelOptions`, `RiskManagementOptions`, `CacheOptions`, `CalendarOptions` |

> Covariance estimators delegate all matrix math to `Boutquin.Numerics.Statistics`. Analytics classes (`FactorRegressor`, `CorrelationAnalyzer`, `MonteCarloSimulator`, etc.) delegate to `Boutquin.Numerics.LinearAlgebra`, `Boutquin.Numerics.MonteCarlo`, and `Boutquin.Numerics.Distributions`.

## Persistence (`src/DataAccess/`)

EF Core SecurityMaster database with 14 entity types: AssetClass, City, Continent, Country, Currency, Exchange, ExchangeHoliday, ExchangeSchedule, FxRate, Position, Security, SecurityPrice, SecuritySymbol, SymbolStandard, TimeZone.

## Recipes (`src/Recipes/`)

MarketData kernel integration for production data workflows.

| Type | Purpose |
|------|---------|
| `IBacktestDataset` | Immutable read-only dataset interface |
| `BacktestDataset` | Implementation carrying prices, FX, dividends, corporate actions, economic/factor series |
| `BacktestDatasetBuilder` | Builder for materializing datasets via `IDataPipeline` |
| `BacktestDatasetSpec` | Declarative specification (symbols, dates, currencies, FRED series, factor datasets) |
| `FakeBacktestDataset` | Test double |

## Tests (`tests/`)

| Project | Framework | Coverage |
|---------|-----------|----------|
| `UnitTests/` | xUnit + FluentAssertions + Moq | 160+ classes, 1,530+ tests |
| `ArchitectureTests/` | NetArchTest | Layer dependency enforcement |
| `Verification/` | Python pytest + C# xUnit | 84 golden JSON vectors, 14 Python generators |

## Entry Points

| Project | Purpose |
|---------|---------|
| `BackTest/` | Console app: runs backtests from configuration |
| `Sample/` | Console app: usage examples and demonstrations |

## Related Repositories

| Repository | Relationship |
|------------|-------------|
| `Boutquin.MarketData` | Shared data ingestion kernel (transport, caching, storage, normalization, provenance). Trading's `Recipes` project depends on `IDataPipeline`. Canonical reference-data types (`Symbol`, `CurrencyCode`, etc.) live here. |
| `Boutquin.Numerics` | Numerical methods library. Trading's covariance estimators, analytics, and financial metric extensions delegate all matrix math, statistics, and distribution functions to this package. |
| `Boutquin.Curves` | Curve construction library. Independent from Trading — both consume MarketData but do not depend on each other. |
| `Boutquin.Domain` | DDD building blocks (Entity, Result, Guard, strongly typed IDs). NuGet dependency. |
