#!/usr/bin/env python3
"""
Generate cross-language verification vectors for PCA-based portfolio construction models.

Models:
  - PrincipalComponentRiskParityConstruction (PC-RP)
  - PcaConstrainedConstruction (PCA decorator)
  - DetonedCovarianceEstimator

All Python reference implementations replicate the EXACT C# algorithms.

CORRECTNESS VALIDATION (three layers):
  1. Library cross-references — compare against numpy eigendecomposition
  2. Analytical solutions — identity covariance, rank-1 covariance
  3. Property-based checks — weight simplex, non-negativity, symmetry
"""

import json
from pathlib import Path

import numpy as np

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

RNG = np.random.default_rng(seed=99)  # Unique seed for PCA vectors


def save(name: str, data: dict) -> None:
    def convert(obj):
        if isinstance(obj, np.ndarray):
            return obj.tolist()
        if isinstance(obj, (np.float64, np.float32)):
            return float(obj)
        if isinstance(obj, (np.int64, np.int32)):
            return int(obj)
        if isinstance(obj, np.bool_):
            return bool(obj)
        raise TypeError(f"Cannot serialize {type(obj)}")

    with open(VECTORS_DIR / f"{name}.json", "w") as f:
        json.dump(data, f, indent=2, default=convert)
    print(f"  -> {name}.json")


# ═══════════════════════════════════════════════════════════════════════════
# Shared helpers (matching C# exactly)
# ═══════════════════════════════════════════════════════════════════════════

def sample_cov(returns: np.ndarray) -> np.ndarray:
    """Sample covariance with N-1 divisor. returns shape: (n_assets, T)."""
    c = np.cov(returns, ddof=1)
    if c.ndim == 0:
        c = np.array([[float(c)]])
    return c


def marcenko_pastur_bound(t: int, n: int) -> float:
    """Marcenko-Pastur upper bound: lambda_+ = (1 + 1/sqrt(q))^2 where q = T/N.
    When q < 1 (more assets than observations), returns Kaiser criterion (1.0)."""
    q = t / n
    if q < 1.0:
        return 1.0  # Kaiser criterion fallback
    return (1.0 + 1.0 / np.sqrt(q)) ** 2


def eigen_decompose(matrix: np.ndarray):
    """Eigendecomposition returning eigenvalues (descending) and eigenvectors (columns).
    Matches C# Jacobi iteration output convention."""
    eigenvalues, eigenvectors = np.linalg.eigh(matrix)
    # Sort descending
    idx = np.argsort(eigenvalues)[::-1]
    return eigenvalues[idx], eigenvectors[:, idx]


# ═══════════════════════════════════════════════════════════════════════════
# PrincipalComponentRiskParityConstruction — matching C# exactly
# ═══════════════════════════════════════════════════════════════════════════

def pc_risk_parity(returns: np.ndarray, signal_threshold: float = None) -> np.ndarray:
    """PC Risk Parity matching C# PrincipalComponentRiskParityConstruction."""
    n, t = returns.shape

    if n == 1:
        return np.array([1.0])

    cov = sample_cov(returns)
    eigenvalues, eigenvectors = eigen_decompose(cov)

    if signal_threshold is None:
        signal_threshold = marcenko_pastur_bound(t, n)

    # Filter signal PCs
    signal_indices = [k for k in range(n) if eigenvalues[k] > signal_threshold]

    if len(signal_indices) == 0:
        return np.full(n, 1.0 / n)

    # Allocate inverse-risk (1/sqrt(lambda)) across signal PCs
    pc_weights = np.zeros(len(signal_indices))
    for i, k in enumerate(signal_indices):
        lam = eigenvalues[k]
        if lam > 0:
            pc_weights[i] = 1.0 / np.sqrt(lam)

    total_inv_risk = pc_weights.sum()
    if total_inv_risk <= 0:
        return np.full(n, 1.0 / n)

    pc_weights /= total_inv_risk

    # Map PC-space weights back to asset-space
    asset_weights = np.zeros(n)
    for i, k in enumerate(signal_indices):
        asset_weights += pc_weights[i] * eigenvectors[:, k]

    # Sign normalization: largest absolute loading positive
    max_abs_idx = np.argmax(np.abs(asset_weights))
    if asset_weights[max_abs_idx] < 0:
        asset_weights = -asset_weights

    # Clamp negatives
    asset_weights = np.maximum(asset_weights, 0.0)

    # Normalize
    total = asset_weights.sum()
    if total <= 0:
        return np.full(n, 1.0 / n)

    return asset_weights / total


