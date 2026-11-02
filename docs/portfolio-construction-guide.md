# Portfolio Construction Guide

This guide helps practitioners select the right portfolio construction model for their use case. Boutquin.Trading provides 19 base models and 2 decorators that can be composed to cover most allocation strategies.

## Decision Framework

### Step 1: Choose Your Philosophy

| Philosophy | Models | Key Property |
|------------|--------|-------------|
| Agnostic (no return views) | EqualWeight, InverseVolatility, MinimumVariance, RiskParity, MaximumDiversification, HRP, HERC, PrincipalComponentRP | No expected-return input needed |
| Return-aware | MeanVariance, BlackLitterman, DynamicBlackLitterman, ReturnTiltedHRP, MeanCVaR, MeanSortino, TacticalOverlay | Requires return forecast or views |
| Adaptive | VolatilityTargeting, RobustMeanVariance, RegimeWeightConstrained | Responds to market conditions |

### Step 2: Consider Your Constraints

| Constraint | Solution |
|------------|----------|
| Maximum/minimum position size | WeightConstrained decorator |
| Turnover budget | TurnoverPenalized decorator |
| High-dimensional portfolio (N > 50) | HRP, HERC, or PCA-Constrained decorator |
| Regime-dependent allocation | RegimeWeightConstrained or TacticalOverlay |
| Tail risk sensitivity | MeanCVaR or MeanSortino |

## Model Reference

### Naive Models

#### EqualWeight

Allocates 1/N to each asset. No estimation risk. Surprisingly competitive baseline -- DeMiguel, Garlappi, and Uppal (2009) showed 1/N outperforms many optimized portfolios out-of-sample when estimation error is high.

**When to use:** Baseline comparison, very short history, or when estimation risk dominates.

#### InverseVolatility

Weight inversely proportional to realized volatility: w_i = (1/sigma_i) / sum(1/sigma_j). Tilts toward lower-volatility assets without requiring correlation estimates.

**When to use:** When you trust volatility estimates but not correlation estimates. Good for asset classes with structurally different volatilities.

### Variance-Based Models

#### MinimumVariance

Minimizes portfolio variance via projected gradient descent with simplex projection. Does not require expected returns -- only the covariance matrix.

**When to use:** When minimizing risk is the primary objective and return forecasting is unreliable. Produces concentrated portfolios unless combined with WeightConstrained.

#### MeanVariance

Maximizes the Sharpe ratio: max w'mu - (lambda/2) w'Sigma w. Uses projected gradient descent. Highly sensitive to expected return inputs.

**When to use:** When you have reliable return forecasts. Combine with Ledoit-Wolf or Denoised covariance estimator to reduce estimation error. Lambda controls risk aversion (higher = more conservative).

#### BlackLitterman

Bayesian framework that combines market-implied equilibrium returns with investor views. Produces more stable weights than raw mean-variance because the prior (equilibrium) anchors the estimate.

**When to use:** When you have specific quantitative views on some assets but want market-implied returns as the baseline. The no-views case returns equilibrium weights directly.

#### DynamicBlackLitterman

Extends Black-Litterman with time-varying views and adaptive confidence. Confidence is clamped to prevent singular matrices.

**When to use:** Systematic strategies where views update periodically (e.g., momentum signals, macro indicators).

### Risk Parity Family

#### RiskParity

Equalizes each asset's marginal risk contribution via iterative inverse-MRC. The resulting portfolio has each asset contributing equally to total portfolio risk.

**When to use:** Multi-asset allocation where no asset should dominate risk. The standard approach for risk-balanced portfolios. Requires a well-conditioned positive-definite covariance matrix (throws if any MRC is negative).

#### MaximumDiversification

Maximizes the diversification ratio DR(w) = sum(w_i * sigma_i) / sigma_portfolio (Choueifaty and Coignard, 2008). For distinct assets, symmetric positive-definite covariance and feasible uniform bounds in [0,1], a correlation-matrix fast path is followed when necessary by mean-variance subproblems with the bounds applied in original asset coordinates. Every successful supported result has an exact decimal budget of one, respects the authored bounds, and carries a conservative true-DR relative-loss certificate of at most 1e-10. A projected or merely feasible portfolio is not sufficient for success. Unresolved arithmetic, repeated uncertified vectors or exhaustion of 256 outer proposals throws `InvalidOperationException`.

