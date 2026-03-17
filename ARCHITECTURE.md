# Boutquin.Trading Solution Architecture

The Boutquin.Trading solution is a quantitative trading framework organized into a layered architecture with clear separation of concerns.

## Directory Layout

```
Boutquin.Trading/
├── src/                        # Source projects
│   ├── Domain/                 # Core domain layer
│   ├── Application/            # Application logic and implementations
│   ├── DataAccess/             # EF Core persistence
│   ├── Data.Tiingo/            # Tiingo equity data provider
│   ├── Data.Frankfurter/       # Frankfurter FX rate provider
│   ├── Data.Fred/              # FRED economic data provider
│   ├── Data.FamaFrench/        # Fama-French factor data provider
│   ├── Data.CSV/               # CSV data reader
│   ├── Data.Processor/         # Data processing pipeline
│   ├── BackTest/               # Backtest runner entry point
│   └── Sample/                 # Usage examples
├── tests/                      # Test projects
│   ├── UnitTests/              # xUnit + FluentAssertions + Moq
│   └── ArchitectureTests/      # NetArchTest fitness functions
├── benchmarks/                 # Performance benchmarks
│   └── BenchMark/              # BenchmarkDotNet suite
├── docs/                       # Documentation
├── specs/                      # Specifications
├── hooks/                      # Git hooks
└── Resources/                  # Shared assets (icon)
```

## Layer Dependencies

```
Domain  ←  Application  ←  BackTest / Sample
Domain  ←  DataAccess
Domain  ←  Data.Tiingo / Data.Frankfurter / Data.Fred / Data.FamaFrench / Data.CSV
Application ← Data.Processor (also depends on Data.Tiingo, Data.Frankfurter, Domain)
```

No project depends upward. Domain has zero project references (only NuGet: Boutquin.Domain, EF Core Relational, Logging.Abstractions).

## Boutquin.Trading.Domain (`src/Domain/`)

Core business logic, contracts, and value types. No implementation dependencies.

