# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-04-17

### Added
- `Boutquin.Trading.Recipes` project: `IBacktestDataset`, `BacktestDataset`, `BacktestDatasetBuilder`, `BacktestDatasetSpec`, and `FakeBacktestDataset` — provides the bridge between the backtest engine and the `Boutquin.MarketData` `IDataPipeline` for production data workflows.
- Nine new covariance estimators completing the four-tier lineup (13 total):
  - Linear shrinkage: `LedoitWolfConstantCorrelationEstimator` (average-correlation target — recommended default for equities), `LedoitWolfSingleFactorEstimator` (market factor target), `OracleApproximatingShrinkageEstimator` (OAS, Chen et al. 2010)
  - Nonlinear/denoising: `QuadraticInverseShrinkageEstimator` (per-eigenvalue shrinkage, gold standard for N ≥ 10), `TracyWidomDenoisedCovarianceEstimator` (sharper finite-sample threshold, preferred when T/N < 5)
  - Factor/sparse/nonparametric: `PoetCovarianceEstimator` (low-rank + sparse residual), `NercomeCovarianceEstimator` (nonparametric split-sample), `DoublySparseEstimator` (sparsifies eigenvectors + noise eigenvalues), `DetonedCovarianceEstimator` (PC1 market-factor shrinkage, Lopez de Prado 2020)

### Changed
- `CorrelationAnalyzer.Analyze` now delegates covariance computation to `Boutquin.Numerics.Statistics.SampleCovarianceEstimator`, removing the inline two-pass covariance loop (~25 lines). `RollingCorrelation` now delegates entirely to `Boutquin.Numerics.Statistics.PearsonCorrelation.Rolling`, removing ~30 lines of inline rolling-window arithmetic.
- `MonteCarloSimulator.Run` now delegates to `Boutquin.Numerics.MonteCarlo.BootstrapMonteCarloEngine`, removing the inline bootstrap loop, per-simulation mean/std computation, and private `Percentile` helper (~30 lines).
- `SampleCovarianceEstimator`, `ExponentiallyWeightedCovarianceEstimator`, `LedoitWolfShrinkageEstimator`, `DenoisedCovarianceEstimator`, and `DetonedCovarianceEstimator` now delegate to `Boutquin.Numerics.Statistics`, removing ~900 lines of duplicated math.
- `PrincipalPortfolioAnalyzer`, `EffectiveNumberOfBets`, and `PcaRegimeSignal` now use `Boutquin.Numerics.LinearAlgebra.JacobiEigenDecomposition`, removing ~400 lines of inline Jacobi sweeps.
- `FactorRegressor` now uses `Boutquin.Numerics.Solvers.OrdinaryLeastSquares<decimal>` (Householder QR, 28-digit accuracy) instead of hand-rolled Gaussian elimination in `double`.
- `DecimalArrayExtensions.ValueAtRisk` now delegates to `Boutquin.Numerics.Distributions.InverseNormal.Evaluate`, removing the inline 52-line Abramowitz & Stegun rational approximation.
- `MinimumVarianceConstruction` and `MeanVarianceConstruction` now use `Boutquin.Numerics.Solvers.ActiveSetQpSolver` (Cholesky active-set with correct KKT cross-covariance handling) instead of the hand-rolled `CholeskyQpSolver`, which had a bug: it ignored the `Σ_FC * w_C` cross-covariance adjustment when solving the free-variable sub-problem after fixing weights at bounds, causing constrained solutions to be suboptimal.
- `MaximumDiversificationConstruction` likewise migrated to `ActiveSetQpSolver`.
- `DecimalArrayExtensions.Skewness` and `Kurtosis` now delegate to `Boutquin.Numerics.Statistics.SampleSkewness.Compute` and `SampleExcessKurtosis.Compute`, removing ~40 lines of duplicated math.
- `RollingWindow<T>` is now consumed from `Boutquin.Numerics.Collections` instead of the local `Domain/Helpers` copy.
- `Boutquin.Numerics` version bumped to `1.1.0`.

### Fixed
- Collinear factor inputs to `FactorRegressor` now correctly throw `CalculationException` instead of `OverflowException`. The fix is in `Boutquin.Numerics.LinearAlgebra.Internal.HouseholderQr<T>.BuildXtXInverse`, which now converts decimal arithmetic overflow to `InvalidOperationException`.
- `MinimumVarianceConstruction` constrained QP now finds the true global minimum. The old `CholeskyQpSolver` ignored the KKT cross-covariance term `Σ_FC * w_C` when fixed-bound variables were present, causing the free-variable sub-problem to be solved against an incorrect RHS and producing a higher-variance portfolio than necessary. `ActiveSetQpSolver` accounts for the full KKT system and matches scipy SLSQP to float64 precision.