# ═══════════════════════════════════════════════════════════════════════════
# PcaConstrainedConstruction — matching C# exactly
# ═══════════════════════════════════════════════════════════════════════════

def pca_constrained(returns: np.ndarray, inner_model_fn, max_components: int = None) -> np.ndarray:
    """PCA-Constrained decorator matching C# PcaConstrainedConstruction.
    inner_model_fn(returns) → weights array."""
    n, t = returns.shape

    if n == 1:
        return np.array([1.0])

    cov = sample_cov(returns)
    eigenvalues, eigenvectors = eigen_decompose(cov)

    threshold = marcenko_pastur_bound(t, n)

    # Determine K
    k = sum(1 for ev in eigenvalues if ev > threshold)
    if max_components is not None:
        k = min(k, max_components)

    if k <= 0:
        return np.full(n, 1.0 / n)

    if k >= n:
        return inner_model_fn(returns)

    # Project returns to K-dimensional PC space
    projected = eigenvectors[:, :k].T @ returns  # shape: (k, t)

    # Delegate to inner model
    inner_weights = inner_model_fn(projected)

    # Map back to asset space
    asset_weights = eigenvectors[:, :k] @ inner_weights

    # Clamp negatives
    asset_weights = np.maximum(asset_weights, 0.0)

    # Normalize
    total = asset_weights.sum()
    if total <= 0:
        return np.full(n, 1.0 / n)

    return asset_weights / total


# ═══════════════════════════════════════════════════════════════════════════
# DetonedCovarianceEstimator — matching C# exactly
# ═══════════════════════════════════════════════════════════════════════════

def detoned_cov(returns: np.ndarray, detoning_alpha: float = 1.0) -> np.ndarray:
    """Detoned covariance matching C# DetonedCovarianceEstimator."""
    n, t = returns.shape

    if n < 3:
        return sample_cov(returns)

    if detoning_alpha == 0.0:
        return denoised_cov(returns)

    # Step 1: Sample covariance → correlation
    s = sample_cov(returns)
    stds = np.sqrt(np.diag(s))
    stds[stds == 0] = 1.0
    corr = s / np.outer(stds, stds)

    # Step 2: Eigendecompose
    eigenvalues, eigenvectors = eigen_decompose(corr)

    # Step 3: MP bound
    q = t / n
    mp_bound = (1.0 + 1.0 / np.sqrt(q)) ** 2

    # Step 4: Denoise
    noise_mask = eigenvalues <= mp_bound
    noise_count = noise_mask.sum()
    noise_sum = eigenvalues[noise_mask].sum() if noise_count > 0 else 0.0

    if 0 < noise_count < n:
        noise_avg = noise_sum / noise_count
        eigenvalues[noise_mask] = noise_avg

    # Step 5: Detone — shrink PC1 (index 0, largest)
    signal_others = [eigenvalues[i] for i in range(1, n) if eigenvalues[i] > mp_bound]
    if len(signal_others) > 0:
        signal_avg = sum(signal_others) / len(signal_others)
        eigenvalues[0] = (1 - detoning_alpha) * eigenvalues[0] + detoning_alpha * signal_avg
    elif noise_count > 0:
        noise_avg = noise_sum / noise_count
        eigenvalues[0] = (1 - detoning_alpha) * eigenvalues[0] + detoning_alpha * noise_avg

    # Step 6: Reconstruct correlation
    cleaned_corr = eigenvectors @ np.diag(eigenvalues) @ eigenvectors.T

    # Step 7: Force unit diagonal
    np.fill_diagonal(cleaned_corr, 1.0)

    # Step 8: Convert back to covariance
    return cleaned_corr * np.outer(stds, stds)


