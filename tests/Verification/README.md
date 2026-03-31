# Cross-Language Verification Suite

Independent Python verification of C# financial calculations, backtest engine, portfolio construction models, risk measures, analytics, indicators, regime detection, and integration scenarios.

## Architecture

```
Python (numpy/scipy/statsmodels/pypfopt)     C# (xUnit)
          |                                       |
          v                                       |
   13 generate_*.py scripts                       |
          |                                       |
          v                                       v
    vectors/*.json  <---- 81 golden vectors ---->  *CrossLanguageTests.cs (9 test classes)
          |                                        CrossLanguageVerificationBase.cs
          v
    pytest (11 test files, self-consistency)
```

**Pattern:** Python generates deterministic JSON vectors using well-known libraries. Python pytest validates self-consistency. C# xUnit loads the same vectors and validates cross-language correctness.

## Quick Start

```bash
# Install Python dependencies
pip install -r requirements.txt

# Generate all test vectors (checked in, but regenerate after formula changes)
python generate_vectors.py
python generate_backtest_vectors.py
python generate_edge_case_vectors.py
python generate_covariance_edge_vectors.py
python generate_construction_basic_vectors.py
python generate_construction_advanced_vectors.py
python generate_risk_measure_vectors.py
python generate_analytics_vectors.py
python generate_indicator_vectors.py
python generate_regime_vectors.py
python generate_misc_vectors.py
python generate_integration_vectors.py
python generate_remaining_construction_vectors.py

# Run Python self-consistency tests
pytest -v

# Run C# cross-language tests (from repo root)
dotnet test --filter "CrossLanguageVerification"
```

## Verification Suites

### Suite 1: Financial Calculation Verification

**Generator:** `generate_vectors.py`
**Tests:** `test_returns.py`, `test_risk.py`, `test_ratios.py`, `test_statistics.py`, `test_covariance.py`, `test_correlation.py`, `test_regression.py`, `test_indicators.py`, `test_attribution.py`
**C# Tests:** `CrossLanguageVerificationTests.cs`

Validates 28 individual financial metrics against independent Python implementations:

| Category | Metrics |
|----------|---------|
| Returns | Cumulative, Annualized, CAGR, Monthly, Annual |
| Risk | Volatility, Downside Deviation, Max Drawdown, VaR, CVaR |
| Ratios | Sharpe, Sortino, Calmar, Omega, Win Rate, Profit Factor |
| Statistics | Skewness, Kurtosis, Alpha, Beta, Information Ratio |
| Portfolio | Covariance (Sample, EWMA, Ledoit-Wolf), Correlation, Factor Regression |
| Indicators | SMA, EMA, Realized Volatility, Momentum Score |
| Attribution | Brinson-Fachler (Allocation, Selection, Interaction) |

### Suite 2: Backtest Verification

**Generator:** `generate_backtest_vectors.py`
**Tests:** `test_backtest_equity.py` (16 tests), `test_backtest_metrics.py` (25 tests)
**C# Tests:** `BacktestCrossLanguageVerificationTests.cs`

10 scenarios using a minimal Python reference backtest engine matching C# conventions exactly:

| # | Scenario | What It Tests |
|---|----------|---------------|
| 1 | Single-asset buy-and-hold | Equity curve, position tracking, daily returns |
| 2 | Single-asset with commission | Commission formula, cash deduction |
| 3 | Multi-asset fixed weights | Position sizing across 2 assets |
| 4 | Commission impact | Side-by-side comparison (with/without) |
| 5 | Periodic rebalancing | 21-day rebalance trades, buy+sell actions |
| 6 | Drawdown / bear market | Max drawdown, trading-day duration, Calmar, VaR/CVaR |
| 7 | Position sizing rounding | `MidpointRounding.AwayFromZero` vs banker's rounding |
| 8 | Benchmark-relative metrics | Alpha, Beta, Information Ratio |
| 9 | Cash precision | Sub-cent cash tracking over 60 days |
| 10 | Three-asset full-year | 252-day portfolio with 16+ tearsheet metrics |

### Suite 3: Edge-Case Verification

**Generator:** `generate_edge_case_vectors.py`
**C# Tests:** `CrossLanguageEdgeCaseTests.cs`, `CrossLanguageEquityCurveTests.cs`

Validates degenerate inputs and EquityCurveExtensions:

| Vector File | What It Tests |
|-------------|---------------|
| `edge_two_elements.json` | N=1/2 minimum-size arrays |
| `edge_all_zero.json` | All-zero returns |
| `edge_identical_positive.json` | Identical values (zero variance) |
| `edge_all_negative.json` | All-negative returns |
| `edge_extreme_returns.json` | Extreme values (+/- 50%) |
| `edge_wipeout.json` | Total loss scenario |
| `edge_single_spike.json` | Single extreme value among zeros |
| `edge_equity_drawdowns.json` | Drawdown identification and duration |
| `edge_equity_monotonic.json` | Monotonically increasing equity (no drawdowns) |
| `edge_monthly_annual_returns.json` | Monthly and annual return calculations |

