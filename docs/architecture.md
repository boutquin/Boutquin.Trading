# Boutquin.Trading Solution Architecture

The Boutquin.Trading solution is a quantitative trading framework organized into a layered architecture with clear separation of concerns. Data ingestion is fully delegated to the `Boutquin.MarketData` ecosystem.

## Directory Layout

```
Boutquin.Trading/
├── src/                        # Source projects (6)
│   ├── Domain/                 # Core domain layer — contracts, events, records, enums
│   ├── Application/            # Implementations — engine, models, analytics, risk, DI
│   ├── Recipes/                # MarketData kernel bridge — BacktestDatasetBuilder
│   ├── DataAccess/             # EF Core persistence (SecurityMaster)
│   ├── BackTest/               # Backtest runner entry point
│   └── Sample/                 # Usage examples
├── tests/                      # Test projects
│   ├── UnitTests/              # xUnit + FluentAssertions + Moq (119+ files)
│   ├── ArchitectureTests/      # NetArchTest fitness functions
│   └── Verification/           # Cross-language Python suite (14+ generators, 84 vectors)
├── benchmarks/                 # Performance benchmarks
│   └── BenchMark/              # BenchmarkDotNet suite
├── docs/                       # Documentation
│   └── examples/               # Worked examples
├── specs/                      # Internal specifications (gitignored in public release)
├── hooks/                      # Git hooks
└── Resources/                  # Shared assets (icon)
```

## Layer Dependencies

```
Domain  ←  Application   ←  BackTest / Sample
Domain  ←  Recipes        ←  Application
Domain  ←  DataAccess

External NuGet dependencies (not project references):
Domain:      Boutquin.Domain, Boutquin.MarketData.Abstractions, Boutquin.Numerics
Application: Boutquin.MarketData.Abstractions, Boutquin.MarketData.Calendars, Boutquin.Numerics
Recipes:     Boutquin.MarketData.Abstractions, Boutquin.MarketData.Orchestration
```

No project depends upward. Domain has zero project references.

Data fetchers (Tiingo, Frankfurter, FRED, Fama-French, Bank of Canada, etc.) are in the separate `Boutquin.MarketData.Adapter` repository and registered as adapters in the `IDataPipeline`.

## Boutquin.Trading.Domain (`src/Domain/`)

Core business logic, contracts, and value types. No implementation dependencies.