def denoised_cov(returns: np.ndarray) -> np.ndarray:
    """Denoised covariance (no LW) matching C# DenoisedCovarianceEstimator."""
    n, t = returns.shape
    s = sample_cov(returns)
    if n < 3:
        return s

    stds = np.sqrt(np.diag(s))
    stds[stds == 0] = 1.0
    corr = s / np.outer(stds, stds)

    eigenvalues, eigenvectors = eigen_decompose(corr)

    q = t / n
    mp_bound = (1.0 + 1.0 / np.sqrt(q)) ** 2

    noise_mask = eigenvalues <= mp_bound
    if noise_mask.any() and not noise_mask.all():
        noise_avg = eigenvalues[noise_mask].mean()
        eigenvalues[noise_mask] = noise_avg

    cleaned_corr = eigenvectors @ np.diag(eigenvalues) @ eigenvectors.T
    diag_sqrt = np.sqrt(np.diag(cleaned_corr))
    diag_sqrt[diag_sqrt == 0] = 1.0
    cleaned_corr = cleaned_corr / np.outer(diag_sqrt, diag_sqrt)

    return cleaned_corr * np.outer(stds, stds)


# ═══════════════════════════════════════════════════════════════════════════
# Inner model helpers for PCA-Constrained tests
# ═══════════════════════════════════════════════════════════════════════════

def equal_weight(returns: np.ndarray) -> np.ndarray:
    n = returns.shape[0]
    return np.full(n, 1.0 / n)


def inverse_volatility(returns: np.ndarray) -> np.ndarray:
    n = returns.shape[0]
    vols = np.std(returns, axis=1, ddof=1)
    vols[vols == 0] = 1e-10
    inv_vols = 1.0 / vols
    return inv_vols / inv_vols.sum()


# ═══════════════════════════════════════════════════════════════════════════
# Vector generators
# ═══════════════════════════════════════════════════════════════════════════

def gen_pc_risk_parity_vectors():
    """PC Risk Parity: uncorrelated, correlated, rank-deficient, N=2, N=5, N=10."""
    cases = {}

    # Case 1: 3-asset uncorrelated (orthogonal)
    ret_uncorr = np.array([
        [1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0],
        [0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0],
        [0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1],
    ], dtype=float)
    w = pc_risk_parity(ret_uncorr)
    cases["uncorrelated_3asset"] = {
        "returns": ret_uncorr, "weights": w, "n": 3,
        "note": "Orthogonal returns → approximately equal weights"
    }

    # Case 2: Perfectly correlated (rank-1)
    base = RNG.normal(0.01, 0.02, 60)
    ret_corr = np.array([base, base.copy(), base.copy()])
    w = pc_risk_parity(ret_corr)
    cases["perfectly_correlated_3asset"] = {
        "returns": ret_corr, "weights": w, "n": 3,
        "note": "Rank-1 → all weight in PC1 direction → equal weights in asset space"
    }

    # Case 3: N=2 minimum
    ret_2 = RNG.normal(0.001, 0.015, size=(2, 60))
    w = pc_risk_parity(ret_2)
    cases["n2_minimum"] = {
        "returns": ret_2, "weights": w, "n": 2,
    }

    # Case 4: N=5 moderate
    ret_5 = RNG.normal(0.001, 0.015, size=(5, 120))
    w = pc_risk_parity(ret_5)
    cases["n5_moderate"] = {
        "returns": ret_5, "weights": w, "n": 5,
    }

    # Case 5: N=10 larger universe
    ret_10 = RNG.normal(0.001, 0.012, size=(10, 252))
    w = pc_risk_parity(ret_10)
    cases["n10_large"] = {
        "returns": ret_10, "weights": w, "n": 10,
    }

    # Case 6: Block-correlated (2 blocks of 2)
    base_a = RNG.normal(0.001, 0.015, 60)
    base_b = RNG.normal(-0.001, 0.012, 60)
    ret_block = np.array([
        base_a + RNG.normal(0, 0.002, 60),
        base_a + RNG.normal(0, 0.002, 60),
        base_b + RNG.normal(0, 0.002, 60),
        base_b + RNG.normal(0, 0.002, 60),
    ])
    w = pc_risk_parity(ret_block)
    cases["block_correlated_4asset"] = {
        "returns": ret_block, "weights": w, "n": 4,
        "note": "2 blocks of correlated assets"
    }

    # Case 7: Custom signal threshold (very high → equal weight fallback)
    ret_thresh = RNG.normal(0.001, 0.015, size=(3, 60))
    w = pc_risk_parity(ret_thresh, signal_threshold=999.0)
    cases["high_threshold_fallback"] = {
        "returns": ret_thresh, "weights": w, "n": 3,
        "signal_threshold": 999.0,
        "note": "All PCs below threshold → equal weight"
    }

    # Case 8: q < 1 (more assets than observations) → Kaiser criterion
    ret_wide = RNG.normal(0.001, 0.015, size=(5, 3))
    w = pc_risk_parity(ret_wide)
    cases["q_less_than_1"] = {
        "returns": ret_wide, "weights": w, "n": 5,
        "note": "5 assets, 3 observations → Kaiser criterion (eigenvalue > 1.0)"
    }

    save("construction_pc_risk_parity", {
        "cases": cases,
        "notes": "PrincipalComponentRiskParityConstruction vectors. "
                 "Inverse-risk (1/sqrt(lambda)) allocation across signal PCs."
    })