### Removed
- `Asset` value object removed from `Boutquin.Trading.Domain.ValueObjects`; replaced by `Boutquin.MarketData.Abstractions.ReferenceData.Symbol`. Both are `readonly record struct` with a `Ticker` property; `Symbol` is the canonical type at the data-access layer.
- `AssetClassCode`, `ContinentCode`, `CountryCode`, `CurrencyCode`, `DividendType`, `ExchangeCode`, `SecuritySymbolStandard`, and `TimeZoneCode` enums removed from `Boutquin.Trading.Domain.Enums`. All eight are now consumed from `Boutquin.MarketData.Abstractions.ReferenceData`, which is the canonical source. `Boutquin.Trading.Domain.Enums` retains only backtest-specific enums (`TradeAction`, `OrderType`, `SignalType`, `EconomicRegime`, `RebalancingFrequency`, `AccountType`, `HoldingPeriod`, `CanadianProvince`, `UsState`).
- `FamaFrenchDataset` enum removed; the `IDataPipeline` architecture uses `Boutquin.MarketData.Abstractions.ReferenceData.FactorDatasetId` (a string-typed record struct) for all factor dataset identification.
- `MarketDataNotFoundException`, `MarketDataProcessingException`, `MarketDataRetrievalException`, `MarketDataStorageException` removed from `Boutquin.Trading.Domain.Exceptions`; canonical versions live in `Boutquin.MarketData.Abstractions.Exceptions`. Also removed dead-code exceptions `SymbolReaderException`, `NegativeBusinessDaysPerYearException`, and `NegativeRiskFreeRateException` (zero usages).
- `Domain.Data.MarketData` and `Domain.Data.FxRateData` records removed; backtest infrastructure uses `Boutquin.MarketData.Abstractions.Records.Bar` and `FxRate` directly.
- `Boutquin.Trading.Domain.Helpers.CholeskyQpSolver` removed; replaced by `Boutquin.Numerics.Solvers.ActiveSetQpSolver`.
- `Boutquin.Trading.Domain.Helpers.RollingWindow<T>` removed; replaced by `Boutquin.Numerics.Collections.RollingWindow<T>`.

## [1.0.0] - 2026-03-30

First production release of the Boutquin.Trading quantitative trading framework for long-only ETF and equity backtesting.

### Core Engine
- Event-driven backtesting pipeline: MarketEvent → SignalEvent → OrderEvent → FillEvent
- Next-bar Open fills with quantity-limiting (no look-ahead bias)
- Multi-currency cash management and position tracking
- Burn-in period support for indicator warm-up
- Trading calendar integration with configurable composition modes
- Dividend reinvestment (DRIP) — optional whole-share reinvestment at Close price
- Expense ratio deduction — portfolio-level default + per-asset overrides in basis points

### Portfolio Construction (18 Models + 1 Decorator)
- EqualWeight, InverseVolatility, MinimumVariance, MeanVariance, RiskParity
- MaximumDiversification (Chopin & Briand 2008)
- HierarchicalRiskParity (Lopez de Prado 2016), HERC, ReturnTiltedHRP (Lohre et al. 2020)
- BlackLitterman, DynamicBlackLitterman
- MeanDownsideRisk with pluggable CVaR and DownsideDeviation measures
- RobustMeanVariance (minimax across covariance scenarios)
- TacticalOverlay, VolatilityTargeting, WeightConstrained, RegimeWeightConstrained
- TurnoverPenalized decorator with L1 penalty

### Covariance Estimation (4)
- Sample (N-1), EWMA, Ledoit-Wolf Shrinkage (with rho correction), Denoised (RMT)

### Downside Risk Measures (3)
- CVaR (Rockafellar-Uryasev 2000), DownsideDeviation, CDaR

### Analytics & Reporting
- Brinson-Fachler attribution, multi-factor OLS regression, correlation analysis
- Drawdown analysis, walk-forward optimization, Monte Carlo simulation
- Effective Number of Bets (Meucci 2009)
- HTML tearsheet with SVG charts, benchmark comparison report

### Risk Management
- Composite risk manager with MaxDrawdown, MaxPositionSize, MaxSectorExposure rules
- DrawdownCircuitBreaker for dynamic intervention

### Data Providers
- Tiingo, TwelveData (equities), Frankfurter (FX), FRED (economic), Fama-French (factors), CSV

### Infrastructure
- L1 memory cache + L2 CSV write-through (6 decorators)
- Full DI registration with explicit factory switches
- CancellationToken on all async APIs, structured logging
- 39 domain interfaces, 17 enums, tax engine extension points (6 interfaces, 9 domain records)
- 1,456 tests (1,452 unit + 4 architecture), cross-language verification against Python (81 golden vectors)
- .NET 10 / C# 14, TreatWarningsAsErrors, SourceLink, MinVer, Apache 2.0