- **Interfaces/** — 26 domain interfaces defining all contracts (`IBrokerage`, `IPortfolio`, `IStrategy`, `IPositionSizer`, `ICovarianceEstimator`, `IPortfolioConstructionModel`, `IRebalancingTrigger`, `IRegimeClassifier`, `IRiskManager`, `IRiskRule`, `IIndicator`, `IMacroIndicator`, `IUniverseSelector`, `IEconomicDataFetcher`, `IFactorDataFetcher`, etc.)
- **Events/** — Event records driving the pipeline: `MarketEvent`, `SignalEvent`, `OrderEvent`, `FillEvent`
- **Enums/** — 14 domain enums: `AssetClassCode`, `CurrencyCode`, `OrderType`, `TradeAction`, `RebalancingFrequency`, `EconomicRegime`, `FamaFrenchDataset`, etc.
- **Extensions/** — `DecimalArrayExtensions` (financial metrics), `EquityCurveExtensions` (drawdown tracking)
- **Analytics/** — 7 sealed record types for analytics results
- **Data/** — `MarketData` record and security price types
- **Helpers/** — `RollingWindow<T>` (circular buffer), `TearSheet` (performance summary), `FamaFrenchConstants` (well-known factor names)
- **ValueObjects/** — `RiskEvaluation` (allowed/rejected with reason)
- **Exceptions/** — `CalculationException` for degenerate-input guards

## Boutquin.Trading.Application (`src/Application/`)

All implementations of domain interfaces, the backtest engine, and DI wiring.

- **Portfolio.cs** — Core portfolio: cash management, position tracking, equity curve
- **Backtest.cs** — Event-driven backtest runner
- **Brokers/** — `SimulatedBrokerage` (market, limit, stop orders with slippage/commission)
- **Strategies/** — `BuyAndHoldStrategy`, `RebalancingBuyAndHoldStrategy`, `ConstructionModelStrategy`
- **PortfolioConstruction/** — 8 models: EqualWeight, InverseVolatility, MinimumVariance, MeanVariance, RiskParity, BlackLitterman, TacticalOverlay, VolatilityTargeting
- **CovarianceEstimators/** — Sample, EWMA, Ledoit-Wolf shrinkage
- **PositionSizing/** — FixedWeight, DynamicWeight, FixedDollar
- **Indicators/** — SMA, EMA, RealizedVolatility, MomentumScore, SpreadIndicator, RateOfChangeIndicator
- **Regime/** — `GrowthInflationRegimeClassifier`
- **Universe/** — MinAum, MinAge, Liquidity filters, CompositeUniverseSelector
- **Analytics/** — BrinsonFachler, FactorRegressor, CorrelationAnalyzer, DrawdownAnalyzer, WalkForwardOptimizer, MonteCarloSimulator
- **Reporting/** — `HtmlReportGenerator`, `BenchmarkComparisonReport`
- **RiskManagement/** — `RiskManager`, MaxDrawdown/MaxPositionSize/MaxSectorExposure rules
- **EventHandlers/** — Market, Signal, Order, Fill event handlers
- **CostModels/** — Tiered, Percentage transaction cost strategies
- **SlippageModels/** — Fixed, Percentage slippage models
- **Configuration/** — `ServiceCollectionExtensions`, `BacktestOptions`, `CostModelOptions`, `RiskManagementOptions`
- **CompositeMarketDataFetcher.cs** — Routes equity → Tiingo, FX → Frankfurter

## Boutquin.Trading.DataAccess (`src/DataAccess/`)

EF Core persistence for the SecurityMaster database.

- **Configuration/** — 14 entity type configurations
- **Entities/** — EF Core entity types
- **Migrations/** — Initial migration (April 2023)
- **Extensions/** — Helper extension methods

## Data Providers (`src/Data.*/`)

Each provider implements domain interfaces for a specific data source:

| Project | Source | Interface | Method |
|---------|--------|-----------|--------|
| **Data.Tiingo** | Tiingo API | `IMarketDataFetcher` | `FetchMarketDataAsync` |
| **Data.Frankfurter** | Frankfurter (ECB) | `IMarketDataFetcher` | `FetchFxRatesAsync` |
| **Data.Fred** | FRED REST API | `IEconomicDataFetcher` | `FetchSeriesAsync` |
| **Data.FamaFrench** | Ken French Data Library | `IFactorDataFetcher` | `FetchDailyAsync`, `FetchMonthlyAsync` |
| **Data.CSV** | CSV files | `ISymbolReader` | `ReadSymbolsAsync` |
| **Data.Processor** | Pipeline | — | Orchestrates data processing |

## Tests (`tests/`)

- **UnitTests/** — xUnit 2.9.3 + FluentAssertions 8.8.0 + Moq 4.20.70. Mirrors source layer structure. Precision: `1e-12m`. Test data in separate `*TestData.cs` files.
- **ArchitectureTests/** — NetArchTest fitness functions enforcing layer dependency rules.

## Benchmarks (`benchmarks/`)

- **BenchMark/** — BenchmarkDotNet performance benchmarks for domain calculations.

## Build & CI/CD

- **Target:** .NET 10 / C# 14
- **Versioning:** MinVer (tag prefix: `v`, default pre-release: `beta`)
- **CI:** GitHub Actions (`pr-verify.yml`) — restore, build, test, format checks
- **Publish:** GitHub Actions (`publish.yml`) — tag-triggered NuGet push
- **Deterministic builds:** SourceLink, symbol packages (.snupkg)
- **Pre-commit hook:** `dotnet format --verify-no-changes` + Release build

## Key Design Decisions

- **Sample divisor (N-1)** for all deviation/variance/covariance calculations — standard for financial time series estimation.
- **`CalculationException`** for degenerate inputs — zero denominators, non-positive bases, and other mathematically undefined scenarios are surfaced, not silently propagated.
- **Event pipeline with `TradeAction`** — `FillEvent` carries Buy/Sell so handlers branch correctly on cash flow direction.
- **`CancellationToken` on all async APIs** — Every async interface method accepts `CancellationToken cancellationToken = default`.
- **Backward-compatible constructors** — `ILogger<T>` added via constructor overloads that default to `NullLogger<T>.Instance`.
- **Projected gradient descent** for optimization — MeanVariance and MinimumVariance use gradient descent with simplex projection rather than external solver dependencies.

## Root Files

| File | Purpose |
|------|---------|
| `.editorconfig` | Code style enforcement |
| `Directory.Build.props` | Centralized MSBuild properties |
| `global.json` | .NET SDK version constraint |
| `ARCHITECTURE.md` | This file |
| `CHANGELOG.md` | Version history |
| `CODE_OF_CONDUCT.md` | Community guidelines |
| `CONTRIBUTING.md` | Contribution workflow |
| `LICENSE.txt` | Apache 2.0 license |
| `README.md` | Project overview |