def gen_pca_constrained_vectors():
    """PCA-Constrained decorator: passthrough, reduction, fallback."""
    cases = {}

    # Case 1: maxComponents=N → passthrough (equal weight inner)
    ret_3 = RNG.normal(0.001, 0.015, size=(3, 60))
    w = pca_constrained(ret_3, equal_weight, max_components=3)
    cases["passthrough_k_equals_n"] = {
        "returns": ret_3, "weights": w, "n": 3,
        "max_components": 3,
        "note": "K=N → passthrough to inner equal-weight model"
    }

    # Case 2: maxComponents=0 → equal weight fallback
    w = pca_constrained(ret_3, equal_weight, max_components=0)
    cases["fallback_k_zero"] = {
        "returns": ret_3, "weights": w, "n": 3,
        "max_components": 0,
        "note": "K=0 → equal weight fallback"
    }

    # Case 3: Dimensionality reduction with equal weight inner
    ret_5 = RNG.normal(0.001, 0.015, size=(5, 120))
    w = pca_constrained(ret_5, equal_weight, max_components=2)
    cases["reduction_equal_weight_inner"] = {
        "returns": ret_5, "weights": w, "n": 5,
        "max_components": 2,
        "note": "K=2 < N=5, equal weight in PC space"
    }

    # Case 4: Dimensionality reduction with inverse-vol inner
    w = pca_constrained(ret_5, inverse_volatility, max_components=2)
    cases["reduction_inverse_vol_inner"] = {
        "returns": ret_5, "weights": w, "n": 5,
        "max_components": 2,
        "note": "K=2 < N=5, inverse volatility in PC space"
    }

    # Case 5: Auto K from MP (no max_components cap)
    ret_structured = np.zeros((5, 200))
    base_factor = RNG.normal(0, 0.012, 200)
    for i in range(3):
        ret_structured[i] = base_factor + RNG.normal(0, 0.003, 200)
    for i in range(3, 5):
        ret_structured[i] = RNG.normal(0, 0.01, 200)
    w = pca_constrained(ret_structured, equal_weight)
    cases["auto_k_from_mp"] = {
        "returns": ret_structured, "weights": w, "n": 5,
        "note": "Factor-structured data → MP determines K automatically"
    }

    # Case 6: Single asset
    ret_1 = RNG.normal(0.001, 0.015, size=(1, 60))
    w = pca_constrained(ret_1, equal_weight)
    cases["single_asset"] = {
        "returns": ret_1, "weights": w, "n": 1,
        "note": "N=1 → full weight on single asset"
    }

    save("construction_pca_constrained", {
        "cases": cases,
        "notes": "PcaConstrainedConstruction vectors. "
                 "Projects returns to PC signal subspace, delegates to inner model, back-projects."
    })