**When to use:** When the goal is to maximize diversification measured by weighted individual volatility relative to portfolio volatility. With equal correlations and nonbinding asset bounds, the optimum is inverse-volatility. Empty/singleton behavior and legacy external-domain routes are retained; those routes do not have the certified optimality guarantee. In particular, legacy relaxation of infeasible bounds does not satisfy authored hard constraints. The constructor's `maxIterations` and `tolerance` retain their meanings for legacy gradient-descent fallback; they do not weaken the supported route's certificate.

#### PrincipalComponentRiskParity

Equalizes risk across statistical factors (principal components) rather than assets. Uses Marcenko-Pastur bound to separate signal from noise eigenvalues. Allocates inverse-risk (1/sqrt(lambda)) to signal PCs, then maps back to asset space.

**When to use:** High-dimensional portfolios where asset-level risk parity is misleading because many assets load on the same factor. Pair with DetonedCovarianceEstimator to prevent PC1 (market) dominance.

### Hierarchical Models

#### HierarchicalRiskParity (HRP)

Lopez de Prado (2016). Three-step algorithm: (1) single-linkage clustering by correlation distance, (2) reorder covariance matrix by dendrogram leaf order, (3) recursive bisection with inverse-variance allocation. Never inverts the covariance matrix.

**When to use:** Default recommendation for portfolios with 10+ assets when return forecasts are unavailable. Robust to estimation error, handles near-singular covariance matrices, and produces intuitive cluster-based allocations.

#### HierarchicalEqualRiskContribution (HERC)

A faithful implementation of Raffinot (2018), *The Hierarchical Equal Risk Contribution Portfolio*. Four steps: (1) single-linkage hierarchical clustering on correlation distance; (2) selection of the optimal number of clusters via the Gap index (Tibshirani et al. 2001); (3) top-down recursive division into two parts **based on the dendrogram shape** (not the number-of-assets midpoint HRP uses), with an **equal-risk-contribution** allocation between the two cluster groups (`a₁ = RC₂/(RC₁+RC₂)`, so the groups contribute equal risk); (4) naive risk parity (inverse-variance) within each cluster. Never inverts the covariance matrix.

**When to use:** When you want HRP's clustering benefits but a more natural, dendrogram-driven split with equal-risk-contribution between clusters and a data-driven cluster count, rather than HRP's fixed number-of-assets bisection.

#### ReturnTiltedHRP

A bespoke return-tilted HRP extension. It blends HRP's inverse-variance allocation with a return signal at each bisection step via softmax: `alpha = (1 - kappa) * alpha_risk + kappa * alpha_return`. The kappa parameter (0 to 1) controls the tilt: kappa=0 recovers pure HRP. Note: this return-softmax-tilt mechanism is the framework's own; it is **not** drawn from Lohre, Rother & Schafer (2020), which de-emphasizes return forecasting and instead contributes lower-tail-dependence-coefficient (LTDC) clustering (not implemented here).

**When to use:** When you want HRP's robustness but have a return signal (momentum, fundamental) worth incorporating. Active in all regimes including bear markets.

### Downside-Risk Models

#### MeanCVaR

Maximizes E[r] - lambda * CVaR(w) via projected gradient ascent with pluggable CVaR risk measure (Rockafellar-Uryasev 2000 reformulation). Only penalizes downside -- naturally tolerates upside volatility.

**When to use:** When tail risk matters more than symmetric volatility. Suitable for portfolios with skewed return distributions.

#### MeanSortino

Same optimization framework as MeanCVaR but with DownsideDeviation as the risk measure. Effectively maximizes the Sortino ratio.

**When to use:** When downside deviation below a minimum acceptable return (MAR) is the relevant risk measure.

### Adaptive Models

#### TacticalOverlay

Wraps any base construction model and applies regime-specific additive tilts plus optional momentum scoring. Re-normalizes weights to sum to 1.0.

**When to use:** When you have a regime classifier and want to tilt allocations toward defensive or growth assets based on the current economic environment.

#### VolatilityTargeting

Scales base model weights by targetVol / realizedVol, capped at maxLeverage. Reduces exposure when volatility spikes, increases it when volatility is low.

**When to use:** When maintaining a stable portfolio volatility is more important than maintaining stable weights.

#### RobustMeanVariance

Minimax optimization: maximizes the worst-case utility across multiple covariance scenarios (e.g., normal + GFC 2008 + rate shock 2022). Uses alternating optimization.

**When to use:** When regime shifts are a concern and you can define plausible covariance scenarios. Falls back to standard mean-variance with a single scenario.

### Constrained Models

#### WeightConstrained

Applies minimum and maximum weight bounds to any inner construction model. Clamps and re-normalizes.

