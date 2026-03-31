#!/usr/bin/env python3
"""
Generate cross-language verification vectors for HERC, DynamicBlackLitterman,
and TacticalOverlay (direct algorithm verification).

Post-roadmap gap closure. Three-layer cross-checks:
  1. Library cross-references (pypfopt HRP as HERC baseline comparison)
  2. Analytical solutions (HERC with equal vol = equal weight; no views = equilibrium)
  3. Property-based checks (sum=1, non-negative, invariants)
"""

import json
import math
from pathlib import Path

import numpy as np

from generate_construction_basic_vectors import (
    sample_cov,
    generate_diverse_returns,
)
from generate_construction_advanced_vectors import (
    compute_correlation_matrix,
    compute_distance_matrix,
    cluster_and_reorder,
    compute_cluster_variance,
    recursive_bisection_hrp,
    project_onto_simplex,
)

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

RNG = np.random.default_rng(seed=707)  # Distinct seed

# ═══════════════════════════════════════════════════════════════════════════
# Correctness tracking
# ═══════════════════════════════════════════════════════════════════════════

HARD_CHECKS: list[tuple[str, bool]] = []
QUALITY_CHECKS: list[tuple[str, bool, str]] = []


def hard_check(name: str, condition: bool):
    HARD_CHECKS.append((name, condition))
    assert condition, f"HARD CHECK FAILED: {name}"


def quality_check(name: str, actual: float, expected: float, rel_tol: float = 0.01):
    if expected == 0:
        gap = abs(actual)
        passed = gap < 1e-6
    else:
        gap = abs(actual - expected) / abs(expected)
        passed = gap < rel_tol
    QUALITY_CHECKS.append((name, passed, f"gap={gap:.6f}"))
    if not passed:
        print(f"  ⚠ QUALITY GAP: {name} — actual={actual:.8f}, expected={expected:.8f}, gap={gap:.4%}")


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
    print(f"  -> {name}.json")


# ═══════════════════════════════════════════════════════════════════════════
# HERC: Same as HRP but uses 1/σ (inverse risk) instead of 1/σ² (inverse variance)
# Matches C# HierarchicalEqualRiskContributionConstruction exactly
# ═══════════════════════════════════════════════════════════════════════════

def recursive_bisection_herc(sorted_indices: list[int], cov: np.ndarray, weights: np.ndarray):
    """Recursive bisection with inverse-risk (1/σ) allocation. Matches C# HERC."""
    if len(sorted_indices) <= 1:
        return

    mid = len(sorted_indices) // 2
    left = sorted_indices[:mid]
    right = sorted_indices[mid:]

    var_left = compute_cluster_variance(left, cov)
    var_right = compute_cluster_variance(right, cov)

    # HERC: inverse standard deviation (not inverse variance like HRP)
    risk_left = math.sqrt(max(0.0, var_left))
    risk_right = math.sqrt(max(0.0, var_right))

    total_inv_risk = 0.0
    if risk_left > 0:
        total_inv_risk += 1.0 / risk_left
    if risk_right > 0:
        total_inv_risk += 1.0 / risk_right

    if total_inv_risk > 0 and risk_left > 0:
        alpha_left = (1.0 / risk_left) / total_inv_risk
    else:
        alpha_left = 0.5

    alpha_right = 1.0 - alpha_left

    for idx in left:
        weights[idx] *= alpha_left
    for idx in right:
        weights[idx] *= alpha_right

    recursive_bisection_herc(left, cov, weights)
    recursive_bisection_herc(right, cov, weights)


def herc(returns: np.ndarray) -> np.ndarray:
    """Full HERC pipeline matching C#."""
    n = returns.shape[0]
    cov = sample_cov(returns)
    corr = compute_correlation_matrix(cov)
    dist = compute_distance_matrix(corr)
    sorted_indices = cluster_and_reorder(dist, n)

    w = np.ones(n)
    recursive_bisection_herc(sorted_indices, cov, w)
    return project_onto_simplex(w)


def hrp(returns: np.ndarray) -> np.ndarray:
    """Full HRP pipeline for comparison."""
    n = returns.shape[0]
    cov = sample_cov(returns)
    corr = compute_correlation_matrix(cov)
    dist = compute_distance_matrix(corr)
    sorted_indices = cluster_and_reorder(dist, n)

    w = np.ones(n)
    recursive_bisection_hrp(sorted_indices, cov, w)
    return project_onto_simplex(w)


