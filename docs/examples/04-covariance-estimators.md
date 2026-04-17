# Example 04 — Covariance Estimator Selection

Boutquin.Trading ships 13 covariance estimators across four tiers. This example shows how to choose and swap estimators, and explains when each tier applies.

## All Estimators Implement `ICovarianceEstimator`

```csharp
// Any estimator can be dropped into any construction model that accepts ICovarianceEstimator.
ICovarianceEstimator estimator = new LedoitWolfConstantCorrelationEstimator();
var model = new HierarchicalRiskParityConstruction(estimator);
```

## Estimator Quick Reference

### Classical

```csharp
// Sample covariance — unbiased but high variance on short histories
var sample = new SampleCovarianceEstimator();

// EWMA — more weight on recent observations; lambda=0.94 is RiskMetrics standard
var ewma = new ExponentiallyWeightedCovarianceEstimator(lambda: 0.94m);
```

### Linear Shrinkage

```csharp
// Ledoit-Wolf toward scaled identity (2004 formula, rho correction included)
var lwIdentity = new LedoitWolfShrinkageEstimator();

// Ledoit-Wolf toward average-correlation target — recommended default for equities
var lwCorr = new LedoitWolfConstantCorrelationEstimator();

// Ledoit-Wolf toward single-factor (market) target
var lwFactor = new LedoitWolfSingleFactorEstimator();

// Oracle Approximating Shrinkage (Chen et al. 2010)
var oas = new OracleApproximatingShrinkageEstimator();
```

### Nonlinear / Denoising

```csharp
// Quadratic Inverse Shrinkage — per-eigenvalue shrinkage; gold standard for N >= 10
var qis = new QuadraticInverseShrinkageEstimator();

// Random Matrix Theory denoising (Marcenko-Pastur)
var denoised = new DenoisedCovarianceEstimator();

// Tracy-Widom threshold — sharper than Marcenko-Pastur when T/N < 5
var tw = new TracyWidomDenoisedCovarianceEstimator();

// Detoned — removes PC1 (market factor) to expose diversification structure
// Recommended when pairing with PrincipalComponentRiskParityConstruction
var detoned = new DetonedCovarianceEstimator(detoningAlpha: 1.0m);
```

### Factor / Sparse / Nonparametric

```csharp
// POET — low-rank + sparse residual; ideal for HRP/PCRP
var poet = new PoetCovarianceEstimator();

// NERCOME — split-sample, no distributional assumption
var nercome = new NercomeCovarianceEstimator();

// Doubly sparse — sparsifies eigenvectors and noise eigenvalues
var doublySparse = new DoublySparseEstimator();
```

## Selection Guide

| Scenario | Recommended estimator |
|----------|----------------------|
| N < 10, long history (T >> N) | `SampleCovarianceEstimator` |
| N 10–50, moderate history | `LedoitWolfConstantCorrelationEstimator` |
| N 50–200, limited history | `QuadraticInverseShrinkageEstimator` |
| Regime-sensitive (recent data matters) | `ExponentiallyWeightedCovarianceEstimator` (lambda=0.94) |
| Near-singular matrix (T/N < 2) | `TracyWidomDenoisedCovarianceEstimator` |
| HRP / PCRP (no matrix inversion) | `DenoisedCovarianceEstimator` or `PoetCovarianceEstimator` |
| PrincipalComponentRiskParity | `DetonedCovarianceEstimator` (suppresses PC1 dominance) |

## PSD Guarantees

| Estimator | PSD guaranteed? |
|-----------|-----------------|
| Sample | Yes (if T ≥ N) |
| EWMA | Yes |
| LedoitWolfShrinkage, ConstantCorrelation, SingleFactor | Yes |
| OAS | Yes |
| QIS | Yes |
| Denoised, TracyWidomDenoised, Detoned | Yes |
| POET | No at high threshold — wrap with `NearestPsdProjection.EigenClip` if needed |
| NERCOME | Yes |
| DoublySparse | No — wrap with `NearestPsdProjection.EigenClip` if needed |

## Comparing Estimators

```csharp
var estimators = new (string Name, ICovarianceEstimator Estimator)[]
{
    ("Sample",   new SampleCovarianceEstimator()),
    ("LW-CC",    new LedoitWolfConstantCorrelationEstimator()),
    ("QIS",      new QuadraticInverseShrinkageEstimator()),
    ("Denoised", new DenoisedCovarianceEstimator()),
};

foreach (var (name, estimator) in estimators)
{
    var model = new MinimumVarianceConstruction(estimator);
    // ... run backtest, collect TearSheet ...
    Console.WriteLine($"{name}: Sharpe={sharpe:F2}, MaxDD={maxDrawdown:P1}");
}
```
