# Boutquin.Trading

[![NuGet](https://img.shields.io/nuget/v/Boutquin.Trading.Domain.svg)](https://www.nuget.org/packages/Boutquin.Trading.Domain)
[![License](https://img.shields.io/github/license/boutquin/Boutquin.Trading)](https://github.com/boutquin/Boutquin.Trading/blob/main/LICENSE.txt)
[![Build](https://github.com/boutquin/Boutquin.Trading/actions/workflows/pr-verify.yml/badge.svg)](https://github.com/boutquin/Boutquin.Trading/actions/workflows/pr-verify.yml)

A production-ready, multi-asset, multi-strategy, event-driven quantitative trading framework for backtesting long-only ETF and equity strategies. Features 19 portfolio construction models, 4 covariance estimators, risk management, performance analytics, and cross-language verification against Python reference implementations. Built with clean architecture, .NET 10, and strict code quality standards.

## Solution Structure

| Project | NuGet Package | Description |
|---------|---------------|-------------|
| **Boutquin.Trading.Domain** | `Boutquin.Trading.Domain` | 39 interfaces, events, value objects, 17 enums, tax engine extension points, and domain logic |
| **Boutquin.Trading.Application** | `Boutquin.Trading.Application` | Backtest engine, portfolio, 18 construction models, analytics, risk management, caching, DI registration |
| **Boutquin.Trading.DataAccess** | `Boutquin.Trading.DataAccess` | EF Core data access (SecurityMaster) |
| **Boutquin.Trading.Data.Tiingo** | `Boutquin.Trading.Data.Tiingo` | Equity data fetcher (Tiingo API) |
| **Boutquin.Trading.Data.Frankfurter** | `Boutquin.Trading.Data.Frankfurter` | FX rate fetcher (Frankfurter API, ECB-sourced) |
| **Boutquin.Trading.Data.Fred** | `Boutquin.Trading.Data.Fred` | Economic data fetcher (FRED API — treasury yields, inflation, growth) |
| **Boutquin.Trading.Data.FamaFrench** | `Boutquin.Trading.Data.FamaFrench` | Fama-French factor data fetcher (Ken French Data Library) |
| **Boutquin.Trading.Data.TwelveData** | `Boutquin.Trading.Data.TwelveData` | Equity data fetcher (Twelve Data API) |
| **Boutquin.Trading.Data.CSV** | `Boutquin.Trading.Data.CSV` | CSV data reader/writer for market, economic, and factor data |
| **Boutquin.Trading.Data.Processor** | — | Data processing pipeline |
| **Boutquin.Trading.BackTest** | — | Backtest runner entry point |
| **Boutquin.Trading.Sample** | — | Usage examples and demonstrations |
| **Boutquin.Trading.Tests.UnitTests** | — | 150+ test classes, 1,456 tests (xUnit, FluentAssertions, Moq) |
| **Boutquin.Trading.Tests.ArchitectureTests** | — | Architecture fitness functions (NetArchTest) |
| **Boutquin.Trading.BenchMark** | — | Performance benchmarks (BenchmarkDotNet) |

## Features

### Event-Driven Backtesting Engine
- **Event pipeline** — `MarketEvent` → `SignalEvent` → `OrderEvent` → `FillEvent` with pluggable handlers
- **Portfolio** — Multi-currency cash management, position tracking, equity curve computation
- **SimulatedBrokerage** — Market, limit, stop, and stop-limit order execution with slippage and commission models; logs warnings when orders are dropped due to missing market data
- **Strategies** — `BuyAndHoldStrategy`, `RebalancingBuyAndHoldStrategy`, `ConstructionModelStrategy`
- **Next-bar Open fills** — Signals on bar T queue pending orders that fill at bar T+1's Open price (no look-ahead bias)
- **Quantity-limiting** — Buy fills clipped to affordable quantity; zero-quantity fills rejected
- **Dividend reinvestment (DRIP)** — Optional automatic reinvestment of dividends into whole shares at Close price
- **Expense ratio deduction** — Configurable annual expense ratio (basis points) with per-asset overrides, deducted daily from portfolio value
- **CancellationToken** — All async APIs support cooperative cancellation

### Portfolio Construction (18 Models + 1 Decorator)
- **Equal Weight** — Uniform allocation across all assets
- **Inverse Volatility** — Weight inversely proportional to realized volatility
- **Minimum Variance** — Minimize portfolio variance via projected gradient descent
- **Mean-Variance** — Maximize Sharpe ratio via mean-variance optimization
- **Risk Parity** — Equalize risk contribution via iterative inverse-MRC
- **Maximum Diversification** — Maximize diversification ratio (Chopin & Briand, 2008)
- **Hierarchical Risk Parity (HRP)** — Lopez de Prado (2016) clustering-based allocation (never inverts covariance)
- **Hierarchical Equal Risk Contribution (HERC)** — Cluster-based equal risk contribution
- **Return-Tilted HRP** — Lohre, Rother, Schafer (2020) blending inverse-variance with return signal via softmax (active in all market regimes including bear markets)
- **Black-Litterman** — Bayesian framework combining equilibrium returns with investor views; no-views case returns equilibrium weights directly (no matrix inversion)
- **Dynamic Black-Litterman** — Time-varying views with adaptive confidence; omega clamped to prevent singular matrices at confidence=1.0
- **Mean-CVaR** — Downside-risk-aware via `MeanDownsideRiskConstruction` with `CVaRRiskMeasure`
- **Mean-Sortino** — Downside-risk-aware via `MeanDownsideRiskConstruction` with `DownsideDeviationRiskMeasure`
- **Robust Mean-Variance** — Minimax optimization across multiple covariance scenarios (regime-resilient)
- **Tactical Overlay** — Regime-specific tilts plus optional momentum scoring
- **Volatility Targeting** — Scale weights to hit a target portfolio volatility
- **Weight-Constrained** — Applies min/max weight bounds to any inner model
- **Regime Weight-Constrained** — Regime-dependent weight constraints
- **Turnover-Penalized** (decorator) — L1 turnover penalty wrapping any inner model (stateful)

### Covariance Estimation (4 Estimators)
- **Sample** — Standard sample covariance (N-1 divisor)
- **EWMA** — Exponentially weighted with configurable lambda
- **Ledoit-Wolf Shrinkage** — Shrinkage toward scaled identity (2004 formula with rho correction)
- **Denoised** — Random Matrix Theory eigenvalue cleaning (Lopez de Prado 2018), optional Ledoit-Wolf on top

### Downside Risk Measures (3)
- **CVaR** — Conditional Value-at-Risk (Rockafellar-Uryasev 2000 reformulation, configurable alpha, guards against empty scenarios)
- **Downside Deviation** — Semi-deviation below configurable MAR
- **CDaR** — Conditional Drawdown-at-Risk (guards against empty scenarios)

### Financial Metrics
- Sharpe Ratio, Sortino Ratio, Annualized Return, Standard Deviation, Downside Deviation
- Maximum Drawdown, Beta, Information Ratio, Tracking Error, Calmar, Omega, Win Rate, Profit Factor
- Historical VaR, Conditional VaR, Skewness, Kurtosis, Recovery Factor
- All calculations use sample divisor (N-1) for financial time series

### Analytics & Attribution
- **Brinson-Fachler Attribution** — Allocation, selection, and interaction effects
- **Factor Regression** — Multi-factor OLS via normal equations with Gaussian elimination + partial pivoting
- **Correlation Analysis** — Full N×N correlation matrix, diversification ratio, rolling pairwise correlation
- **Effective Number of Bets** — Entropy-based diversification metric from eigenvalue spectrum (Meucci, 2009)
- **Drawdown Analysis** — Discrete drawdown period identification (peak → trough → recovery)
- **Walk-Forward Optimization** — Rolling in-sample/out-of-sample validation (no look-ahead bias)
- **Monte Carlo Simulation** — Bootstrap resampling with Sharpe ratio distribution

### Tactical & Regime Detection
- **Indicators** — SMA, EMA, Realized Volatility, Momentum Score, Spread, Rate of Change
- **Regime Classifier** — Growth/inflation quadrant detection with configurable deadband hysteresis
- **Universe Filtering** — AUM, inception age, liquidity, supersession filters with composite AND logic
- **Dynamic Universe** — Time-varying universe with `ITimedUniverseSelector`
- **Trading Calendar** — Configurable calendar with composition modes

### Risk Management
- **Composite risk manager** — Evaluates all rules; first rejection short-circuits
- **MaxDrawdownRule** — Rejects orders when equity curve drawdown exceeds limit
- **MaxPositionSizeRule** — Rejects when single position exceeds % of portfolio
- **MaxSectorExposureRule** — Rejects when asset class exposure exceeds threshold
- **DrawdownCircuitBreaker** — `IDrawdownControl` for dynamic drawdown-based risk intervention with safe peak initialization

### Reporting
- **HTML Tearsheet** — Self-contained HTML with embedded SVG equity curve, drawdown area chart, metrics table, and monthly returns heatmap
- **Benchmark Comparison** — Side-by-side portfolio vs benchmark with dual equity curve and tracking error

### Caching
- **L1 memory cache** — `ConcurrentDictionary` + `Lazy<Task>` decorators for thread-safe exactly-once materialization; IEnumerable inputs materialized to List before key building; faulted entries auto-evicted on error; caller cancellation checked per-item (cache fetch uses `CancellationToken.None` to prevent stale token capture)
- **L2 CSV write-through** — Transparent disk cache with atomic writes (tmp + rename), per-symbol existence checks, and partial cache support; API fetch failures propagate immediately (no fallthrough to incomplete CSV reads)
- **DI wiring** — `AddBoutquinTradingCaching()` auto-decorates pre-registered fetchers based on `CacheOptions` (L1/L2 independently toggleable)

### Data Providers
- **Tiingo** — Historical equity/ETF price data
- **Twelve Data** — Equity market data combining time series, dividends, and splits
- **Frankfurter** — ECB-sourced FX rates with date range filtering
- **FRED** — Federal Reserve Economic Data (treasury yields, inflation, GDP, macro indicators)
- **Fama-French** — Academic factor return series (3-factor, 5-factor, momentum) from the Ken French Data Library
- **CSV** — Market data, economic data, factor data, and symbol list storage/ingestion
- **Composite fetcher** — Routes equity vs FX requests to the appropriate provider

### Cross-Language Verification
- **81 golden JSON test vectors** generated by 13 Python scripts against numpy/scipy/statsmodels/scikit-learn/PyPortfolioOpt
- **11 verification suites** — calculations, backtests, edge cases, covariance estimators, construction models (basic + advanced + remaining), risk measures, analytics, indicators/regime, integration
- **Three-layer cross-checks** — library cross-references, analytical solutions, property-based invariants
- **Python pytest** validates self-consistency; C# xUnit validates cross-language correctness
- See [tests/Verification/README.md](tests/Verification/README.md) for details

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
  },
  "Cache": {
    "DataDirectory": "./data/cache",
    "EnableMemoryCache": true
  }
}
```

## Architecture

```
┌───────────────────────────────────────────────────────────────────────────┐
│                            Domain Layer (39 interfaces)                   │
│  Core: IPortfolio, IBrokerage, IStrategy, IPositionSizer                  │
│  Construction: IPortfolioConstructionModel, IRobustConstructionModel,     │
│    ILeveragedConstructionModel, ICovarianceEstimator, IDownsideRiskMeasure│
│  Risk: IRiskManager, IRiskRule, IDrawdownControl                          │
│  Tax: ICostBasisMethod, ITaxJurisdiction, IDividendClassifier,            │
│    ILossHarvestingRule, IWithholdingTaxSchedule, ITaxFxRateProvider       │
│  Tactical: IIndicator, IMacroIndicator, IRegimeClassifier                 │
│  Universe: IUniverseSelector, ITimedUniverseSelector                      │
│  Infrastructure: ITradingCalendar, ITransactionCostModel, ISlippageModel  │
│  Events: MarketEvent, SignalEvent, OrderEvent, FillEvent                  │
│  Enums: 17 (AccountType, DividendType, HoldingPeriod + 14 existing)       │
│  Value Objects: RiskEvaluation, BatchRiskEvaluation, Asset, SecurityId    │
│  Tax Records: TaxLot, LotDisposal, TaxImpact, DividendRecord + 5          │
│  Analytics: BrinsonFachlerResult, DrawdownPeriod, MonteCarloResult + 4    │
└───────────────────────────┬───────────────────────────────────────────────┘
                            │ depends on
┌───────────────────────────▼───────────────────────────────────────────────┐
│                        Application Layer                                  │
│  Engine: Portfolio, BackTest, SimulatedBrokerage                          │
│  Strategies: BuyAndHold, RebalancingBuyAndHold, ConstructionModel         │
│  Construction (18): EqualWeight, InverseVol, MinVar, MeanVar, RiskParity ,│
│    MaxDiversification, HRP, HERC, ReturnTiltedHRP, BlackLitterman,        │
│    DynamicBL, MeanDownsideRisk, RobustMeanVar, TacticalOverlay,           │
│    VolTargeting, WeightConstrained, RegimeWeightConstrained               │
│  Decorator: TurnoverPenalized                                             │
│  Covariance (4): Sample, EWMA, LedoitWolf, Denoised                       │
│  Downside Risk (3): CVaR, DownsideDeviation, CDaR                         │
│  Analytics (7): BrinsonFachler, FactorRegressor, CorrelationAnalyzer,     │
│    DrawdownAnalyzer, WalkForward, MonteCarlo, EffectiveNumberOfBets       │
│  Caching: L1 Memory (3 decorators), L2 CSV (3 write-through decorators)   │
│  Risk: RiskManager, MaxDrawdown, MaxPositionSize, MaxSectorExposure,      │
│    DrawdownCircuitBreaker                                                 │
│  Indicators: SMA, EMA, RealizedVol, Momentum, Spread, RateOfChange        │
│  Universe: MinAum, MinAge, Liquidity, Supersession, Dynamic, Composite    │
│  Reporting: HtmlReportGenerator, BenchmarkComparisonReport                │
│  DI: ServiceCollectionExtensions + 5 options classes                      │
└───────────────────────────────────────────────────────────────────────────┘
┌───────────────────────────────────────────────────────────────────────────┐
│                           Data Layer                                      │
│  Tiingo, TwelveData (equities), Frankfurter (FX), FRED (economic),        │
│  FamaFrench (factors), CSV (storage), CompositeMarketDataFetcher          │
│  DataAccess (EF Core SecurityMaster)                                      │
└───────────────────────────────────────────────────────────────────────────┘
```

The architecture follows the dependency inversion principle — the Domain layer defines contracts, and Application/Data layers provide implementations that can be swapped independently.

For detailed architecture including component navigation and data flow, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Directory Structure

```
Boutquin.Trading/
├── src/                    # Source projects (12)
│   ├── Domain/             # 39 interfaces, events, 17 enums, tax engine records, value objects
│   ├── Application/        # Engine, 18 construction models, analytics, risk, caching, DI
│   ├── DataAccess/         # EF Core data access (SecurityMaster)
│   ├── Data.Tiingo/        # Tiingo equity data fetcher
│   ├── Data.TwelveData/    # Twelve Data equity data fetcher
│   ├── Data.Frankfurter/   # Frankfurter FX rate fetcher
│   ├── Data.Fred/          # FRED economic data fetcher
│   ├── Data.FamaFrench/    # Fama-French factor data fetcher
│   ├── Data.CSV/           # CSV data reader/writer
│   ├── Data.Processor/     # Data processing pipeline
│   ├── BackTest/           # Backtest runner entry point
│   └── Sample/             # Usage examples
├── tests/
│   ├── UnitTests/          # 150+ test classes, 1,456 tests (xUnit, FluentAssertions, Moq)
│   ├── ArchitectureTests/  # NetArchTest fitness functions (4 tests)
│   └── Verification/       # Cross-language Python suite (13 generators, 81 vectors)
├── benchmarks/
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

## Tax Engine Extension Points

The Domain layer includes interfaces for jurisdiction-aware tax computation, designed for proprietary or third-party implementations:

| Interface | Purpose |
|-----------|---------|
| `ICostBasisMethod` | Lot-level cost basis tracking (FIFO, ACB, Specific ID) |
| `ITaxJurisdiction` | Jurisdiction-specific gain/loss and dividend tax computation |
| `IDividendClassifier` | Classify dividends by tax treatment (qualified, eligible, foreign, ROC) |
| `ILossHarvestingRule` | Wash sale (US) and superficial loss (Canada) detection |
| `IWithholdingTaxSchedule` | Cross-border withholding tax rates by account type |
| `ITaxFxRateProvider` | Official FX rates for tax purposes (e.g., Bank of Canada noon rate) |

Domain records (`TaxLot`, `LotDisposal`, `TaxImpact`, `DividendRecord`, etc.) are included in the open-source package.

**Boutquin.Trading.TaxEngine** — A separately licensed implementation providing full US and Canadian tax-aware backtesting (cost basis methods, capital gains classification, wash sale/superficial loss rules, multi-account withholding, and tax-loss harvesting optimization) is available under a commercial license. Contact the author for details.

## Disclaimer

Boutquin.Trading is open-source software provided under the Apache 2.0 License. It is a general-purpose research and backtesting tool intended for educational purposes only.

**This software does not constitute financial advice.** All historical performance data represents backtested results computed using actual historical index and ETF return data. Backtested performance is hypothetical and does not represent actual trading. Actual investment results may differ materially. Past performance is not indicative of future results.

The software authors are not registered investment advisers, portfolio managers, or financial planners. Use of this software to make investment decisions is entirely at your own risk. Before making any investment decision, consult with a qualified financial professional who understands your individual circumstances, goals, and risk tolerance.

## License

This project is licensed under the Apache 2.0 License — see the [LICENSE](LICENSE.txt) file for details.

## Contact

For inquiries, please open an issue or reach out via [GitHub Discussions](https://github.com/boutquin/Boutquin.Trading/discussions).