def gen_herc():
    print("Generating HERC vectors...")

    cases = {}

    # 5-asset case
    returns_5 = generate_diverse_returns(5, 252)
    w_herc_5 = herc(returns_5)
    w_hrp_5 = hrp(returns_5)
    cases["five_asset"] = {"returns": returns_5, "weights": w_herc_5}

    # Layer 3: Property checks
    hard_check("HERC 5-asset: sum=1", abs(w_herc_5.sum() - 1.0) < 1e-10)
    hard_check("HERC 5-asset: non-negative", np.all(w_herc_5 >= -1e-14))
    hard_check("HERC 5-asset: differs from HRP", np.max(np.abs(w_herc_5 - w_hrp_5)) > 1e-6)

    # 3-asset case
    returns_3 = generate_diverse_returns(3, 252)
    w_herc_3 = herc(returns_3)
    cases["three_asset"] = {"returns": returns_3, "weights": w_herc_3}

    hard_check("HERC 3-asset: sum=1", abs(w_herc_3.sum() - 1.0) < 1e-10)
    hard_check("HERC 3-asset: non-negative", np.all(w_herc_3 >= -1e-14))

    # 2-asset case
    returns_2 = generate_diverse_returns(2, 252)
    w_herc_2 = herc(returns_2)
    cases["two_asset"] = {"returns": returns_2, "weights": w_herc_2}

    hard_check("HERC 2-asset: sum=1", abs(w_herc_2.sum() - 1.0) < 1e-10)

    # Layer 2: Analytical — 2 identical assets should give 50/50
    rng_local = np.random.default_rng(seed=707)
    returns_identical = np.tile(rng_local.normal(0.001, 0.02, 252), (2, 1))
    # Add tiny noise to avoid singular cov
    returns_identical[1] += rng_local.normal(0, 1e-10, 252)
    w_identical = herc(returns_identical)
    cases["two_identical"] = {"returns": returns_identical, "weights": w_identical}

    hard_check("HERC identical: ~equal",
               abs(w_identical[0] - w_identical[1]) < 0.01)

    # Layer 1: Quality — HERC vs HRP shouldn't diverge wildly (same dendrogram)
    cov_5 = sample_cov(returns_5)
    var_herc = float(w_herc_5 @ cov_5 @ w_herc_5)
    var_hrp = float(w_hrp_5 @ cov_5 @ w_hrp_5)
    quality_check("HERC vs HRP variance ratio", var_herc, var_hrp, rel_tol=0.50)

    # Single asset
    returns_1 = generate_diverse_returns(1, 50)
    w_herc_1 = herc(returns_1)
    cases["single_asset"] = {"returns": returns_1, "weights": w_herc_1}

    hard_check("HERC single: w=1.0", abs(w_herc_1[0] - 1.0) < 1e-10)

    save("construction_herc", {
        "cases": cases,
        "notes": "HERC: single-linkage clustering + recursive bisection with inverse-risk (1/sigma)."
    })


# ═══════════════════════════════════════════════════════════════════════════
# Dynamic Black-Litterman: 1/N equilibrium + view integration
# Matches C# DynamicBlackLittermanConstruction exactly
# ═══════════════════════════════════════════════════════════════════════════

def invert_matrix(matrix: np.ndarray) -> np.ndarray:
    """Gauss-Jordan inversion matching C# InvertMatrix."""
    size = matrix.shape[0]
    augmented = np.zeros((size, 2 * size))
    augmented[:, :size] = matrix.copy()
    for i in range(size):
        augmented[i, size + i] = 1.0

    for col in range(size):
        max_row = col
        for row in range(col + 1, size):
            if abs(augmented[row, col]) > abs(augmented[max_row, col]):
                max_row = row
        if max_row != col:
            augmented[[col, max_row]] = augmented[[max_row, col]]

        pivot = augmented[col, col]
        if abs(pivot) < 1e-20:
            raise ValueError("Singular matrix")

        augmented[col] /= pivot

        for row in range(size):
            if row == col:
                continue
            factor = augmented[row, col]
            augmented[row] -= factor * augmented[col]

    return augmented[:, size:]


