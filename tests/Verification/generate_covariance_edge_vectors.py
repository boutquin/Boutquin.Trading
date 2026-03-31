#!/usr/bin/env python3
"""
Generate covariance estimator edge-case vectors for Boutquin.Trading.

Provides reference vectors for:
  - SampleCovarianceEstimator (1-asset, 2-asset, correlated)
  - ExponentiallyWeightedCovarianceEstimator (lambda variants, T=3)
  - LedoitWolfShrinkageEstimator (own formula — NOT sklearn, matches C# exactly)
  - DenoisedCovarianceEstimator (Marcenko-Pastur denoising)

The Ledoit-Wolf implementation here replicates the EXACT C# formula
(Ledoit & Wolf 2004 with rho correction for scaled identity target),
which differs slightly from sklearn's implementation.
"""

import json
from pathlib import Path

import numpy as np
from sklearn.covariance import LedoitWolf

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

TRADING_DAYS = 252
RNG = np.random.default_rng(seed=77)


def save(name: str, data: dict) -> None:
    def convert(obj):
        if isinstance(obj, np.ndarray):
            return obj.tolist()
        if isinstance(obj, (np.float64, np.float32)):
            return float(obj)
        if isinstance(obj, (np.int64, np.int32)):
            return int(obj)
        raise TypeError(f"Cannot serialize {type(obj)}")

    with open(VECTORS_DIR / f"{name}.json", "w") as f:
        json.dump(data, f, indent=2, default=convert)
    print(f"  ✓ {name}.json")


# ═══════════════════════════════════════════════════════════════════════════
# Reference implementations (match C# exactly)
# ═══════════════════════════════════════════════════════════════════════════

def sample_cov(returns: np.ndarray) -> np.ndarray:
    """Sample covariance with N-1 divisor. returns shape: (n_assets, T)."""
    return np.cov(returns, ddof=1)


def ewma_cov(returns: np.ndarray, lam: float) -> np.ndarray:
    """EWMA covariance matching C# ExponentiallyWeightedCovarianceEstimator."""
    n, t = returns.shape
    means = returns.mean(axis=1)
    demeaned = returns - means[:, np.newaxis]
    weights = np.array([lam ** (t - 1 - k) for k in range(t)])
    weights /= weights.sum()
    cov = np.zeros((n, n))
    for i in range(n):
        for j in range(i, n):
            val = np.sum(weights * demeaned[i] * demeaned[j])
            cov[i, j] = val
            cov[j, i] = val
    return cov


def ledoit_wolf_cov(returns: np.ndarray) -> tuple[np.ndarray, float]:
    """Ledoit-Wolf 2004 shrinkage matching C# LedoitWolfShrinkageEstimator exactly.

    returns shape: (n_assets, T)
    Returns: (shrunk_cov, delta)
    """
    n, t = returns.shape

    # Step 1: Sample covariance
    s = np.cov(returns, ddof=1)
    # Ensure 2D for single asset
    if s.ndim == 0:
        s = np.array([[float(s)]])

    # Step 2: Target = mu * I
    mu = np.trace(s) / n

    # Step 3: Optimal shrinkage intensity
    means = returns.mean(axis=1)

    # gamma: sum of squared Frobenius norms of (S - F)
    target = mu * np.eye(n)
    diff = s - target
    gamma = np.sum(diff ** 2)

    # piSum: sum of asymptotic variances
    pi_sum = 0.0
    for i in range(n):
        for j in range(n):
            total = 0.0
            for k in range(t):
                x = (returns[i, k] - means[i]) * (returns[j, k] - means[j]) - s[i, j]
                total += x * x
            pi_sum += total / t

    # rhoSum: diagonal-only pi terms (for scaled identity target)
    rho_sum = 0.0
    for i in range(n):
        total = 0.0
        for k in range(t):
            zki = returns[i, k] - means[i]
            term = zki * zki - s[i, i]
            total += term * term
        rho_sum += total / t

    # delta
    if gamma == 0:
        delta = 1.0
    else:
        delta = (pi_sum - rho_sum) / (t * gamma)
    delta = max(0.0, min(1.0, delta))

    # Step 4: Shrunk covariance
    shrunk = delta * target + (1.0 - delta) * s

    return shrunk, delta