### Suite 4: Covariance Estimator Verification

**Generator:** `generate_covariance_edge_vectors.py`
**C# Tests:** `CovarianceEstimatorCrossLanguageTests.cs`

| Vector File | What It Tests |
|-------------|---------------|
| `covariance_edge_1asset.json` | Single-asset degenerate case |
| `covariance_edge_2asset.json` | Minimal 2-asset case |
| `covariance_edge_ewma_lambdas.json` | EWMA with lambda 0.5, 0.94, 0.99 and T=3 |
| `covariance_edge_correlated.json` | Highly correlated assets |
| `covariance_edge_denoised.json` | Factor-structured 5-asset matrix (Marcenko-Pastur) |
| `covariance_ledoit_wolf_own.json` | Own Ledoit-Wolf formula (not sklearn) |

### Suite 5: Construction Model Verification

**Generators:** `generate_construction_basic_vectors.py`, `generate_construction_advanced_vectors.py`
**C# Tests:** `ConstructionBasicCrossLanguageTests.cs`, `ConstructionAdvancedCrossLanguageTests.cs`

Basic models (6 vector files):

| Vector File | What It Tests |
|-------------|---------------|
| `construction_equal_weight.json` | N=1,2,3,5,10 assets |
| `construction_inverse_volatility.json` | 2/3/5-asset portfolios |
| `construction_minimum_variance.json` | 2/3/5-asset + constrained |
| `construction_mean_variance.json` | Lambda 0.5/1.0/5.0 + single-asset |
| `construction_risk_parity.json` | 2/3/5-asset portfolios |
| `construction_max_diversification.json` | 1/3/5-asset portfolios |

Advanced models (5 vector files):

| Vector File | What It Tests |
|-------------|---------------|
| `construction_hrp.json` | HRP and Return-Tilted HRP |
| `construction_black_litterman.json` | Black-Litterman with/without views |
| `construction_mean_cvar.json` | Mean-CVaR optimization |
| `construction_mean_sortino.json` | Mean-Sortino optimization |
| `construction_robust_mean_variance.json` | Multi-scenario robust optimization |
| `construction_turnover_voltarget.json` | Turnover penalty and volatility targeting |
| `construction_return_tilted_hrp.json` | Return-tilted HRP with varying kappa (softmax active in all regimes including bear markets) |

### Suite 6: Risk Measure Verification

**Generator:** `generate_risk_measure_vectors.py`
**C# Tests:** `RiskMeasureCrossLanguageTests.cs`

| Vector File | What It Tests |
|-------------|---------------|
| `risk_measure_cvar.json` | CVaR value and gradient computation |
| `risk_measure_downside_deviation.json` | Downside deviation value and gradient |
| `risk_measure_cdar.json` | Conditional Drawdown-at-Risk |
| `risk_rules.json` | MaxDrawdown, MaxPositionSize, MaxSectorExposure rules |

### Suite 7: Analytics Verification

**Generator:** `generate_analytics_vectors.py`
**C# Tests:** `AnalyticsCrossLanguageTests.cs`

| Vector File | What It Tests |
|-------------|---------------|
| `analytics_brinson_fachler.json` | Performance attribution decomposition |
| `analytics_factor_regression.json` | Multi-factor OLS regression |
| `analytics_correlation.json` | Correlation matrix and diversification ratio |
| `analytics_enb.json` | Effective Number of Bets (eigenvalue entropy) |
| `analytics_drawdown.json` | Drawdown period identification |
| `monte_carlo.json` | Bootstrap simulation distributions |
| `walk_forward.json` | Walk-forward optimization folds |

### Suite 8: Indicator & Regime Verification

**Generators:** `generate_indicator_vectors.py`, `generate_regime_vectors.py`
**C# Tests:** `IndicatorCrossLanguageTests.cs`, `RegimeCrossLanguageTests.cs`

| Vector File | What It Tests |
|-------------|---------------|
| `indicator_sma_ema.json` | SMA and EMA with multiple periods |
| `indicator_realvol_momentum.json` | Realized volatility and momentum score |
| `indicator_spread_roc.json` | Spread and rate-of-change indicators |
| `regime_classifier.json` | Growth/inflation regime classification with deadband |

### Suite 9: Miscellaneous Verification

**Generator:** `generate_misc_vectors.py`
**C# Tests:** `MiscCrossLanguageTests.cs`

Covers position sizing, negative return edge cases, and supplementary metric vectors.

### Suite 10: Integration Verification

**Generator:** `generate_integration_vectors.py`
**C# Tests:** `IntegrationCrossLanguageTests.cs`

End-to-end integration scenarios combining construction models, risk management, and backtest engine:

