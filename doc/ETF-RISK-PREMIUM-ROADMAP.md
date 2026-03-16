# Boutquin.Trading — ETF Risk-Premium Harvesting Roadmap

> **Date:** 2026-03-16
> **Scope:** Reprioritized gap analysis and phased roadmap for building diversified ETF portfolios that harvest well-documented risk premia
> **Companion:** [COMPETITIVE-ANALYSIS.md](COMPETITIVE-ANALYSIS.md) — full competitive benchmarking and original gap analysis

---

## Table of Contents

1. [Investment Thesis](#1-investment-thesis)
2. [Target Portfolio Archetypes](#2-target-portfolio-archetypes)
3. [What the Framework Can Do Today](#3-what-the-framework-can-do-today)
4. [Reprioritized Gap Analysis](#4-reprioritized-gap-analysis)
5. [Phased Roadmap](#5-phased-roadmap)
6. [Appendix A: Risk Premia Definitions](#appendix-a-risk-premia-definitions)
7. [Appendix B: Candidate ETF Universe](#appendix-b-candidate-etf-universe)
8. [Appendix C: Key Academic References](#appendix-c-key-academic-references)

---

## 1. Investment Thesis

**Risk-premium harvesting** is the systematic capture of excess returns that compensate investors for bearing specific, well-documented risks. Unlike discretionary stock-picking or technical-indicator timing, this approach rests on decades of peer-reviewed evidence that certain return drivers persist across markets and time periods because they represent compensation for genuine economic risks or structural/behavioral inefficiencies.

### 1.1 Core Premia Targeted

| Premium | Source | Typical Vehicle (ETF) | Academic Support |
|---------|--------|-----------------------|------------------|
| **Equity** | Stocks vs. risk-free rate | Broad market ETFs (VTI, VXUS) | Mehra & Prescott 1985 |
| **Term** | Long-duration bonds vs. short-duration | Long treasury ETFs (TLT, VGLT) vs. short (SHV, BIL) | Fama & Bliss 1987 |
| **Credit** | Corporate bonds vs. government bonds | Investment-grade (LQD) / high-yield (HYG) vs. treasuries | Elton et al. 2001 |
| **Value** | Value stocks vs. growth stocks | Value factor ETFs (VTV, VLUE, IVAL) | Fama & French 1992 |
| **Size** | Small-cap vs. large-cap | Small-cap ETFs (VB, SCHA, VSS) | Fama & French 1993 |
| **Momentum** | Recent winners vs. recent losers | Momentum ETFs (MTUM, IMTM) | Jegadeesh & Titman 1993 |
| **Low Volatility** | Low-vol stocks vs. high-vol stocks | Low-vol ETFs (USMV, SPLV, EFAV) | Baker, Bradley & Wurgler 2011 |
| **Carry** | High-yield currencies/assets vs. low-yield | Currency-hedged ETFs, EM bond ETFs (EMB, EMLC) | Koijen et al. 2018 |
| **Trend / Managed Futures** | Long trending assets, short declining | Managed futures ETFs (DBMF, CTA, KMLM) | Moskowitz, Ooi & Pedersen 2012 |
| **Real Assets** | Inflation-linked real returns | TIPS (TIP, SCHP), REITs (VNQ, VNQI), Commodities (DJP, PDBC) | Gorton & Rouwenhorst 2006 |

### 1.2 Why ETFs

- **Low cost**: Expense ratios 0.03–0.40% for most factor/asset-class ETFs
- **Diversification**: Single ETF provides exposure to hundreds/thousands of securities
- **Liquidity**: Tight spreads, intraday trading, no minimum investment
- **Transparency**: Holdings disclosed daily; no style drift hidden in a fund mandate
- **Tax efficiency**: In-kind creation/redemption minimizes capital gains distributions
- **Accessibility**: All premia listed above are accessible via widely-traded ETFs

### 1.3 Alpha via Allocation, Not Security Selection

The alpha in this approach comes from:

1. **Strategic asset allocation** — harvesting multiple premia simultaneously with diversification benefits (low correlation between premia)
2. **Tactical tilting** — modestly adjusting weights based on valuation signals, momentum, or volatility regime (not market timing, but regime-aware rebalancing)
3. **Disciplined rebalancing** — systematically buying underperforming assets (contra-cyclical) captures mean-reversion across premia
4. **Cost control** — minimizing implementation costs preserves the net premium

---

## 2. Target Portfolio Archetypes

These represent the portfolio strategies the framework should support end-to-end:

### 2.1 Strategic Risk Parity

Allocate risk (not capital) equally across uncorrelated risk premia. Classic approach from Bridgewater's All Weather.

```
Target: Equal risk contribution from each premium bucket
Rebalance: Monthly or quarterly
Inputs: Covariance matrix (rolling or exponentially-weighted)
ETFs: VTI, VXUS, TLT, IEF, TIP, VNQ, DJP, GLD
```

### 2.2 Factor Tilt Portfolio

Market-cap core + strategic tilts toward value, size, momentum, low-vol premia.

```
Target: 60% core market + 40% split across factor ETFs
Rebalance: Quarterly
Inputs: Fixed target weights, optional momentum-based tilt adjustment
ETFs: VTI, VXUS, VTV, VB, MTUM, USMV
```

### 2.3 Global Balanced with Alternatives

Traditional 60/40 enhanced with real assets and trend-following to improve tail-risk behavior.

```
Target: 40% equity, 20% fixed income, 15% real assets, 15% trend, 10% alternatives
Rebalance: Monthly
Inputs: Fixed strategic weights with calendar rebalancing
ETFs: VTI, VXUS, AGG, TLT, TIP, VNQ, DJP, GLD, DBMF
```

### 2.4 Tactical All-Weather

Regime-aware variant: shift weights based on economic environment (growth/inflation signals from yield curve, breakeven inflation, credit spreads).

```
Target: Varies by regime — risk-on, risk-off, inflationary, deflationary
Rebalance: Monthly with regime signal
Inputs: Macro signals → regime classification → target weights per regime
ETFs: Full universe from Appendix B
```

---

## 3. What the Framework Can Do Today

| Capability | Status | Notes |
|-----------|--------|-------|
| Multi-asset portfolio with fixed weights | **Working** | `FixedWeightPositionSizer` + `RebalancingBuyAndHoldStrategy` |
| Calendar-based rebalancing | **Working** | Daily / Weekly / Monthly / Quarterly / Annual via `RebalancingFrequency` |
| Multi-currency support | **Working** | FX conversion built into portfolio value calculation |
| Equity data (Tiingo) | **Working** | Supports ETFs — Tiingo covers all US-listed ETFs |
| FX rates (Frankfurter) | **Working** | ECB-sourced, 34 currencies |
| Performance metrics | **Working** | Sharpe, Sortino, Alpha, Beta, Information Ratio, CAGR, Max Drawdown, Volatility |
| Event-driven backtest | **Working** | Full pipeline: MarketEvent → Signal → Order → Fill |
| Benchmark comparison | **Working** | Alpha and Beta vs. benchmark portfolio |

**Bottom line:** You can already backtest a simple fixed-weight ETF portfolio with calendar rebalancing and compare it to a benchmark. The framework needs enhancements to do risk-premium harvesting *well*.

---

## 4. Reprioritized Gap Analysis

Gaps are re-ranked by their importance to the ETF risk-premium harvesting use case. Original gap IDs from [COMPETITIVE-ANALYSIS.md](COMPETITIVE-ANALYSIS.md) are preserved for traceability.

### Tier 1 — Required for Any Risk-Premium Portfolio

These gaps block the core use case entirely or produce unreliable results.

| Priority | Original ID | Gap | Why It's Critical for Risk-Premium Harvesting |
|----------|-------------|-----|------------------------------------------------|
| **T1-1** | G10 | **Incomplete performance metrics** | Cannot evaluate risk premia without VaR, CVaR, Calmar, Omega, skewness, kurtosis. Risk premia are *defined* by their risk characteristics — measuring only Sharpe is insufficient. |
| **T1-2** | G9 | **No transaction cost model beyond flat commission** | ETF portfolios incur bid-ask spreads, commission per trade, and (for leveraged/commodity ETFs) roll costs. Understating costs can make a negative-alpha strategy appear profitable. |
| **T1-3** | G8 | **No slippage model** | Rebalancing multi-ETF portfolios generates simultaneous orders. Without slippage, backtest results are unrealistically optimistic, especially for less-liquid ETFs (commodity, EM, alternatives). |
| **T1-4** | G2 | **No risk management module** | Risk-premium harvesting *is* risk management. Need: max drawdown limits, position size constraints, exposure budgets per premium bucket, and correlation-based guards. |
| **T1-5** | NEW | **No portfolio construction models beyond fixed-weight** | Cannot build risk parity, minimum variance, mean-variance, or Black-Litterman portfolios. Need `IPortfolioConstructionModel` with at least: `EqualWeight`, `InverseVolatility`, `RiskParity`, `MeanVariance`. |
| **T1-6** | NEW | **No covariance / correlation estimation** | Risk parity and mean-variance require a covariance matrix. Need rolling-window and exponentially-weighted covariance estimators, plus shrinkage methods (Ledoit-Wolf). |
| **T1-7** | NEW | **No return attribution / factor decomposition** | Cannot tell *which* premia are contributing returns. Need Brinson-style attribution or regression-based factor decomposition to validate the strategy is harvesting intended premia. |
| **T1-8** | G7 | **Mutable state on interfaces** | A portfolio construction optimizer that reads `IStrategy.Positions` during computation while another thread mutates it produces non-deterministic results. Must fix before building stateful allocation models. |

### Tier 2 — Required for Tactical / Advanced Portfolios

These gaps limit the sophistication of the strategies but don't block the basic use case.

| Priority | Original ID | Gap | Why It Matters for Risk-Premium Harvesting |
|----------|-------------|-----|---------------------------------------------|
| **T2-1** | G1 | **No indicator library** | Tactical tilting requires momentum indicators (SMA crossover, 12-1 month momentum), volatility indicators (realized vol, VIX-like), and macro signals (yield curve slope). Not needed for static strategic allocation, but essential for tactical variants. |
| **T2-2** | G4 | **No universe selection** | Cannot dynamically filter ETFs (e.g., exclude ETFs with AUM < $100M, include only ETFs older than 3 years). Needed for robust universe construction. |
| **T2-3** | G5 | **No DI container** | Swapping portfolio construction models (risk parity vs. mean-variance) or cost models requires manual rewiring. DI makes strategy composition declarative and testable. |
| **T2-4** | NEW | **No regime detection** | Tactical all-weather needs economic regime classification (growth/inflation quadrant). Need rolling macro indicators and a regime classifier. |
| **T2-5** | NEW | **No rolling / expanding window calculations** | Covariance estimation, momentum scoring, and volatility targeting all require windowed computations. Need a generic rolling-window abstraction. |
| **T2-6** | G16 | **No walk-forward analysis / Monte Carlo** | Must validate out-of-sample to confirm premium capture isn't curve-fitted. Walk-forward and bootstrap resampling are standard robustness tests for factor strategies. |
| **T2-7** | NEW | **No drawdown-based rebalancing trigger** | Calendar rebalancing is suboptimal. Threshold-based rebalancing (rebalance when drift exceeds X%) is more cost-efficient and better captures mean-reversion. |

### Tier 3 — Important for Production Quality

These gaps affect usability, debugging, and operational readiness but not the analytical validity of results.

| Priority | Original ID | Gap | Relevance |
|----------|-------------|-----|-----------|
| **T3-1** | G11 | **No visualization** | Cannot visually inspect equity curves, drawdowns, allocation drift, factor exposures over time. Need at minimum: equity curve chart, asset allocation area chart, drawdown chart, monthly returns heatmap. |
| **T3-2** | I4 / G5 | **No DI + configuration system** | Hard to run parameter studies (e.g., "risk parity with 60-day vs. 120-day covariance window") without a proper configuration pipeline. |
| **T3-3** | I8 | **No logging** | Debugging allocation decisions (why did risk parity put 40% in bonds last month?) requires structured logging of intermediate computations. |
| **T3-4** | I6 | **No `CancellationToken` support** | Long backtest runs (20+ years, 10+ ETFs) need cancellation support. |
| **T3-5** | I1 | **Business logic in interfaces** | `IStrategy` default implementations make it harder to create new portfolio construction strategies without accidentally inheriting inappropriate behavior. |

### Tier 4 — Deferred (Not Needed for ETF Risk-Premium Harvesting)

These original gaps are deprioritized because they don't serve the target use case.

| Original ID | Gap | Why Deferred |
|-------------|-----|-------------|
| G6 | Plugin/extension architecture | Single-user research framework; not building a platform |
| G12 | Live broker integration | Backtest-first; live trading is a separate initiative |
| G13 | Options/futures/crypto support | Using ETFs as the vehicle; no need for derivative instruments |
| G14 | Order book / Level 2 data | End-of-day ETF rebalancing; no microstructure modeling needed |
| G15 | Vectorized computation path | Performance is adequate for daily-frequency ETF backtests |
| G17 | ML integration pipeline | Risk-premium harvesting is evidence-based, not ML-driven |
| G18 | Web-based UI / dashboard | Console + HTML report is sufficient for research |
| G19 | State persistence | Daily ETF backtests are fast; no need for checkpointing |
| G20 | Scheduling / real-time event bus | End-of-day strategies; no intraday needs |

---

## 5. Phased Roadmap

### Phase 1: Reliable Backtesting (Foundation)

**Goal:** Produce backtests whose results you can trust — realistic cost modeling and complete risk metrics.

| ID | Task | Addresses | TDD Acceptance Criteria |
|----|------|-----------|------------------------|
| RP1-01 | **Transaction cost model** — `ITransactionCostModel` with `FixedPerTrade`, `PerShare`, `PercentageOfValue`, `TieredCommission` | T1-2 | Tests: $10/trade flat commission for 5-ETF rebalance = $50 total; percentage model on $100k order at 0.1% = $100; tiered model transitions correctly at volume breakpoints |
| RP1-02 | **Spread cost model** — `ISpreadCostModel` with configurable bid-ask spreads per ETF | T1-2 | Tests: ETF with 0.02% spread on $50k order costs $10; total cost = commission + spread; illiquid ETF (0.15% spread) correctly penalizes small positions |
| RP1-03 | **Slippage model** — `ISlippageModel` with `NoSlippage`, `FixedSlippage`, `PercentageSlippage` | T1-3 | Tests: Market order fill price = close + slippage; percentage slippage scales with order price; no slippage model gives fill at exact close price |
| RP1-04 | **Extended analytics** — Calmar Ratio, Omega Ratio, VaR (historical + parametric), CVaR, Skewness, Kurtosis, Win/Loss Rate, Profit Factor, Recovery Factor | T1-1 | Tests: Each metric validated against hand-calculated values; VaR(95%) ≤ CVaR(95%); Calmar = CAGR / MaxDrawdown; edge cases (no drawdown, all losses) handled |
| RP1-05 | **Monthly / annual returns table** | T1-1 | Tests: Returns table matches manual calculation; partial year handled; leap year handled; sum of monthly returns approximates annual (geometric) |
| RP1-06 | **Fix mutable state on interfaces** — `IStrategy.Positions` → `IReadOnlyDictionary`, `IStrategy.Cash` → `IReadOnlyDictionary` | T1-8 | Tests: Cannot cast interface collections to mutable; internal mutation in `StrategyBase` still works; serialization round-trip succeeds |
| RP1-07 | **Extract `StrategyBase`** from `IStrategy` default implementations | T3-5 | Tests: All existing strategy tests pass; new strategy can extend `StrategyBase` and override individual methods |

### Phase 2: Portfolio Construction Models (Core Alpha)

**Goal:** Implement the allocation models that define risk-premium harvesting strategies.

| ID | Task | Addresses | TDD Acceptance Criteria |
|----|------|-----------|------------------------|
| RP2-01 | **Rolling-window abstraction** — `RollingWindow<T>` with configurable length, support for `decimal[]` returns | T2-5 | Tests: Window of size 60 drops oldest on add; `IsFull` false until 60 samples; `ToArray()` returns chronological order |
| RP2-02 | **Covariance estimation** — `ICovarianceEstimator` with `SampleCovariance`, `ExponentiallyWeightedCovariance`, `LedoitWolfShrinkage` | T1-6 | Tests: 2x2 covariance of perfectly correlated series = [σ² σ²; σ² σ²]; EWMA weights recent data more (verify with known decay); Ledoit-Wolf shrinkage pulls eigenvalues toward grand mean; sample-based uses N-1 divisor |
| RP2-03 | **`IPortfolioConstructionModel` interface** with methods: `ComputeTargetWeights(assets, returns, constraints) → Dictionary<Asset, decimal>` | T1-5 | Tests: Weights sum to 1.0 (or configured leverage); all weights ≥ 0 (long-only constraint); empty asset list returns empty weights |
| RP2-04 | **Equal-weight model** — `EqualWeightConstruction` | T1-5 | Tests: 4 ETFs → each gets 25%; single ETF → 100%; weights sum to 1.0 |
| RP2-05 | **Inverse-volatility model** — `InverseVolatilityConstruction` | T1-5 | Tests: Asset with half the volatility gets double the weight; zero-vol asset throws `CalculationException`; weights sum to 1.0 |
| RP2-06 | **Risk-parity model** — `RiskParityConstruction` (equal risk contribution via iterative optimization) | T1-5 | Tests: Marginal risk contribution of each asset equal (within tolerance); result stable across iterations; converges in <100 iterations for 10-asset portfolio; uses covariance estimator from RP2-02 |
| RP2-07 | **Mean-variance model** — `MeanVarianceConstruction` (Markowitz efficient frontier, max Sharpe) | T1-5 | Tests: Max-Sharpe portfolio on 2-asset case matches closed-form solution; constraints respected (no short, max weight); degenerate case (identical assets) returns equal weight |
| RP2-08 | **Minimum-variance model** — `MinimumVarianceConstruction` | T1-5 | Tests: Result has lower portfolio variance than equal-weight and inverse-vol on same inputs; constraints respected |
| RP2-09 | **Black-Litterman model** — `BlackLittermanConstruction` with prior (equilibrium) + views | T1-5 | Tests: With no views, output = equilibrium weights; single absolute view shifts weight toward view asset; confidence parameter scales view impact |
| RP2-10 | **Threshold-based rebalancing trigger** — Rebalance when any asset drifts beyond configurable band (e.g., ±5%) | T2-7 | Tests: No rebalance when all within band; single asset at +6% triggers full rebalance; rebalancing resets drift to zero |
| RP2-11 | **Wire construction models into strategy pipeline** — `RebalancingStrategy` accepts `IPortfolioConstructionModel` and recomputes weights at each rebalance | T1-5 | Tests: Risk-parity strategy backtest produces different weights over time; weights change as covariance changes; end-to-end backtest with risk parity completes without error |

### Phase 3: Analytics & Attribution (Validate the Thesis)

**Goal:** Measure whether the strategy is actually harvesting the intended premia.

| ID | Task | Addresses | TDD Acceptance Criteria |
|----|------|-----------|------------------------|
| RP3-01 | **Return attribution** — Brinson-Fachler model: allocation effect + selection effect + interaction | T1-7 | Tests: Portfolio with same weights as benchmark → zero allocation effect; portfolio with same selection as benchmark → zero selection effect; effects sum to total active return |
| RP3-02 | **Factor regression** — Regress portfolio returns against Fama-French factors (Mkt-Rf, SMB, HML, RMW, CMA, Mom) | T1-7 | Tests: Pure market portfolio has Beta≈1, all other loadings≈0; value-tilted portfolio has positive HML loading; R² > 0 for diversified portfolio |
| RP3-03 | **Correlation analysis** — Rolling correlation matrix across portfolio assets; diversification ratio | T1-7 | Tests: Perfectly correlated assets → diversification ratio = 1.0; uncorrelated → ratio > 1; rolling window produces time series |
| RP3-04 | **Drawdown analysis** — Detailed drawdown table: start date, trough date, recovery date, depth, duration, recovery time | T1-1 | Tests: Known equity curve produces correct drawdown periods; overlapping drawdowns handled; ongoing drawdown has no recovery date |
| RP3-05 | **HTML report generator** — Interactive tearsheet with: equity curve, drawdown chart, asset allocation over time, monthly returns heatmap, risk contribution pie chart, factor exposure bar chart | T3-1 | Tests: Generated HTML is valid; contains all expected charts; file size < 2MB for 10-year backtest |
| RP3-06 | **Benchmark comparison report** — Side-by-side metrics table, relative equity curve, tracking error, information ratio over time | T3-1 | Tests: Benchmark line appears on equity chart; tracking error calculated correctly; metrics table contains both portfolio and benchmark columns |

### Phase 4: Tactical Enhancements (Advanced Strategies)

**Goal:** Add tactical overlay capabilities for regime-aware allocation.

| ID | Task | Addresses | TDD Acceptance Criteria |
|----|------|-----------|------------------------|
| RP4-01 | **Core indicators** — SMA, EMA for trend signals; realized volatility (rolling std dev); momentum score (12-1 month return) | T2-1 | Tests: SMA(10) matches known values; EMA warm-up correct; 12-1 month momentum excludes most recent month |
| RP4-02 | **Macro indicators** — Yield curve slope (10Y - 2Y), breakeven inflation (10Y nominal - 10Y TIPS), credit spread (HYG yield - treasury yield) | T2-4 | Tests: Known yield curve data produces correct slope; breakeven calculated from ETF price ratio; spread widens during stress periods in test data |
| RP4-03 | **Regime classifier** — `IRegimeClassifier` with `GrowthInflationRegime` (4 quadrants: rising/falling growth × rising/falling inflation) | T2-4 | Tests: Known macro data maps to correct quadrant; regime changes trigger weight adjustment; ambiguous data uses prior regime (hysteresis) |
| RP4-04 | **Tactical overlay model** — `TacticalOverlayConstruction` that adjusts strategic weights based on regime and momentum signals | T2-4 | Tests: In risk-off regime, equity weight reduced by configured tilt; momentum signal overweights trending assets; total weights still sum to 1.0 |
| RP4-05 | **Volatility targeting** — Scale portfolio exposure to target a specific volatility level (e.g., 10% annualized) | T2-1 | Tests: When realized vol = 15% and target = 10%, exposure scaled to 10/15 = 66.7%; leverage capped at configurable maximum |
| RP4-06 | **Walk-forward validation** — `WalkForwardOptimizer` with in-sample / out-of-sample splits | T2-6 | Tests: Out-of-sample results differ from in-sample; no look-ahead bias (verify via date filtering); parameter selected from in-sample used in out-of-sample |
| RP4-07 | **Monte Carlo robustness test** — Bootstrap resampling of daily returns | T2-6 | Tests: 1000 simulations produce distribution of Sharpe ratios; 5th percentile Sharpe reported as "worst-case"; confidence intervals calculated |
| RP4-08 | **Universe filtering** — `IUniverseSelector` with `MinAumFilter`, `MinAgeFilter`, `LiquidityFilter` for ETFs | T2-2 | Tests: ETF below AUM threshold excluded; ETF younger than age threshold excluded; filters compose (AND logic) |

### Phase 5: Infrastructure Polish

**Goal:** Make the framework pleasant to use for ongoing research.

| ID | Task | Addresses | TDD Acceptance Criteria |
|----|------|-----------|------------------------|
| RP5-01 | **DI container** — `Microsoft.Extensions.DependencyInjection` for all services | T2-3, T3-2 | Tests: All services resolve; strategy with risk-parity construction model injected correctly; swap to mean-variance via config |
| RP5-02 | **Configuration via `IOptions<T>`** — Backtest dates, cost model parameters, rebalance frequency, construction model choice | T3-2 | Tests: Config from `appsettings.json` loads correctly; override via environment variable works; defaults match current behavior |
| RP5-03 | **Structured logging** — `ILogger<T>` throughout, especially in portfolio construction (log computed weights, rebalance decisions) | T3-3 | Tests: Weight computation logs target weights at Info level; rebalance trigger logs at Debug; no exceptions with NullLogger |
| RP5-04 | **`CancellationToken` on async APIs** | T3-4 | Tests: Cancelled backtest throws `OperationCanceledException`; partial results available up to cancellation point |
| RP5-05 | **Risk management module** — `IRiskManager` with `MaxDrawdownRule`, `MaxPositionSizeRule`, `MaxSectorExposureRule` | T1-4 | Tests: Order rejected when drawdown > configured limit; position size capped; exposure per asset class capped |

---

## Appendix A: Risk Premia Definitions

### Equity Risk Premium
The excess return of equities over the risk-free rate. Compensates for bearing systematic market risk (business cycle, earnings volatility). Historically ~4-6% annualized in developed markets. Harvested via broad market index ETFs.

### Term Premium
The excess return of long-duration bonds over short-duration bonds. Compensates for interest rate risk and inflation uncertainty. Historically ~1-2% annualized. Harvested via duration positioning across the yield curve.

### Credit Premium
The excess return of corporate bonds over duration-matched government bonds. Compensates for default risk. Historically ~1-3% annualized for investment-grade, higher for high-yield. Harvested via corporate bond ETFs relative to treasuries.

### Value Premium
The excess return of cheap stocks (low P/B, P/E, or P/CF) over expensive stocks. First documented by Fama & French (1992). Historically ~2-4% annualized, though weaker in recent decades. Harvested via value-factor ETFs.

### Size Premium
The excess return of small-capitalization stocks over large-capitalization stocks. Part of the Fama-French three-factor model. Historically ~1-3% annualized, debated in recent data. Harvested via small-cap ETFs.

### Momentum Premium
The tendency of recent winners to continue outperforming and recent losers to continue underperforming over 3-12 month horizons. Among the most robust anomalies across asset classes and geographies. Historically ~4-8% annualized (long-short). Harvested via momentum-factor ETFs.

### Low-Volatility Premium
Low-volatility stocks deliver risk-adjusted returns equal to or better than high-volatility stocks, contradicting CAPM. Explained by leverage constraints and lottery preferences. Harvested via minimum-volatility ETFs.

### Carry Premium
The return from holding higher-yielding assets funded by lower-yielding assets. Applies across currencies, bonds, and commodities. Harvested via currency carry or EM bond ETFs.

### Trend / Time-Series Momentum
The tendency of assets in uptrends to continue rising and downtrends to continue falling. Distinct from cross-sectional momentum. Applied across asset classes in managed-futures strategies. Harvested via managed-futures ETFs.

---

## Appendix B: Candidate ETF Universe

### US Equities
| Ticker | Name | Expense Ratio | Premium |
|--------|------|---------------|---------|
| VTI | Vanguard Total Stock Market | 0.03% | Equity |
| VOO | Vanguard S&P 500 | 0.03% | Equity |
| VTV | Vanguard Value | 0.04% | Value |
| VUG | Vanguard Growth | 0.04% | (Growth baseline) |
| VB | Vanguard Small-Cap | 0.05% | Size |
| VBR | Vanguard Small-Cap Value | 0.07% | Value + Size |
| MTUM | iShares MSCI USA Momentum | 0.15% | Momentum |
| USMV | iShares MSCI USA Min Vol | 0.15% | Low Volatility |
| VLUE | iShares MSCI USA Value | 0.15% | Value |

### International Equities
| Ticker | Name | Expense Ratio | Premium |
|--------|------|---------------|---------|
| VXUS | Vanguard Total International | 0.07% | Equity (non-US) |
| VEA | Vanguard Developed Markets | 0.05% | Equity (DM) |
| VWO | Vanguard Emerging Markets | 0.08% | Equity (EM) |
| IVAL | Alpha Architect Intl Value | 0.39% | Value (Intl) |
| IMTM | iShares MSCI Intl Momentum | 0.30% | Momentum (Intl) |
| EFAV | iShares MSCI Intl Min Vol | 0.20% | Low Volatility (Intl) |

### Fixed Income
| Ticker | Name | Expense Ratio | Premium |
|--------|------|---------------|---------|
| BND | Vanguard Total Bond Market | 0.03% | Credit + Term |
| AGG | iShares Core US Aggregate | 0.03% | Credit + Term |
| TLT | iShares 20+ Year Treasury | 0.15% | Term (long) |
| IEF | iShares 7-10 Year Treasury | 0.15% | Term (intermediate) |
| SHV | iShares Short Treasury | 0.15% | (Risk-free proxy) |
| BIL | SPDR 1-3 Month T-Bill | 0.14% | (Risk-free proxy) |
| LQD | iShares Investment Grade Corp | 0.14% | Credit (IG) |
| HYG | iShares High Yield Corp | 0.49% | Credit (HY) |
| TIP | iShares TIPS Bond | 0.19% | Inflation protection |
| SCHP | Schwab US TIPS | 0.03% | Inflation protection |
| EMB | iShares JP Morgan EM Bond | 0.39% | Carry (EM sovereign) |

### Real Assets
| Ticker | Name | Expense Ratio | Premium |
|--------|------|---------------|---------|
| VNQ | Vanguard Real Estate | 0.12% | Real estate |
| VNQI | Vanguard Intl Real Estate | 0.12% | Real estate (Intl) |
| DJP | iPath Bloomberg Commodity | 0.70% | Commodity carry |
| PDBC | Invesco Optimum Yield Diversified | 0.59% | Commodity carry |
| GLD | SPDR Gold Shares | 0.40% | Safe haven / inflation |
| IAU | iShares Gold Trust | 0.25% | Safe haven / inflation |

### Alternatives / Trend
| Ticker | Name | Expense Ratio | Premium |
|--------|------|---------------|---------|
| DBMF | iMGP DBi Managed Futures | 0.85% | Trend |
| CTA | Simplify Managed Futures | 0.75% | Trend |
| KMLM | KFA Mount Lucas Managed Futures | 0.90% | Trend |

---

## Appendix C: Key Academic References

1. **Fama, E.F. & French, K.R. (1993).** "Common risk factors in the returns on stocks and bonds." *Journal of Financial Economics*, 33(1), 3-56. — Foundation of value and size premia.

2. **Jegadeesh, N. & Titman, S. (1993).** "Returns to buying winners and selling losers: Implications for stock market efficiency." *Journal of Finance*, 48(1), 65-91. — Original momentum premium documentation.

3. **Moskowitz, T.J., Ooi, Y.H. & Pedersen, L.H. (2012).** "Time series momentum." *Journal of Financial Economics*, 104(2), 228-250. — Trend-following across asset classes.

4. **Asness, C.S., Moskowitz, T.J. & Pedersen, L.H. (2013).** "Value and momentum everywhere." *Journal of Finance*, 68(3), 929-985. — Value and momentum premia across asset classes and geographies.

5. **Ilmanen, A. (2011).** *Expected Returns: An Investor's Guide to Harvesting Market Rewards.* Wiley. — Comprehensive treatment of all major risk premia.

6. **Maillard, S., Roncalli, T. & Teïletche, J. (2010).** "The properties of equally weighted risk contribution portfolios." *Journal of Portfolio Management*, 36(4), 60-70. — Risk parity theory and implementation.

7. **Ledoit, O. & Wolf, M. (2004).** "A well-conditioned estimator for large-dimensional covariance matrices." *Journal of Multivariate Analysis*, 88(2), 365-411. — Shrinkage estimation for portfolio optimization.

8. **Black, F. & Litterman, R. (1992).** "Global portfolio optimization." *Financial Analysts Journal*, 48(5), 28-43. — Bayesian approach to combining market equilibrium with investor views.

9. **Baker, M., Bradley, B. & Wurgler, J. (2011).** "Benchmarks as limits to arbitrage: Understanding the low-volatility anomaly." *Financial Analysts Journal*, 67(1), 40-54. — Low-volatility premium explanation.

10. **Ang, A. (2014).** *Asset Management: A Systematic Approach to Factor Investing.* Oxford University Press. — Modern factor investing framework.

11. **Koijen, R.S.J., Moskowitz, T.J., Pedersen, L.H. & Vrugt, E.B. (2018).** "Carry." *Journal of Financial Economics*, 127(2), 197-225. — Carry premium across asset classes.

12. **Harvey, C.R., Liu, Y. & Zhu, H. (2016).** "...and the Cross-Section of Expected Returns." *Review of Financial Studies*, 29(1), 5-68. — Cautionary paper on data mining in factor research (tests 316 published factors).

---

*This document is a living roadmap. Priorities may shift as implementation progresses and backtest results reveal which premia are most reliably captured in the ETF vehicle.*
