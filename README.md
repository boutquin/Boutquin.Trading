# Boutquin.Trading

[![NuGet](https://img.shields.io/nuget/v/Boutquin.Trading.Domain.svg)](https://www.nuget.org/packages/Boutquin.Trading.Domain)
[![License](https://img.shields.io/github/license/boutquin/Boutquin.Trading)](https://github.com/boutquin/Boutquin.Trading/blob/main/LICENSE.txt)
[![Build](https://github.com/boutquin/Boutquin.Trading/actions/workflows/pr-verify.yml/badge.svg)](https://github.com/boutquin/Boutquin.Trading/actions/workflows/pr-verify.yml)

A production-ready, multi-asset, multi-strategy, event-driven quantitative trading framework for backtesting long-only ETF and equity strategies. Features 21 portfolio construction models, 13 covariance estimators, risk management, performance analytics, and cross-language verification against Python reference implementations. Built with clean architecture, .NET 10, and strict code quality standards.

## Solution Structure

| Project | NuGet Package | Description |
|---------|---------------|-------------|
| **Boutquin.Trading.Domain** | `Boutquin.Trading.Domain` | 39 interfaces, events, value objects, 9 backtest enums, tax engine extension points, and domain logic |
| **Boutquin.Trading.Application** | `Boutquin.Trading.Application` | Backtest engine, portfolio, 21 construction models, 13 covariance estimators, analytics, risk management, DI registration |
| **Boutquin.Trading.Recipes** | `Boutquin.Trading.Recipes` | `IBacktestDataset`, `BacktestDatasetBuilder` — MarketData kernel integration for production data workflows |
| **Boutquin.Trading.DataAccess** | `Boutquin.Trading.DataAccess` | EF Core data access (SecurityMaster) |
| **Boutquin.Trading.BackTest** | — | Backtest runner entry point |
| **Boutquin.Trading.Examples** | — | Usage examples and demonstrations |
| **Boutquin.Trading.Tests.UnitTests** | — | 119+ test files (xUnit, FluentAssertions, Moq) |
| **Boutquin.Trading.Tests.ArchitectureTests** | — | Architecture fitness functions (NetArchTest) |
| **Boutquin.Trading.BenchMark** | — | Performance benchmarks (BenchmarkDotNet) |

> **Data providers** are in the separate [Boutquin.MarketData.Adapter](https://github.com/boutquin/Boutquin.MarketData.Adapter) repository (Tiingo, Frankfurter, FRED, Bank of Canada, Fama-French, and more). The `Boutquin.Trading.Recipes` package connects the backtest engine to the `Boutquin.MarketData` data pipeline.

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
- **Data quality gate** — `RunAsync` logs data provenance and surfaces pipeline issues before the event loop; `"Warning"` or `"Error"` issues abort with `InvalidOperationException` unless `ignoreDataQualityIssues=true`
- **CancellationToken** — All async APIs support cooperative cancellation

### Portfolio Construction (19 Models + 2 Decorators)
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
- **Principal Component Risk Parity** — Equalizes risk across statistical factors (PCs) via inverse-risk (1/√λ) allocation with Marcenko-Pastur signal filtering (Meucci, 2009)
- **Tactical Overlay** — Regime-specific tilts plus optional momentum scoring
- **Volatility Targeting** — Scale weights to hit a target portfolio volatility
- **Weight-Constrained** — Applies min/max weight bounds to any inner model
- **Regime Weight-Constrained** — Regime-dependent weight constraints
- **Turnover-Penalized** (decorator) — L1 turnover penalty wrapping any inner model (stateful)
- **PCA-Constrained** (decorator) — Projects returns to signal PC subspace before delegating to inner model; reduces noise for more stable weights

### Covariance Estimation (13 Estimators)

**Classical**
- **Sample** — Standard sample covariance (N-1 divisor)
- **EWMA** — Exponentially weighted with configurable lambda

**Linear shrinkage**
- **Ledoit-Wolf Shrinkage** — Shrinkage toward scaled identity (Ledoit & Wolf 2004, with rho correction)
- **Ledoit-Wolf Constant Correlation** — Shrinkage toward average correlation target; better default for equity portfolios
- **Ledoit-Wolf Single Factor** — Shrinkage toward market single-factor target
- **Oracle Approximating Shrinkage (OAS)** — Chen et al. (2010); improved finite-sample performance

**Nonlinear / denoising**
- **Quadratic Inverse Shrinkage (QIS)** — Per-eigenvalue shrinkage, gold standard for N ≥ 10 (Ledoit & Wolf 2022)
- **Denoised** — Random Matrix Theory Marcenko-Pastur eigenvalue cleaning (Lopez de Prado 2018), optional Ledoit-Wolf on top
- **Tracy-Widom Denoised** — Sharper finite-sample eigenvalue threshold; preferred when T/N < 5
- **Detoned** — Extends denoising with PC1 (market factor) shrinkage (Lopez de Prado 2020); configurable alpha

**Factor / sparse / nonparametric**
- **POET** — Low-rank + sparse residual; ideal for HRP/PCRP (no PSD guarantee at high threshold)
- **NERCOME** — Nonparametric split-sample; no distributional assumption
- **Doubly Sparse** — Sparsifies eigenvectors and noise eigenvalues

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
- **Principal Portfolio Analysis** — Decomposes portfolio risk into orthogonal investable principal portfolios with risk contributions (Meucci, 2009; Partovi & Caputo, 2004)
- **PCA Regime Signal** — PC1 variance share (systemic risk) and eigenvector stability (regime shift detection) from correlation eigenspectrum (Kritzman et al., 2011)
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

### Data Access

Data ingestion is delegated to the [Boutquin.MarketData](https://github.com/boutquin/Boutquin.MarketData) kernel and [Boutquin.MarketData.Adapter](https://github.com/boutquin/Boutquin.MarketData.Adapter) packages. The `Boutquin.Trading.Recipes` project provides the bridge:

- **`BacktestDatasetSpec`** — declarative specification of symbols, date range, base currency, FRED series, and factor datasets
- **`BacktestDatasetBuilder`** — materializes a spec via `IDataPipeline` into an immutable `IBacktestDataset`
- **`IBacktestDataset`** — read-only dataset consumed by `BackTest.RunAsync`

Available adapters (in `Boutquin.MarketData.Adapter`):

| Adapter | Source | Data |
|---------|--------|------|
| `Tiingo` | Tiingo REST API | Equity/ETF OHLCAV, adjusted close |
| `TwelveData` | Twelve Data API | Equities + dividends + splits |
| `Frankfurter` | Frankfurter / ECB | FX spot rates |
| `FRED` | Federal Reserve | Treasury yields, inflation, GDP, macro |
| `FamaFrench` | Ken French Data Library | Factor returns (3-factor, 5-factor, momentum) |
| `BankOfCanada` | Bank of Canada | CORRA fixings, zero curves |
| `NewYorkFed` | NY Fed | SOFR fixings |

### Cross-Language Verification
- **84 golden JSON test vectors** generated by 14 Python scripts against numpy/scipy/statsmodels/scikit-learn/PyPortfolioOpt
- **12 verification suites** — calculations, backtests, edge cases, covariance estimators, construction models (basic + advanced + remaining + PCA), risk measures, analytics, indicators/regime, integration
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
│  Construction (19): EqualWeight, InverseVol, MinVar, MeanVar, RiskParity ,│
│    MaxDiversification, HRP, HERC, ReturnTiltedHRP, BlackLitterman,        │
│    DynamicBL, MeanDownsideRisk, RobustMeanVar, PrincipalComponentRP,      │
│    TacticalOverlay, VolTargeting, WeightConstrained, RegimeWeightConstr.  │
│  Decorators (2): TurnoverPenalized, PcaConstrained                        │
│  Covariance (13): Sample, EWMA, LedoitWolf×3, OAS, QIS, Denoised,         │
│    TracyWidomDenoised, Detoned, POET, NERCOME, DoublySparse               │
│  Downside Risk (3): CVaR, DownsideDeviation, CDaR                         │
│  Analytics (9): BrinsonFachler, FactorRegressor, CorrelationAnalyzer,     │
│    DrawdownAnalyzer, WalkForward, MonteCarlo, EffectiveNumberOfBets,      │
│    PrincipalPortfolioAnalyzer, PcaRegimeSignal                            │
│  Caching: L1 Memory (3 decorators), L2 CSV (3 write-through decorators)   │
│  Risk: RiskManager, MaxDrawdown, MaxPositionSize, MaxSectorExposure,      │
│    DrawdownCircuitBreaker                                                 │
│  Indicators: SMA, EMA, RealizedVol, Momentum, Spread, RateOfChange        │
│  Universe: MinAum, MinAge, Liquidity, Supersession, Dynamic, Composite    │
│  Reporting: HtmlReportGenerator, BenchmarkComparisonReport                │
│  DI: ServiceCollectionExtensions + 5 options classes                      │
└───────────────────────────────────────────────────────────────────────────┘
┌───────────────────────────────────────────────────────────────────────────┐
│                       Recipes Layer                                       │
│  BacktestDatasetBuilder → IDataPipeline (Boutquin.MarketData kernel)      │
│  IBacktestDataset — immutable read-only dataset fed into BackTest         │
└───────────────────────────────────────────────────────────────────────────┘
┌───────────────────────────────────────────────────────────────────────────┐
│           External: Boutquin.MarketData + Boutquin.MarketData.Adapter     │
│  Transport, caching, normalization, provenance (MarketData kernel)        │
│  Adapters: Tiingo, TwelveData, Frankfurter, FRED, FamaFrench, ...         │
│  DataAccess (EF Core SecurityMaster — local persistence layer)            │
└───────────────────────────────────────────────────────────────────────────┘
```

The architecture follows the dependency inversion principle — the Domain layer defines contracts, and Application and Recipes layers provide implementations that can be swapped independently. Data ingestion is fully delegated to the `Boutquin.MarketData` ecosystem.

For detailed architecture including component navigation and data flow, see [docs/architecture.md](docs/architecture.md).

## Directory Structure

```
Boutquin.Trading/
├── src/                    # Source projects (6)
│   ├── Domain/             # 39 interfaces, events, 9 backtest enums, tax engine records, value objects
│   ├── Application/        # Engine, 21 construction models, 13 estimators, analytics, risk, DI
│   ├── Recipes/            # IBacktestDataset, BacktestDatasetBuilder (MarketData kernel bridge)
│   ├── DataAccess/         # EF Core data access (SecurityMaster)
│   ├── BackTest/           # Backtest runner entry point
│   └── Sample/             # Usage examples
├── tests/
│   ├── UnitTests/          # 119+ test files (xUnit, FluentAssertions, Moq)
│   ├── ArchitectureTests/  # NetArchTest fitness functions
│   └── Verification/       # Cross-language Python suite (14+ generators, 84 vectors)
├── benchmarks/
│   └── BenchMark/          # BenchmarkDotNet suite
├── docs/                   # Documentation
│   └── examples/           # Worked examples (buy-and-hold → attribution)
├── specs/                  # Internal specifications (gitignored in public release)
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