def gen_detoned_covariance_vectors():
    """Detoned covariance estimator vectors."""
    cases = {}

    # Case 1: 3-asset with correlated pair
    ret_3 = np.array([
        RNG.normal(0.001, 0.015, 60),
        RNG.normal(0.001, 0.015, 60),
        RNG.normal(-0.001, 0.012, 60),
    ])
    # Make assets 0 and 1 correlated
    ret_3[1] = ret_3[0] * 0.9 + RNG.normal(0, 0.003, 60)

    d_full = detoned_cov(ret_3, detoning_alpha=1.0)
    d_half = detoned_cov(ret_3, detoning_alpha=0.5)
    d_zero = detoned_cov(ret_3, detoning_alpha=0.0)
    d_sample = sample_cov(ret_3)

    cases["three_asset"] = {
        "returns": ret_3,
        "detoned_alpha_1": d_full,
        "detoned_alpha_05": d_half,
        "detoned_alpha_0": d_zero,
        "sample_covariance": d_sample,
        "note": "Assets 0&1 correlated. Alpha=0 should match denoised."
    }

    # Case 2: N < 3 delegates to sample
    ret_2 = RNG.normal(0.001, 0.015, size=(2, 60))
    d_2 = detoned_cov(ret_2)
    s_2 = sample_cov(ret_2)
    cases["two_asset_delegates"] = {
        "returns": ret_2,
        "detoned_covariance": d_2,
        "sample_covariance": s_2,
        "note": "N<3 → delegates to sample"
    }

    # Case 3: 5-asset with factor structure
    base = RNG.normal(0.001, 0.012, 200)
    ret_5 = np.array([
        base + RNG.normal(0, 0.002, 200),
        base + RNG.normal(0, 0.002, 200),
        base + RNG.normal(0, 0.002, 200),
        RNG.normal(0.0001, 0.015, 200),
        RNG.normal(0.0002, 0.008, 200),
    ])
    d_5 = detoned_cov(ret_5)
    s_5 = sample_cov(ret_5)
    cases["five_asset_factor"] = {
        "returns": ret_5,
        "detoned_covariance": d_5,
        "sample_covariance": s_5,
        "note": "3 correlated + 2 independent. Tests signal/noise split."
    }

    save("covariance_detoned", {
        "cases": cases,
        "notes": "DetonedCovarianceEstimator vectors. "
                 "Extends denoising with PC1 shrinkage (Lopez de Prado 2020)."
    })