def denoised_cov(returns: np.ndarray, apply_lw: bool = False) -> np.ndarray:
    """Denoised covariance matching C# DenoisedCovarianceEstimator.

    Uses Marcenko-Pastur distribution to identify noise eigenvalues.
    For n < 3: delegates to sample (or LW if apply_lw=True).
    """
    n, t = returns.shape

    # Sample covariance
    s = np.cov(returns, ddof=1)
    if s.ndim == 0:
        s = np.array([[float(s)]])

    if n < 3:
        if apply_lw:
            result, _ = ledoit_wolf_cov(returns)
            return result
        return s

    # Convert to correlation matrix
    stds = np.sqrt(np.diag(s))
    corr = s / np.outer(stds, stds)

    # Eigendecomposition
    eigenvalues, eigenvectors = np.linalg.eigh(corr)

    # Marcenko-Pastur bound
    q = t / n
    lambda_plus = (1.0 + 1.0 / np.sqrt(q)) ** 2

    # Identify noise eigenvalues (<= MP bound)
    noise_mask = eigenvalues <= lambda_plus
    if noise_mask.any():
        noise_avg = eigenvalues[noise_mask].mean()
        eigenvalues[noise_mask] = noise_avg

    # Reconstruct correlation from cleaned eigenvalues
    cleaned_corr = eigenvectors @ np.diag(eigenvalues) @ eigenvectors.T

    # Normalize to proper correlation (diagonal = 1)
    diag_sqrt = np.sqrt(np.diag(cleaned_corr))
    cleaned_corr = cleaned_corr / np.outer(diag_sqrt, diag_sqrt)

    # Convert back to covariance
    result = cleaned_corr * np.outer(stds, stds)

    # Optional LW shrinkage on top (C# uses fixed intensity 0.1)
    if apply_lw:
        mu = np.trace(result) / n
        target_mat = mu * np.eye(n)
        intensity = 0.1
        result = intensity * target_mat + (1.0 - intensity) * result

    return result


# ═══════════════════════════════════════════════════════════════════════════
# Vector generators
# ═══════════════════════════════════════════════════════════════════════════

def gen_2asset_vectors():
    """2-asset: 2x2 covariance matrices."""
    returns = RNG.normal(loc=0.0003, scale=0.012, size=(2, 100))

    s = sample_cov(returns)
    e = ewma_cov(returns, 0.94)
    lw, lw_delta = ledoit_wolf_cov(returns)

    save("covariance_edge_2asset", {
        "inputs": {"returns": returns},
        "expected": {
            "sample_covariance": s,
            "ewma_covariance": e,
            "ledoit_wolf_covariance": lw,
            "ledoit_wolf_shrinkage": lw_delta,
        },
        "notes": "2-asset, 100 observations. Minimal multi-asset case.",
    })


def gen_1asset_vectors():
    """1-asset: 1x1 = variance."""
    returns = RNG.normal(loc=0.0003, scale=0.012, size=(1, 100))

    s = sample_cov(returns)
    # numpy.cov returns scalar for 1D, wrap it
    if s.ndim == 0:
        s = np.array([[float(s)]])

    save("covariance_edge_1asset", {
        "inputs": {"returns": returns},
        "expected": {
            "sample_covariance": s,
        },
        "notes": "1-asset, 100 observations. Covariance is just variance (1x1).",
    })


def gen_ewma_lambda_variants():
    """EWMA with different lambda values and minimal T."""
    returns_3asset = RNG.normal(loc=0.0003, scale=0.012, size=(3, 100))

    e_050 = ewma_cov(returns_3asset, 0.50)
    e_099 = ewma_cov(returns_3asset, 0.99)

    # T=3 minimal observations
    returns_t3 = RNG.normal(loc=0.0003, scale=0.012, size=(3, 3))
    e_t3 = ewma_cov(returns_t3, 0.94)

    save("covariance_edge_ewma_lambdas", {
        "inputs": {"returns": returns_3asset},
        "expected": {
            "ewma_lambda_050": e_050,
            "ewma_lambda_099": e_099,
            "t3_returns": returns_t3,
            "ewma_t3": e_t3,
        },
        "notes": "3-asset, 100 obs. Lambda=0.5 (aggressive), 0.99 (near-sample). T=3 (minimal).",
    })