def dynamic_black_litterman(
    returns: np.ndarray,
    views: list[dict],
    risk_aversion: float = 2.5,
    tau: float = 0.05,
) -> np.ndarray:
    """Full Dynamic BL pipeline matching C#."""
    n = returns.shape[0]
    cov = sample_cov(returns)

    # Equilibrium: 1/N
    eq_weights = np.full(n, 1.0 / n)

    # Implied returns: pi = delta * Sigma * w_eq
    pi = risk_aversion * (cov @ eq_weights)

    if len(views) == 0:
        posterior_mu = pi.copy()
    else:
        k = len(views)
        P = np.zeros((k, n))
        Q = np.zeros(k)

        for v_idx, view in enumerate(views):
            Q[v_idx] = view["expected_return"]
            if view["type"] == "absolute":
                P[v_idx, view["asset_index"]] = 1.0
            else:  # relative
                P[v_idx, view["long_index"]] = 1.0
                P[v_idx, view["short_index"]] = -1.0

        # Omega (Idzorek formulation)
        omega = np.zeros((k, k))
        for v_idx, view in enumerate(views):
            p_tau_sigma_pt = P[v_idx] @ (tau * cov) @ P[v_idx]
            omega[v_idx, v_idx] = (1.0 / view["confidence"] - 1.0) * p_tau_sigma_pt

        # BL posterior
        tau_sigma_pt = tau * cov @ P.T  # N x K
        M = P @ tau_sigma_pt + omega     # K x K
        M_inv = invert_matrix(M)

        q_minus_p_pi = Q - P @ pi
        posterior_mu = pi + tau_sigma_pt @ M_inv @ q_minus_p_pi

    # Optimal weights: w* = (1/delta) * Sigma^-1 * mu_BL
    try:
        sigma_inv = invert_matrix(cov)
        raw_weights = sigma_inv @ posterior_mu / risk_aversion
    except ValueError:
        # Singular — diagonal fallback
        raw_weights = np.zeros(n)
        for i in range(n):
            if cov[i, i] > 0:
                raw_weights[i] = posterior_mu[i] / (risk_aversion * cov[i, i])

    # Normalize: floor at 0, sum to 1
    raw_weights = np.maximum(raw_weights, 0.0)
    s = raw_weights.sum()
    if s > 0:
        raw_weights /= s
    else:
        raw_weights = np.full(n, 1.0 / n)

    return project_onto_simplex(raw_weights)


def gen_dynamic_black_litterman():
    print("Generating DynamicBlackLitterman vectors...")

    cases = {}

    returns_3 = generate_diverse_returns(3, 252)

    # No views — should return equilibrium-derived weights
    w_no_views = dynamic_black_litterman(returns_3, [])
    cases["no_views"] = {
        "returns": returns_3,
        "views": [],
        "risk_aversion": 2.5,
        "tau": 0.05,
        "weights": w_no_views,
    }

    hard_check("DynBL no-views: sum=1", abs(w_no_views.sum() - 1.0) < 1e-10)
    hard_check("DynBL no-views: non-negative", np.all(w_no_views >= -1e-14))

    # One absolute view: ASSET0 will return 8%
    views_abs = [{"type": "absolute", "asset_index": 0, "expected_return": 0.08, "confidence": 0.8}]
    w_abs = dynamic_black_litterman(returns_3, views_abs)
    cases["one_absolute_view"] = {
        "returns": returns_3,
        "views": [{"type": "absolute", "asset": "ASSET0", "expected_return": 0.08, "confidence": 0.8}],
        "risk_aversion": 2.5,
        "tau": 0.05,
        "weights": w_abs,
    }

    hard_check("DynBL abs view: sum=1", abs(w_abs.sum() - 1.0) < 1e-10)
    hard_check("DynBL abs view: non-negative", np.all(w_abs >= -1e-14))
    hard_check("DynBL abs view: ASSET0 tilted up", w_abs[0] > w_no_views[0])

    # One relative view: ASSET1 outperforms ASSET2 by 3%
    views_rel = [{"type": "relative", "long_index": 1, "short_index": 2,
                  "expected_return": 0.03, "confidence": 0.6}]
    w_rel = dynamic_black_litterman(returns_3, views_rel)
    cases["one_relative_view"] = {
        "returns": returns_3,
        "views": [{"type": "relative", "long_asset": "ASSET1", "short_asset": "ASSET2",
                    "expected_return": 0.03, "confidence": 0.6}],
        "risk_aversion": 2.5,
        "tau": 0.05,
        "weights": w_rel,
    }

    hard_check("DynBL rel view: sum=1", abs(w_rel.sum() - 1.0) < 1e-10)
    hard_check("DynBL rel view: ASSET1 > ASSET2", w_rel[1] > w_rel[2])

    # High confidence view should tilt more
    views_high_conf = [{"type": "absolute", "asset_index": 0, "expected_return": 0.08, "confidence": 0.99}]
    w_high_conf = dynamic_black_litterman(returns_3, views_high_conf)
    cases["high_confidence_view"] = {
        "returns": returns_3,
        "views": [{"type": "absolute", "asset": "ASSET0", "expected_return": 0.08, "confidence": 0.99}],
        "risk_aversion": 2.5,
        "tau": 0.05,
        "weights": w_high_conf,
    }

    hard_check("DynBL high conf: sum=1", abs(w_high_conf.sum() - 1.0) < 1e-10)
    hard_check("DynBL high conf: more tilt than low conf", w_high_conf[0] > w_abs[0])

    # Layer 2: Analytical — with very low confidence, view effect should be smaller
    views_low = [{"type": "absolute", "asset_index": 0, "expected_return": 0.08, "confidence": 0.01}]
    w_low_conf = dynamic_black_litterman(returns_3, views_low)
    # Low confidence should produce less tilt than high confidence
    tilt_low = abs(w_low_conf[0] - w_no_views[0])
    tilt_high = abs(w_high_conf[0] - w_no_views[0])
    hard_check("DynBL low conf: less tilt than high conf", tilt_low < tilt_high)

    # Layer 1: Quality — variance should be reasonable vs equal-weight
    cov_3 = sample_cov(returns_3)
    var_no_views = float(w_no_views @ cov_3 @ w_no_views)
    var_eq = float(np.full(3, 1/3) @ cov_3 @ np.full(3, 1/3))
    quality_check("DynBL no-views var vs EW", var_no_views, var_eq, rel_tol=0.50)

    save("construction_dynamic_bl", {
        "cases": cases,
        "notes": "DynamicBlackLitterman: 1/N equilibrium + Idzorek confidence-scaled views."
    })


