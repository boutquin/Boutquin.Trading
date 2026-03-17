# Boutquin.Trading

[![NuGet](https://img.shields.io/nuget/vpre/Boutquin.Trading.Domain.svg)](https://www.nuget.org/packages/Boutquin.Trading.Domain)
[![License](https://img.shields.io/github/license/boutquin/Boutquin.Trading)](https://github.com/boutquin/Boutquin.Trading/blob/main/LICENSE.txt)
[![Build](https://github.com/boutquin/Boutquin.Trading/actions/workflows/pr-verify.yml/badge.svg)](https://github.com/boutquin/Boutquin.Trading/actions/workflows/pr-verify.yml)

A multi-asset, multi-strategy, event-driven quantitative trading framework for backtesting strategies with portfolio-based risk management and dynamic capital allocation. Built with clean architecture, .NET 10, and strict code quality standards.

## Solution Structure

| Project | NuGet Package | Description |
|---------|---------------|-------------|
| **Boutquin.Trading.Domain** | `Boutquin.Trading.Domain` | Interfaces, events, value objects, enums, extensions, and domain logic |
| **Boutquin.Trading.Application** | `Boutquin.Trading.Application` | Backtest engine, portfolio, strategies, analytics, risk management, DI registration |
| **Boutquin.Trading.DataAccess** | `Boutquin.Trading.DataAccess` | EF Core data access (SecurityMaster) |
| **Boutquin.Trading.Data.Tiingo** | `Boutquin.Trading.Data.Tiingo` | Equity data fetcher (Tiingo API) |
| **Boutquin.Trading.Data.Frankfurter** | `Boutquin.Trading.Data.Frankfurter` | FX rate fetcher (Frankfurter API, ECB-sourced) |
| **Boutquin.Trading.Data.CSV** | `Boutquin.Trading.Data.CSV` | CSV data reader |
| **Boutquin.Trading.Data.Processor** | — | Data processing pipeline |
| **Boutquin.Trading.BackTest** | — | Backtest runner entry point |
| **Boutquin.Trading.Sample** | — | Usage examples and demonstrations |
| **Boutquin.Trading.Tests.UnitTests** | — | Unit tests (xUnit, FluentAssertions, Moq) |
| **Boutquin.Trading.Tests.ArchitectureTests** | — | Architecture fitness functions (NetArchTest) |
| **Boutquin.Trading.BenchMark** | — | Performance benchmarks (BenchmarkDotNet) |

## Features

### Event-Driven Backtesting Engine
- **Event pipeline** — `MarketEvent` → `SignalEvent` → `OrderEvent` → `FillEvent` with pluggable handlers
- **Portfolio** — Multi-currency cash management, position tracking, equity curve computation (14 interface methods)
- **SimulatedBrokerage** — Market, limit, and stop order execution with slippage and commission models
- **Strategies** — `BuyAndHoldStrategy`, `RebalancingBuyAndHoldStrategy`, `ConstructionModelStrategy`

### Portfolio Construction (8 Models)
- **Equal Weight** — Uniform allocation across all assets
- **Inverse Volatility** — Weight inversely proportional to realized volatility
- **Minimum Variance** — Minimize portfolio variance via projected gradient descent
- **Mean-Variance** — Maximize Sharpe ratio via mean-variance optimization
- **Risk Parity** — Equalize risk contribution across assets
- **Black-Litterman** — Bayesian framework combining equilibrium returns with investor views
- **Tactical Overlay** — Regime-specific tilts plus optional momentum scoring
- **Volatility Targeting** — Scale weights to hit a target portfolio volatility

### Financial Metrics
- Sharpe Ratio, Sortino Ratio, Annualized Return, Standard Deviation, Downside Deviation
- Maximum Drawdown, Beta, Information Ratio, Tracking Error
- All calculations use sample divisor (N-1) for financial time series

### Analytics & Attribution
- **Brinson-Fachler Attribution** — Allocation, selection, and interaction effects
- **Factor Regression** — Multi-factor OLS via normal equations (Fama-French compatible)
- **Correlation Analysis** — Full N×N correlation matrix, diversification ratio, rolling pairwise correlation
- **Drawdown Analysis** — Discrete drawdown period identification (peak → trough → recovery)
- **Walk-Forward Optimization** — Rolling in-sample/out-of-sample validation (no look-ahead bias)
- **Monte Carlo Simulation** — Bootstrap resampling with Sharpe ratio distribution

### Tactical & Regime Detection
- **Indicators** — SMA, EMA, Realized Volatility, Momentum Score, Spread, Rate of Change
- **Regime Classifier** — Growth/inflation quadrant detection with configurable deadband hysteresis
- **Universe Filtering** — AUM, inception age, and liquidity filters with composite AND logic

### Risk Management
- **Composite risk manager** — Evaluates all rules; first rejection short-circuits
- **MaxDrawdownRule** — Rejects orders when equity curve drawdown exceeds limit
- **MaxPositionSizeRule** — Rejects when single position exceeds % of portfolio
- **MaxSectorExposureRule** — Rejects when asset class exposure exceeds threshold

### Reporting
- **HTML Tearsheet** — Self-contained HTML with embedded SVG equity curve, drawdown area chart, metrics table, and monthly returns heatmap
- **Benchmark Comparison** — Side-by-side portfolio vs benchmark with dual equity curve and tracking error

### Data Providers
- **Tiingo** — Historical equity/ETF price data
- **Frankfurter** — ECB-sourced FX rates with date range filtering
- **CSV** — Symbol list ingestion
- **Composite fetcher** — Routes equity vs FX requests to the appropriate provider

## Quick Start

### Installation

```sh
dotnet add package Boutquin.Trading.Domain
dotnet add package Boutquin.Trading.Application
```

### Dependency Injection Setup

```csharp
using Boutquin.Trading.Application.Configuration;

services.AddBoutquinTrading(configuration);
```

Configuration via `appsettings.json`:

```json
{
  "Backtest": {
    "StartDate": "2020-01-01",
    "EndDate": "2023-12-31",
    "BaseCurrency": "USD",
    "RebalancingFrequency": "Monthly",
    "ConstructionModel": "RiskParity"
  },
  "CostModel": {
    "CommissionRate": 0.001,
    "SlippageType": "PercentageSlippage",
    "SlippageAmount": 0.0005
  },
  "RiskManagement": {
    "MaxDrawdownPercent": 0.20,
    "MaxPositionSizePercent": 0.10,
    "MaxSectorExposurePercent": 0.40
  }
}
```

## Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                           Domain Layer                                │
│  Interfaces: IPortfolio, IBrokerage, IStrategy, IPositionSizer,       │
│    ICovarianceEstimator, IPortfolioConstructionModel, IRiskManager,    │
│    IIndicator, IMacroIndicator, IRegimeClassifier, IUniverseSelector  │
│  Events: MarketEvent, SignalEvent, OrderEvent, FillEvent              │
│  Enums: AssetClassCode, CurrencyCode, OrderType, TradeAction (13)     │
│  Extensions: DecimalArrayExtensions, EquityCurveExtensions            │
│  Analytics: BrinsonFachlerResult, DrawdownPeriod, MonteCarloResult     │
│  Value Objects: RiskEvaluation                                        │
└───────────────────────────┬──────────────────────────────────────────┘
                            │ depends on
┌───────────────────────────▼──────────────────────────────────────────┐
│                        Application Layer                              │
│  Engine: Portfolio, BackTest, SimulatedBrokerage                      │
│  Strategies: BuyAndHold, RebalancingBuyAndHold, ConstructionModel     │
│  Construction: EqualWeight, InverseVol, MinVar, MeanVar, RiskParity,  │
│    BlackLitterman, TacticalOverlay, VolatilityTargeting               │
│  Analytics: BrinsonFachler, FactorRegressor, CorrelationAnalyzer,     │
│    DrawdownAnalyzer, WalkForwardOptimizer, MonteCarloSimulator        │
│  Risk: RiskManager, MaxDrawdown, MaxPositionSize, MaxSectorExposure   │
│  Indicators: SMA, EMA, RealizedVol, Momentum, Spread, RateOfChange   │
│  Reporting: HtmlReportGenerator, BenchmarkComparisonReport            │
│  DI: ServiceCollectionExtensions, BacktestOptions, CostModelOptions   │
└──────────────────────────────────────────────────────────────────────┘
┌──────────────────────────────────────────────────────────────────────┐
│                          Data Layer                                   │
│  Tiingo (equities), Frankfurter (FX), CSV (symbols)                   │
│  CompositeMarketDataFetcher, DataAccess (EF Core SecurityMaster)      │
└──────────────────────────────────────────────────────────────────────┘
```

The architecture follows the dependency inversion principle — the Domain layer defines contracts, and Application/Data layers provide implementations that can be swapped independently.

For detailed architecture including component navigation and data flow, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Directory Structure

```
Boutquin.Trading/
├── src/                    # Source projects
│   ├── Domain/             # Core domain: interfaces, events, enums, extensions
│   ├── Application/        # Backtest engine, strategies, analytics, DI
│   ├── DataAccess/         # EF Core data access
│   ├── Data.Tiingo/        # Tiingo equity data fetcher
│   ├── Data.Frankfurter/   # Frankfurter FX rate fetcher
│   ├── Data.CSV/           # CSV data reader
│   ├── Data.Processor/     # Data processing pipeline
│   ├── BackTest/           # Backtest runner entry point
│   └── Sample/             # Usage examples
├── tests/                  # Test projects
│   ├── UnitTests/          # xUnit + FluentAssertions + Moq
│   └── ArchitectureTests/  # NetArchTest fitness functions
├── benchmarks/             # Performance benchmarks
│   └── BenchMark/          # BenchmarkDotNet suite
├── docs/                   # Documentation
├── specs/                  # Specifications
├── hooks/                  # Git hooks (pre-commit)
└── Resources/              # Shared assets (icon)
```

## Contributing

Contributions are welcome! Please read the [contributing guidelines](CONTRIBUTING.md) and [code of conduct](CODE_OF_CONDUCT.md) first.

### Reporting Bugs

If you find a bug, please report it by opening an issue on the [Issues](https://github.com/boutquin/Boutquin.Trading/issues) page with:

- A clear and descriptive title
- Steps to reproduce the issue
- Expected and actual behavior
- Screenshots or code snippets, if applicable

### Contributing Code

1. Fork the repository and clone locally
2. Create a feature branch: `git checkout -b feature-name`
3. Install git hooks: `./hooks/install.sh`
4. Make your changes following the [style guides](CONTRIBUTING.md)
5. Commit with clear messages: `git commit -m "Add feature X"`
6. Push and open a pull request

## License

This project is licensed under the Apache 2.0 License — see the [LICENSE](LICENSE.txt) file for details.

## Contact

For inquiries, please open an issue or reach out via [GitHub Discussions](https://github.com/boutquin/Boutquin.Trading/discussions).