**When to use:** Regulatory or mandate constraints (e.g., no single position above 10%, minimum 2% in each asset).

#### RegimeWeightConstrained

Extends WeightConstrained with regime-dependent bounds. Different constraints apply in different economic regimes.

**When to use:** When you want tighter concentration limits during stress regimes or looser limits during benign periods.

## Decorators

Decorators wrap any base model and can be composed:

### TurnoverPenalized

Applies L1 turnover penalty: minimize ||w - w_target||^2 + lambda * ||w - w_prev||_1. Stateful -- tracks previous weights internally.

**When to use:** When transaction costs are material and you want to trade off optimality for stability.

**As-of / single-date use:** the two-argument `ComputeTargetWeights(assets, returns)` is stateful — it reads and updates the tracked previous weights across a backtest loop. For computing penalized weights at a single as-of date without a backtest (e.g. one call per cohort, each against that cohort's own current weights), use the stateless three-argument overload `ComputeTargetWeights(assets, returns, currentWeights)`. It neither reads nor writes the tracked state, so one instance can serve many independent series without cross-contamination.

### PcaConstrained

Projects N-dimensional returns onto a K-dimensional PC signal subspace before delegating to the inner model. K is determined by Marcenko-Pastur bound. Reduces noise dimensions for more stable weights and lower turnover.

**When to use:** High-dimensional portfolios where the inner model is sensitive to noise (MeanVariance, MinimumVariance). Not compatible with hierarchical models (HRP, HERC).

## Covariance Estimator Pairing

| Model Type | Recommended Estimator | Reason |
|------------|----------------------|--------|
| HRP / HERC / ReturnTiltedHRP | Sample or Ledoit-Wolf | Clustering uses correlation distance, not inverse covariance |
| MinVar / MeanVar / RiskParity | Ledoit-Wolf | Regularizes ill-conditioned matrices |
| PrincipalComponentRP | Detoned | Prevents PC1 market factor from dominating |
| High-dimensional (N > 50) | Denoised | RMT eigenvalue cleaning removes estimation noise |
| Regime-sensitive strategies | EWMA | Gives more weight to recent observations |

## Configuration

All models are selectable via DI configuration in `appsettings.json`:

```json
{
  "Backtest": {
    "ConstructionModel": "RiskParity"
  }
}
```

Valid values: `EqualWeight`, `InverseVolatility`, `MinimumVariance`, `MeanVariance`, `RiskParity`, `MaximumDiversification`, `HierarchicalRiskParity`, `ReturnTiltedHRP`, `MeanCVaR`, `MeanSortino`, `RobustMeanVariance`, `BlackLitterman`.

Decorators (`TurnoverPenalized`, `PcaConstrained`) and models requiring external configuration (`WeightConstrained`, `RegimeWeightConstrained`, `TacticalOverlay`, `VolatilityTargeting`, `DynamicBlackLitterman`) must be composed manually outside DI.

## References

- Choueifaty, Y. and Coignard, Y. (2008). Toward Maximum Diversification. *Journal of Portfolio Management*, 35(1), 40-51.
- DeMiguel, V., Garlappi, L., and Uppal, R. (2009). Optimal versus naive diversification. *Review of Financial Studies*.
- Ledoit, O. and Wolf, M. (2004). Honey, I shrunk the sample covariance matrix. *Journal of Portfolio Management*.
- Lohre, H., Rother, C., and Schafer, K.A. (2020). Hierarchical Risk Parity: Accounting for Tail Dependencies in Multi-asset Multi-factor Allocations. *Machine Learning and Asset Management*.
- Lopez de Prado, M. (2016). Building Diversified Portfolios that Outperform Out of Sample. *Journal of Portfolio Management*.
- Lopez de Prado, M. (2018). *Advances in Financial Machine Learning*. Chapter 2: Denoising.
- Lopez de Prado, M. (2020). *Machine Learning for Asset Managers*. Chapter 2: Detoning.
- Meucci, A. (2009). Managing diversification. *Risk*.
- Raffinot, T. (2018). The Hierarchical Equal Risk Contribution Portfolio. *Journal of Alternative Investments* (SSRN 3237540).
- Rockafellar, R.T. and Uryasev, S. (2000). Optimization of conditional value-at-risk. *Journal of Risk*.
- Tibshirani, R., Walther, G., and Hastie, T. (2001). Estimating the number of clusters in a data set via the gap statistic. *Journal of the Royal Statistical Society, Series B*, 63(2), 411-423.