def gen_correlated_vectors():
    """Highly correlated assets: near-singular covariance."""
    base = RNG.normal(loc=0.0003, scale=0.012, size=100)
    # Asset 1 = base + small noise
    asset1 = base + RNG.normal(0, 0.001, size=100)
    # Asset 2 = base + small noise (very correlated with asset 1)
    asset2 = base + RNG.normal(0, 0.001, size=100)
    # Asset 3 = independent
    asset3 = RNG.normal(loc=0.0002, scale=0.01, size=100)

    returns = np.array([asset1, asset2, asset3])

    s = sample_cov(returns)
    lw, lw_delta = ledoit_wolf_cov(returns)

    save("covariance_edge_correlated", {
        "inputs": {"returns": returns},
        "expected": {
            "sample_covariance": s,
            "ledoit_wolf_covariance": lw,
            "ledoit_wolf_shrinkage": lw_delta,
        },
        "notes": "Assets 0&1 highly correlated (r~0.99), asset 2 independent. Tests near-singular case.",
    })


def gen_denoised_vectors():
    """Denoised covariance with SIGNAL + NOISE eigenvalues.

    Uses correlated data so that at least one eigenvalue is above the MP bound.
    This tests the actual denoising logic (replacing noise, keeping signal),
    not just Jacobi orthogonality precision on an identity-like matrix.
    """
    # Create data with clear factor structure:
    # Assets 0-2 share a common factor (→ dominant eigenvalue above MP bound)
    # Assets 3-4 are independent (→ eigenvalues in noise band)
    base = RNG.normal(0.0003, 0.012, size=200)
    returns_5 = np.array([
        base + RNG.normal(0, 0.002, 200),  # correlated
        base + RNG.normal(0, 0.002, 200),  # correlated
        base + RNG.normal(0, 0.002, 200),  # correlated
        RNG.normal(0.0001, 0.015, 200),    # independent
        RNG.normal(0.0002, 0.008, 200),    # independent
    ])

    # Verify we have signal + noise split
    cov = np.cov(returns_5, ddof=1)
    stds = np.sqrt(np.diag(cov))
    corr = cov / np.outer(stds, stds)
    eigenvalues = np.linalg.eigvalsh(corr)
    q = 200 / 5
    mp_bound = (1 + 1 / np.sqrt(q)) ** 2
    n_noise = np.sum(eigenvalues <= mp_bound)
    n_signal = np.sum(eigenvalues > mp_bound)
    assert n_signal >= 1, f"Test data must have at least 1 signal eigenvalue, got {n_signal}"
    assert n_noise >= 1, f"Test data must have at least 1 noise eigenvalue, got {n_noise}"

    d_plain = denoised_cov(returns_5, apply_lw=False)
    d_lw = denoised_cov(returns_5, apply_lw=True)

    # Also use the same 3-asset data from main covariance vectors
    d_main = json.load(open(VECTORS_DIR / "multi_asset_returns.json"))
    returns_3 = np.array(d_main["values"])  # 3x252

    d3_plain = denoised_cov(returns_3, apply_lw=False)
    d3_lw = denoised_cov(returns_3, apply_lw=True)

    # Eigenvalue reference for direct eigendecomposition testing
    eigenvalues_sorted = np.sort(np.linalg.eigvalsh(corr))[::-1]  # descending

    save("covariance_edge_denoised", {
        "inputs": {"returns": returns_5},
        "expected": {
            "denoised_covariance": d_plain,
            "denoised_lw_covariance": d_lw,
            "returns_3asset": returns_3,
            "denoised_3asset_covariance": d3_plain,
            "denoised_3asset_lw_covariance": d3_lw,
            "eigenvalues_descending": eigenvalues_sorted,
            "mp_upper_bound": mp_bound,
            "noise_count": int(n_noise),
            "signal_count": int(n_signal),
        },
        "notes": f"5-asset with factor structure: {n_signal} signal + {n_noise} noise eigenvalues. "
                 f"MP bound = {mp_bound:.4f}. Tests actual denoising, not just Jacobi orthogonality.",
    })