| Vector File | What It Tests |
|-------------|---------------|
| `integration_hrp_backtest.json` | HRP construction through full backtest pipeline |
| `integration_minvar_backtest.json` | Minimum variance construction through backtest |
| `integration_risk_managed.json` | Risk rules rejecting trades during backtest |
| `integration_tactical_overlay.json` | Tactical overlay with regime detection |

### Suite 11: Remaining Construction Model Verification

**Generator:** `generate_remaining_construction_vectors.py`
**C# Tests:** `RemainingConstructionCrossLanguageTests.cs`

Models added post-roadmap with three-layer cross-checks (31/31 hard, 2/2 quality):

| Vector File | What It Tests |
|-------------|---------------|
| `construction_herc.json` | HERC (inverse-risk bisection): 5-asset, 3-asset, 2-asset, identical, single |
| `construction_dynamic_bl.json` | Dynamic Black-Litterman: no views, absolute view, relative view, high confidence |
| `construction_tactical_overlay_direct.json` | Tactical overlay direct algorithm: zero tilts, positive tilt, momentum, combined, floor-at-zero |

## Convention Alignment (Python = C#)

| Convention | Implementation |
|------------|---------------|
| Fill price | Next-bar Open (signals on bar T fill at bar T+1's Open price) |
| Commission | `fillPrice * quantity * rate` (`PercentageOfValueCostModel`) |
| Position rounding | `AwayFromZero` — Python: `math.floor(x + 0.5)` |
| Quantity-limiting | Buy fills clipped to `floor(cash / (fillPrice + commissionPerShare))`; zero rejected |
| Cash on Buy | `-(price * qty + commission)` |
| Cash on Sell | `+(price * qty - commission)` |
| Equity | `sum(position * AdjustedClose) + cash` |
| Drawdown duration | Trading days (equity curve index-based), not calendar days |
| Daily returns | `(equity[t] - equity[t-1]) / equity[t-1]` |

## Precision Tiers

| Tier | Value | Use |
|------|-------|-----|
| `PRECISION_EXACT` | `1e-10` | Returns, simple ratios, deterministic arithmetic |
| `PRECISION_NUMERIC` | `1e-6` | Optimizers, matrix operations, iterative algorithms |
| `PRECISION_STATISTICAL` | `1e-4` | Monte Carlo, bootstrap sampling, Jacobi eigenvector reconstruction |

## Vector Files

All 81 JSON vectors are checked into `vectors/` so C# tests run without requiring Python. Regenerate after changing any formula. Vectors use deterministic seeds for reproducibility. Last regenerated: 2026-03-30 (ReturnTiltedHRP softmax fix — bear market tilt now applied).

## File Map

| File | Purpose |
|------|---------|
| `conftest.py` | Shared fixtures, precision constants, vector I/O helpers |
| `requirements.txt` | Python dependencies (numpy, scipy, pandas, pytest, pypfopt, sklearn, etc.) |
| **Generators** | |
| `generate_vectors.py` | Financial calculation vectors (28 metrics) |
| `generate_backtest_vectors.py` | Backtest pipeline vectors (10 scenarios) |
| `generate_edge_case_vectors.py` | Degenerate input and equity curve vectors |
| `generate_covariance_edge_vectors.py` | Covariance estimator vectors (6 files) |
| `generate_construction_basic_vectors.py` | Basic construction model vectors (6 files) |
| `generate_construction_advanced_vectors.py` | Advanced construction model vectors |
| `generate_risk_measure_vectors.py` | Risk measure and risk rule vectors |
| `generate_analytics_vectors.py` | Analytics vectors (attribution, regression, etc.) |
| `generate_indicator_vectors.py` | Technical indicator vectors |
| `generate_regime_vectors.py` | Regime classifier vectors |
| `generate_misc_vectors.py` | Position sizing and supplementary vectors |
| `generate_integration_vectors.py` | End-to-end integration vectors |
| `generate_remaining_construction_vectors.py` | HERC, DynamicBL, TacticalOverlay vectors |
| **Python Tests** | |
| `test_returns.py` | Return metric self-consistency |
| `test_risk.py` | Risk metric self-consistency |
| `test_ratios.py` | Ratio self-consistency (Sharpe, Sortino, Calmar, etc.) |
| `test_statistics.py` | Skewness, Kurtosis self-consistency |
| `test_covariance.py` | Covariance estimator self-consistency |
| `test_correlation.py` | Correlation matrix self-consistency |
| `test_regression.py` | Factor regression self-consistency |
| `test_indicators.py` | Technical indicator self-consistency |
| `test_attribution.py` | Brinson-Fachler attribution self-consistency |
| `test_backtest_equity.py` | Backtest equity/position/cash self-consistency (16 tests) |
| `test_backtest_metrics.py` | Backtest tearsheet metric self-consistency (25 tests) |
| `vectors/` | 81 golden JSON test vector files (checked in) |