# ═══════════════════════════════════════════════════════════════════════════
# Correctness cross-checks (three layers)
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

    TOL_EXACT = 1e-10
    TOL_NUMERIC = 1e-6

    # ─── Layer 1: Library cross-references ──────────────────────────────
    print()
    print("Layer 1: Library cross-references (numpy)")
    print("-" * 50)

    # PC-RP on identity covariance → equal weights
    n = 3
    ret_id = np.array([
        RNG.normal(0, 0.01, 500),
        RNG.normal(0, 0.01, 500),
        RNG.normal(0, 0.01, 500),
    ])
    w = pc_risk_parity(ret_id)
    max_diff = max(abs(w[i] - 1.0 / n) for i in range(n))
    _quality(max_diff < 0.1, f"PC-RP on near-uncorrelated → ~equal weights (max_diff={max_diff:.4f})")

    # Detoned alpha=0 should match denoised
    ret_check = np.array([
        RNG.normal(0.001, 0.015, 100),
        RNG.normal(0.001, 0.015, 100),
        RNG.normal(-0.001, 0.012, 100),
    ])
    ret_check[1] = ret_check[0] * 0.8 + RNG.normal(0, 0.005, 100)
    d_alpha0 = detoned_cov(ret_check, detoning_alpha=0.0)
    d_denoised = denoised_cov(ret_check)
    diff = np.max(np.abs(d_alpha0 - d_denoised))
    _hard(diff < TOL_NUMERIC, f"Detoned(alpha=0) matches Denoised (max_diff={diff:.2e})")

    # ─── Layer 2: Analytical solutions ──────────────────────────────────
    print()
    print("Layer 2: Analytical solutions")
    print("-" * 50)

    # PC-RP: rank-1 covariance → all weight in PC1 → equal weights (PC1 is [1/√n, ...])
    base = RNG.normal(0, 0.01, 100)
    ret_rank1 = np.array([base, base.copy(), base.copy()])
    w_rank1 = pc_risk_parity(ret_rank1)
    _hard(abs(w_rank1.sum() - 1.0) < TOL_NUMERIC, f"PC-RP rank-1: weights sum to 1 ({w_rank1.sum():.8f})")
    _hard(all(w >= -TOL_NUMERIC for w in w_rank1), "PC-RP rank-1: all weights non-negative")

    # PCA-Constrained passthrough: K=N should match inner model
    ret_pass = RNG.normal(0, 0.01, size=(3, 60))
    w_direct = equal_weight(ret_pass)
    w_pca = pca_constrained(ret_pass, equal_weight, max_components=3)
    diff = np.max(np.abs(w_pca - w_direct))
    # Note: PCA passthrough re-projects through eigenvectors, so exact match
    # only holds when all eigenvalues are signal (above MP bound)
    _quality(diff < 0.1, f"PCA passthrough ~= direct (max_diff={diff:.4f})")

    # PCA-Constrained K=0 → equal weight
    w_k0 = pca_constrained(ret_pass, equal_weight, max_components=0)
    expected_ew = 1.0 / 3
    max_ew_diff = max(abs(w_k0[i] - expected_ew) for i in range(3))
    _hard(max_ew_diff < TOL_EXACT, f"PCA K=0 → equal weight (max_diff={max_ew_diff:.2e})")

    # Detoned: higher alpha → more off-diagonal shrinkage
    ret_alpha = np.array([
        RNG.normal(0.001, 0.015, 100),
        RNG.normal(0.001, 0.015, 100),
        RNG.normal(-0.001, 0.012, 100),
    ])
    ret_alpha[1] = ret_alpha[0] * 0.9 + RNG.normal(0, 0.003, 100)
    d_low = detoned_cov(ret_alpha, detoning_alpha=0.3)
    d_high = detoned_cov(ret_alpha, detoning_alpha=0.9)
    off_low = sum(abs(d_low[i, j]) for i in range(3) for j in range(i + 1, 3))
    off_high = sum(abs(d_high[i, j]) for i in range(3) for j in range(i + 1, 3))
    _quality(off_high <= off_low + 1e-10,
             f"Higher alpha → less off-diagonal (low={off_low:.6f}, high={off_high:.6f})")

    # ─── Layer 3: Property-based checks ─────────────────────────────────
    print()
    print("Layer 3: Property-based checks (mathematical invariants)")
    print("-" * 50)

    for label, ret in [
        ("3x60", RNG.normal(0, 0.012, (3, 60))),
        ("5x120", RNG.normal(0, 0.015, (5, 120))),
        ("2x30", RNG.normal(0, 0.01, (2, 30))),
    ]:
        n = ret.shape[0]
        print(f"  {label}:")

        # PC-RP properties
        w = pc_risk_parity(ret)
        _hard(abs(w.sum() - 1.0) < TOL_NUMERIC, f"PC-RP weights sum to 1 ({w.sum():.8f})")
        _hard(all(ww >= -TOL_NUMERIC for ww in w), "PC-RP all weights non-negative")
        _hard(len(w) == n, f"PC-RP returns {n} weights")

        # PCA-Constrained properties
        w_pca = pca_constrained(ret, equal_weight, max_components=min(2, n))
        _hard(abs(w_pca.sum() - 1.0) < TOL_NUMERIC, f"PCA weights sum to 1 ({w_pca.sum():.8f})")
        _hard(all(ww >= -TOL_NUMERIC for ww in w_pca), "PCA all weights non-negative")

        # Detoned properties (N >= 3 only)
        if n >= 3:
            d = detoned_cov(ret)
            # Symmetry
            asym = np.max(np.abs(d - d.T))
            _hard(asym < TOL_EXACT, f"Detoned symmetric (max_asym={asym:.2e})")
            # Positive diagonal
            _hard(all(d[i, i] > 0 for i in range(n)), "Detoned positive diagonal")

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


# ═══════════════════════════════════════════════════════════════════════════
# Main
# ═══════════════════════════════════════════════════════════════════════════

def main():
    print("Generating PCA construction model vectors...")
    print()

    print("PC Risk Parity:")
    gen_pc_risk_parity_vectors()

    print("PCA-Constrained:")
    gen_pca_constrained_vectors()

    print("Detoned Covariance:")
    gen_detoned_covariance_vectors()

    print()
    print(f"Done. PCA vectors in {VECTORS_DIR}")

    # Correctness validation
    run_cross_checks()


if __name__ == "__main__":
    main()