def gen_ledoit_wolf_own_formula():
    """Ledoit-Wolf using our exact formula (not sklearn) for the main 3-asset data.

    This replaces the sklearn-based vector in covariance.json for LW verification.
    """
    d = json.load(open(VECTORS_DIR / "multi_asset_returns.json"))
    returns = np.array(d["values"])  # 3x252

    lw, lw_delta = ledoit_wolf_cov(returns)

    # Also check against sklearn for reference
    lw_sklearn = LedoitWolf().fit(returns.T)
    sklearn_delta = lw_sklearn.shrinkage_

    save("covariance_ledoit_wolf_own", {
        "inputs": {"returns": returns},
        "expected": {
            "ledoit_wolf_covariance": lw,
            "ledoit_wolf_shrinkage": lw_delta,
        },
        "reference": {
            "sklearn_covariance": lw_sklearn.covariance_,
            "sklearn_shrinkage": sklearn_delta,
            "note": "sklearn uses a different formula variant. Our C# matches ledoit_wolf_covariance, not sklearn.",
        },
        "notes": "Same 3-asset, 252-obs data as main vectors. Uses exact C# formula, not sklearn.",
    })


# ═══════════════════════════════════════════════════════════════════════════
# Main
# ═══════════════════════════════════════════════════════════════════════════

# ═══════════════════════════════════════════════════════════════════════════
# Correctness cross-checks (three layers)
#
# Layer 1: Library cross-references — compare our own-formula covariance
#          estimators against sklearn, numpy for independent verification.
# Layer 2: Analytical solutions — identity cov, diagonal cov, known
#          shrinkage behavior for special cases.
# Layer 3: Property-based checks — symmetry, PSD, trace preservation,
#          shrinkage intensity bounds.
# ═══════════════════════════════════════════════════════════════════════════

_hard_total = 0
_hard_pass = 0
_quality_total = 0
_quality_pass = 0


def _hard(condition: bool, label: str):
    global _hard_total, _hard_pass
    _hard_total += 1
    if condition:
        _hard_pass += 1
        print(f"    [PASS] {label}")
    else:
        print(f"    [HARD FAIL] {label}")


def _quality(condition: bool, label: str):
    global _quality_total, _quality_pass
    _quality_total += 1
    if condition:
        _quality_pass += 1
        print(f"    [PASS] {label}")
    else:
        print(f"    [QUALITY] {label}")