- **Interfaces/** — 39 domain interfaces defining all contracts (`IBrokerage`, `IPortfolio`, `IStrategy`, `IPositionSizer`, `ICovarianceEstimator`, `IPortfolioConstructionModel`, `IRebalancingTrigger`, `IRegimeClassifier`, `IRiskManager`, `IRiskRule`, `IDrawdownControl`, `IIndicator`, `IMacroIndicator`, `IUniverseSelector`, `ITimedUniverseSelector`, `ITradingCalendar`, and more)
- **Events/** — Event records driving the pipeline: `MarketEvent`, `SignalEvent`, `OrderEvent`, `FillEvent`
- **Enums/** — 9 backtest-specific enums: `OrderType`, `TradeAction`, `SignalType`, `EconomicRegime`, `RebalancingFrequency`, `AccountType`, `HoldingPeriod`, `CanadianProvince`, `UsState` (market data enums such as `CurrencyCode` and `AssetClassCode` live in `Boutquin.MarketData.Abstractions.ReferenceData`)
- **Extensions/** — `DecimalArrayExtensions` (28+ financial metrics), `EquityCurveExtensions` (drawdown tracking, monthly/annual returns)
- **Analytics/** — Sealed record types for analytics results: `BrinsonFachlerResult`, `FactorRegressionResult`, `CorrelationAnalysisResult`, `DrawdownPeriod`, `WalkForwardResult`, `MonteCarloResult`, `CurrencyReturnDecomposition`
- **TaxEngine/** — 9 tax domain records: `TaxLot`, `LotDisposal`, `TaxImpact`, `DividendRecord`, `DividendTaxImpact`, `LossHarvestingResult`, `TradeRecord`, `PositionSnapshot`, `RocAdjustmentResult`
- **Helpers/** — `TearSheet` (21 performance metrics), `RebalanceOrder`
- **ValueObjects/** — `RiskEvaluation`, `BatchRiskEvaluation`, `AssetWeightConstraints`
- **Exceptions/** — `CalculationException` for degenerate-input guards

## Boutquin.Trading.Application (`src/Application/`)

All implementations of domain interfaces, the backtest engine, and DI wiring. Depends on Domain, Recipes, Boutquin.Numerics, and Boutquin.MarketData.Abstractions.

- **Portfolio.cs** — Core portfolio: multi-currency cash management, position tracking, equity curve, DRIP
- **Backtest.cs** — Event-driven backtest runner with data quality gate and expense ratio deduction
- **Brokers/** — `SimulatedBrokerage` (market, limit, stop, stop-limit orders; slippage and commission models; next-bar Open fill; quantity-limiting)
- **Strategies/** — `BuyAndHoldStrategy`, `RebalancingBuyAndHoldStrategy`, `ConstructionModelStrategy`
- **PortfolioConstruction/** — 19 base models + 2 decorators (see [Portfolio Construction Guide](portfolio-construction-guide.md))
- **CovarianceEstimators/** — 13 estimators: Sample, EWMA, LedoitWolfShrinkage, LedoitWolfConstantCorrelation, LedoitWolfSingleFactor, OAS, QIS, Denoised (RMT), TracyWidomDenoised, Detoned, POET, NERCOME, DoublySparse
- **DownsideRisk/** — `CVaRRiskMeasure`, `DownsideDeviationRiskMeasure`, `CDaRRiskMeasure`
- **Analytics/** — BrinsonFachlerAttributor, FactorRegressor, CorrelationAnalyzer, DrawdownAnalyzer, WalkForwardOptimizer, MonteCarloSimulator, EffectiveNumberOfBets, PrincipalPortfolioAnalyzer, PcaRegimeSignal
- **Indicators/** — SMA, EMA, RealizedVolatility, MomentumScore, SpreadIndicator, RateOfChangeIndicator
- **Regime/** — `GrowthInflationRegimeClassifier` (4-quadrant with deadband hysteresis)
- **Universe/** — MinAum, MinAge, Liquidity, Supersession filters; Composite and Dynamic selectors; `ITimedUniverseSelector`
- **Rebalancing/** — `CalendarRebalancingTrigger`, `ThresholdRebalancingTrigger`
- **PositionSizing/** — `FixedWeightPositionSizer`, `DynamicWeightPositionSizer`
- **RiskManagement/** — `RiskManager` (composite), MaxDrawdown/MaxPositionSize/MaxSectorExposure rules, `DrawdownCircuitBreaker`
- **Reporting/** — `HtmlReportGenerator` (SVG tearsheet), `BenchmarkComparisonReport`
- **EventHandlers/** — Market, Signal, Order, Fill, RebalancingSignal handlers
- **CostModels/** — Fixed, PerShare, Percentage, Tiered, Composite transaction cost models
- **SlippageModels/** — Fixed, No, Percentage, Spread, VolumeShare slippage models
- **Configuration/** — `ServiceCollectionExtensions` (`AddBoutquinTrading`), `BacktestOptions`, `CostModelOptions`, `RiskManagementOptions`, `CacheOptions`, `CalendarOptions`

> **Numerics delegation** — Covariance estimators, analytics classes (`FactorRegressor`, `CorrelationAnalyzer`, `MonteCarloSimulator`), and financial metric extensions delegate all matrix math, statistics, and distribution functions to `Boutquin.Numerics`. No duplicated math in Application.

## Boutquin.Trading.Recipes (`src/Recipes/`)

MarketData kernel integration. Provides the bridge between the backtest engine and the `IDataPipeline`.

| Type | Purpose |
|------|---------|
| `IBacktestDataset` | Immutable read-only dataset interface consumed by `BackTest.RunAsync` |
| `BacktestDataset` | Implementation carrying prices, FX rates, dividends, corporate actions, economic/factor series |
| `BacktestDatasetBuilder` | Materializes a `BacktestDatasetSpec` via `IDataPipeline` into `IBacktestDataset` |
| `BacktestDatasetSpec` | Declarative specification: symbols, date range, base currency, FRED series, factor datasets |
| `FakeBacktestDataset` | Test double for unit testing without live data |

## Boutquin.Trading.DataAccess (`src/DataAccess/`)

EF Core persistence for the SecurityMaster database.

- **Configuration/** — 14 entity type configurations
- **Entities/** — EF Core entity types (Security, SecurityPrice, Exchange, Currency, etc.)
- **Migrations/** — Schema migrations

## Tests (`tests/`)

- **UnitTests/** — xUnit + FluentAssertions + Moq. 119+ test files. Precision: `1e-12m`. Test data in separate `*TestData.cs` files.
- **ArchitectureTests/** — NetArchTest fitness functions enforcing layer dependency rules.
- **Verification/** — 14+ Python generators produce 84 golden JSON vectors; C# xUnit tests validate cross-language agreement to three precision tiers (`1e-10` exact, `1e-6` numeric, `1e-4` statistical).

## Build & CI/CD

- **Target:** .NET 10 / C# 14
- **Versioning:** MinVer (tag prefix: `v`, default pre-release: `beta`)
- **CI:** GitHub Actions (`pr-verify.yml`) — restore, build, test with coverage, format checks, doc quality scans
- **Publish:** GitHub Actions (`publish.yml`) — tag-triggered NuGet push with version verification
- **Deterministic builds:** SourceLink, symbol packages (.snupkg)

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Sample divisor (N-1) | Standard for financial time series estimation from a sample |
| `CalculationException` for degenerate inputs | Zero denominators and undefined scenarios are surfaced, not silently propagated |
| Next-bar Open fills | Eliminates look-ahead bias; matches zipline/QuantConnect/backtrader convention |
| Projected gradient descent for optimization | No external solver dependency (no BLAS/LAPACK/cvxpy required) |
| Numerics delegation | Covariance estimators, analytics, and metrics delegate to `Boutquin.Numerics` — no duplicated math |
| MarketData kernel for data ingestion | Transport, caching, normalization, and provenance are shared infrastructure — not re-implemented per consumer |
| `CancellationToken` on all async APIs | Cooperative cancellation throughout the stack |
| Decorator pattern for caching | Transparent, composable, independently toggleable L1/L2 tiers |
| Tax engine as interfaces only | Extension points for proprietary jurisdiction-specific implementations (commercial `Boutquin.Trading.TaxEngine`) |

## Root Files

| File | Purpose |
|------|---------|
| `.editorconfig` | Code style enforcement |
| `Directory.Build.props` | Centralized MSBuild properties |
| `Directory.Packages.props` | Central package version management |
| `global.json` | .NET SDK version constraint |
| `docs/architecture.md` | This file |
| `CHANGELOG.md` | Version history |
| `CONTRIBUTING.md` | Contribution workflow |
| `LICENSE.txt` | Apache 2.0 license |
| `README.md` | Project overview |