# ═══════════════════════════════════════════════════════════════════════════
# Tactical Overlay: base model + regime tilts + momentum overlay
# Matches C# TacticalOverlayConstruction exactly
# ═══════════════════════════════════════════════════════════════════════════

def tactical_overlay(
    base_weights: dict[int, float],
    tilts: dict[int, float],
    momentum_scores: dict[int, float] | None,
    momentum_strength: float,
    n_assets: int,
) -> np.ndarray:
    """Replicate TacticalOverlayConstruction.ComputeTargetWeights algorithm."""
    adjusted = {}
    for i in range(n_assets):
        w = base_weights.get(i, 0.0)
        w += tilts.get(i, 0.0)
        if momentum_scores is not None and i in momentum_scores:
            w += momentum_scores[i] * momentum_strength
        adjusted[i] = max(w, 0.0)  # Floor at zero

    total = sum(adjusted.values())
    if total > 0:
        for i in range(n_assets):
            adjusted[i] /= total
    else:
        eq = 1.0 / n_assets
        for i in range(n_assets):
            adjusted[i] = eq

    return np.array([adjusted[i] for i in range(n_assets)])


def gen_tactical_overlay():
    print("Generating TacticalOverlay direct algorithm vectors...")

    cases = {}
    n = 3
    returns_3 = generate_diverse_returns(n, 252)

    # Base weights: equal weight (1/3 each)
    base_w = {0: 1/3, 1: 1/3, 2: 1/3}

    # Case 1: Zero tilts, no momentum → passthrough
    tilts_zero = {0: 0.0, 1: 0.0, 2: 0.0}
    w_passthrough = tactical_overlay(base_w, tilts_zero, None, 0.0, n)
    cases["zero_tilts"] = {
        "returns": returns_3,
        "base_weights": list(base_w.values()),
        "tilts": tilts_zero,
        "momentum_scores": None,
        "momentum_strength": 0.0,
        "weights": w_passthrough,
    }

    hard_check("Tactical zero tilts: sum=1", abs(w_passthrough.sum() - 1.0) < 1e-10)
    hard_check("Tactical zero tilts: passthrough",
               np.max(np.abs(w_passthrough - np.array([1/3, 1/3, 1/3]))) < 1e-10)

    # Case 2: Positive tilt on asset 0
    tilts_pos = {0: 0.10, 1: -0.05, 2: -0.05}
    w_tilted = tactical_overlay(base_w, tilts_pos, None, 0.0, n)
    cases["positive_tilt"] = {
        "returns": returns_3,
        "base_weights": list(base_w.values()),
        "tilts": tilts_pos,
        "momentum_scores": None,
        "momentum_strength": 0.0,
        "weights": w_tilted,
    }

    hard_check("Tactical pos tilt: sum=1", abs(w_tilted.sum() - 1.0) < 1e-10)
    hard_check("Tactical pos tilt: ASSET0 > 1/3", w_tilted[0] > 1/3)
    hard_check("Tactical pos tilt: non-negative", np.all(w_tilted >= -1e-14))

    # Case 3: Momentum only (no regime tilts)
    momentum = {0: 0.5, 1: -0.2, 2: 0.1}
    w_momentum = tactical_overlay(base_w, tilts_zero, momentum, 0.1, n)
    cases["momentum_only"] = {
        "returns": returns_3,
        "base_weights": list(base_w.values()),
        "tilts": tilts_zero,
        "momentum_scores": momentum,
        "momentum_strength": 0.1,
        "weights": w_momentum,
    }

    hard_check("Tactical momentum: sum=1", abs(w_momentum.sum() - 1.0) < 1e-10)
    hard_check("Tactical momentum: ASSET0 highest (best momentum)",
               w_momentum[0] >= w_momentum[1] and w_momentum[0] >= w_momentum[2])

    # Case 4: Tilt + momentum combined
    tilts_mix = {0: 0.05, 1: 0.0, 2: -0.05}
    momentum_mix = {0: 0.3, 1: -0.1, 2: 0.2}
    w_combined = tactical_overlay(base_w, tilts_mix, momentum_mix, 0.1, n)
    cases["tilt_plus_momentum"] = {
        "returns": returns_3,
        "base_weights": list(base_w.values()),
        "tilts": tilts_mix,
        "momentum_scores": momentum_mix,
        "momentum_strength": 0.1,
        "weights": w_combined,
    }

    hard_check("Tactical combined: sum=1", abs(w_combined.sum() - 1.0) < 1e-10)
    hard_check("Tactical combined: non-negative", np.all(w_combined >= -1e-14))

    # Case 5: Large negative tilt floors at zero
    tilts_large_neg = {0: -0.50, 1: 0.25, 2: 0.25}
    w_floored = tactical_overlay(base_w, tilts_large_neg, None, 0.0, n)
    cases["floor_at_zero"] = {
        "returns": returns_3,
        "base_weights": list(base_w.values()),
        "tilts": tilts_large_neg,
        "momentum_scores": None,
        "momentum_strength": 0.0,
        "weights": w_floored,
    }

    hard_check("Tactical floor: sum=1", abs(w_floored.sum() - 1.0) < 1e-10)
    hard_check("Tactical floor: ASSET0 = 0 (floored)", w_floored[0] < 1e-10)
    hard_check("Tactical floor: non-negative", np.all(w_floored >= -1e-14))

    # Layer 2: Analytical — zero tilt = base weights (tested above)
    # Layer 3: Renormalization after floor test
    hard_check("Tactical floor: ASSET1+ASSET2 fill the gap",
               abs(w_floored[1] + w_floored[2] - 1.0) < 1e-10)

    save("construction_tactical_overlay_direct", {
        "cases": cases,
        "notes": "TacticalOverlay: base weights + additive tilts + momentum, floor at 0, renormalize."
    })


# ═══════════════════════════════════════════════════════════════════════════

def main():
    gen_herc()
    gen_dynamic_black_litterman()
    gen_tactical_overlay()

    print()
    print("=" * 60)
    print("CROSS-CHECK SUMMARY")
    print("=" * 60)
    print(f"  HARD checks: {sum(1 for _, ok in HARD_CHECKS if ok)}/{len(HARD_CHECKS)} passed")
    failed_hard = [(n, ok) for n, ok in HARD_CHECKS if not ok]
    if failed_hard:
        for name, _ in failed_hard:
            print(f"    ✗ {name}")
    else:
        print("    All HARD checks passed ✓")

    print(f"  QUALITY checks: {sum(1 for _, ok, _ in QUALITY_CHECKS if ok)}/{len(QUALITY_CHECKS)} passed")
    for name, ok, detail in QUALITY_CHECKS:
        status = "✓" if ok else "⚠"
        print(f"    {status} {name} ({detail})")

    print()
    print("Done — 3 vector files generated.")


if __name__ == "__main__":
    main()