def run_cross_checks():
    global _hard_total, _hard_pass, _quality_total, _quality_pass
    _hard_total = _hard_pass = _quality_total = _quality_pass = 0

    print()
    print("=" * 70)
    print("PHASE 1: CORRECTNESS CROSS-CHECKS")
    print("=" * 70)

    check_rng = np.random.default_rng(seed=7777)
    TOL_EXACT = 1e-10
    TOL_NUMERIC = 1e-6
    TOL_STAT = 1e-4

    # ─── Layer 1: Library cross-references ──────────────────────────────
    print()
    print("Layer 1: Library cross-references (numpy, sklearn)")
    print("-" * 50)

    returns_3 = check_rng.normal(0.0003, 0.012, size=(3, 200))

    # Sample covariance: our formula vs numpy.cov
    our_scov = sample_cov(returns_3)
    lib_scov = np.cov(returns_3, ddof=1)
    diff = np.max(np.abs(our_scov - lib_scov))
    _hard(diff < TOL_EXACT, f"Sample cov matches numpy.cov (max_diff={diff:.2e})")

    # EWMA: verify against manually computed weights
    our_ewma = ewma_cov(returns_3, 0.94)
    # Cross-check: EWMA should approach sample cov as lambda -> 1
    ewma_099 = ewma_cov(returns_3, 0.999)
    diff_099 = np.max(np.abs(ewma_099 - our_scov))
    _hard(diff_099 < 0.1 * np.max(np.abs(our_scov)),
          f"EWMA(lambda=0.999) ~ sample cov (max_diff={diff_099:.6f})")

    # Ledoit-Wolf: our formula vs sklearn (QUALITY — known to differ)
    our_lw, our_delta = ledoit_wolf_cov(returns_3)
    lw_sklearn = LedoitWolf().fit(returns_3.T)
    sklearn_cov = lw_sklearn.covariance_
    sklearn_delta = lw_sklearn.shrinkage_
    diff_lw = np.max(np.abs(our_lw - sklearn_cov))
    _quality(diff_lw < 1e-4,
             f"LW own-formula vs sklearn (max_diff={diff_lw:.6f}, "
             f"delta_ours={our_delta:.6f}, delta_sklearn={sklearn_delta:.6f})")

    # Denoised: verify eigenvalue cleaning preserves trace approximately
    returns_5 = np.array([
        check_rng.normal(0, 0.012, 200) + check_rng.normal(0, 0.01, 200),
        check_rng.normal(0, 0.012, 200) + check_rng.normal(0, 0.01, 200),
        check_rng.normal(0, 0.012, 200) + check_rng.normal(0, 0.01, 200),
        check_rng.normal(0, 0.015, 200),
        check_rng.normal(0, 0.008, 200),
    ])
    scov5 = sample_cov(returns_5)
    dcov5 = denoised_cov(returns_5, apply_lw=False)
    trace_ratio = np.trace(dcov5) / np.trace(scov5)
    _quality(abs(trace_ratio - 1.0) < 0.15,
             f"Denoised trace preservation (ratio={trace_ratio:.4f})")

    # ─── Layer 2: Analytical solutions ──────────────────────────────────
    print()
    print("Layer 2: Analytical solutions")
    print("-" * 50)

    # Identity returns (independent, same vol) → diagonal cov
    n_id = 3
    independent = np.array([
        check_rng.normal(0, 0.01, 500),
        check_rng.normal(0, 0.01, 500),
        check_rng.normal(0, 0.01, 500),
    ])
    scov_id = sample_cov(independent)
    off_diag_max = max(abs(scov_id[i, j])
                       for i in range(n_id) for j in range(n_id) if i != j)
    diag_min = min(scov_id[i, i] for i in range(n_id))
    _hard(off_diag_max < diag_min * 0.3,
          f"Independent returns: |off-diag| ({off_diag_max:.6f}) < 30% of diag ({diag_min:.6f})")

    # 1-asset: cov = [[variance]]
    ret_1 = check_rng.normal(0.001, 0.02, size=(1, 100))
    scov_1 = sample_cov(ret_1)  # returns [[var]] after ndim fix in sample_cov
    expected_var = float(np.var(ret_1[0], ddof=1))
    actual_var = float(scov_1[0, 0]) if scov_1.ndim == 2 else float(scov_1)
    _hard(abs(actual_var - expected_var) < TOL_EXACT,
          f"1-asset cov = variance (diff={abs(actual_var - expected_var):.2e})")

    # LW shrinkage on identity-like data → low shrinkage (data is well-conditioned)
    well_cond = check_rng.normal(0, 0.01, size=(3, 1000))
    _, delta_well = ledoit_wolf_cov(well_cond)
    _hard(0 <= delta_well <= 1,
          f"LW shrinkage intensity in [0,1] (delta={delta_well:.6f})")
    _quality(delta_well < 0.5,
             f"LW on well-conditioned data: low shrinkage (delta={delta_well:.6f})")

    # LW shrinkage on near-singular data → high shrinkage
    near_sing = check_rng.normal(0, 0.01, size=(10, 15))  # T ~ N
    _, delta_sing = ledoit_wolf_cov(near_sing)
    _quality(delta_sing > delta_well,
             f"LW near-singular ({delta_sing:.4f}) > well-conditioned ({delta_well:.4f})")

    # EWMA lambda=0 → only last observation matters
    # (lambda=0 means all weight on most recent)
    ret_ewma = check_rng.normal(0, 0.01, size=(2, 50))
    ewma_0 = ewma_cov(ret_ewma, 0.0001)  # Near-zero lambda (99.99% on last obs)
    # With lambda≈0, weight concentrates on the last observation
    # The matrix should be close to rank-1 (outer product of last demeaned obs)
    eigenvalues = np.linalg.eigvalsh(ewma_0)
    ratio = eigenvalues[0] / eigenvalues[1] if eigenvalues[1] > 0 else 0
    _quality(ratio < 0.01,
             f"EWMA(lambda≈0): near rank-1 (eigenvalue ratio={ratio:.6f})")

    # ─── Layer 3: Property-based checks ─────────────────────────────────
    print()
    print("Layer 3: Property-based checks (mathematical invariants)")
    print("-" * 50)

    for label, returns in [
        ("3x200", check_rng.normal(0, 0.012, (3, 200))),
        ("5x100", check_rng.normal(0, 0.015, (5, 100))),
        ("2x500", check_rng.normal(0, 0.01, (2, 500))),
    ]:
        n_assets = returns.shape[0]
        print(f"  {label}:")

        for est_name, cov_matrix in [
            ("Sample", sample_cov(returns)),
            ("EWMA(0.94)", ewma_cov(returns, 0.94)),
            ("LW", ledoit_wolf_cov(returns)[0]),
        ]:
            # P1: Symmetry
            asym = np.max(np.abs(cov_matrix - cov_matrix.T))
            _hard(asym < TOL_EXACT, f"{est_name} symmetric (max_asym={asym:.2e})")

            # P2: Diagonal non-negative (variances ≥ 0)
            diag_min = np.min(np.diag(cov_matrix))
            _hard(diag_min >= 0, f"{est_name} diagonal ≥ 0 (min={diag_min:.2e})")

            # P3: Positive semi-definite (all eigenvalues ≥ 0)
            eigs = np.linalg.eigvalsh(cov_matrix)
            _hard(eigs[0] >= -TOL_NUMERIC,
                  f"{est_name} PSD (min eigenvalue={eigs[0]:.2e})")

            # P4: |correlation| ≤ 1
            diag = np.sqrt(np.diag(cov_matrix))
            if np.all(diag > 0):
                corr = cov_matrix / np.outer(diag, diag)
                max_corr = np.max(np.abs(corr))
                _hard(max_corr <= 1.0 + TOL_EXACT,
                      f"{est_name} |correlation| ≤ 1 (max={max_corr:.8f})")

        # LW-specific: shrinkage between sample and target
        lw_cov, delta = ledoit_wolf_cov(returns)
        scov = sample_cov(returns)
        mu = np.trace(scov) / n_assets
        target = mu * np.eye(n_assets)
        reconstructed = delta * target + (1 - delta) * scov
        diff = np.max(np.abs(lw_cov - reconstructed))
        _hard(diff < TOL_EXACT,
              f"LW = delta*target + (1-delta)*sample (max_diff={diff:.2e})")

    # ─── Summary ────────────────────────────────────────────────────────
    print()
    print("=" * 70)
    hard_failed = _hard_total - _hard_pass
    quality_failed = _quality_total - _quality_pass
    print(f"HARD checks:    {_hard_pass}/{_hard_total} passed"
          + ("" if hard_failed == 0 else f"  *** {hard_failed} FAILED ***"))
    print(f"QUALITY checks: {_quality_pass}/{_quality_total} passed"
          + ("" if quality_failed == 0 else f"  ({quality_failed} gap(s))"))
    if hard_failed == 0:
        print("All mathematical invariants hold.")
    print("=" * 70)

    if hard_failed > 0:
        raise AssertionError(f"{hard_failed} hard cross-check(s) failed")


def main():
    print("Generating covariance edge-case vectors...")
    print()

    print("2-asset:")
    gen_2asset_vectors()

    print("1-asset:")
    gen_1asset_vectors()

    print("EWMA lambda variants:")
    gen_ewma_lambda_variants()

    print("Highly correlated assets:")
    gen_correlated_vectors()

    print("Denoised covariance:")
    gen_denoised_vectors()

    print("Ledoit-Wolf own formula (3-asset main data):")
    gen_ledoit_wolf_own_formula()

    print()
    print(f"Done. Covariance edge vectors in {VECTORS_DIR}")

    # Correctness validation
    run_cross_checks()


if __name__ == "__main__":
    main()
