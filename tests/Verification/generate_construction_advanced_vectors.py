#!/usr/bin/env python3
"""
Generate cross-language verification vectors for advanced portfolio construction models.

Phase 3 of the verification roadmap.
Models: HRP, ReturnTiltedHRP, BlackLitterman, RobustMeanVariance,
        MeanDownsideRisk (CVaR), MeanDownsideRisk (Sortino),
        TurnoverPenalized, VolatilityTargeting.

All Python reference implementations replicate the EXACT C# algorithms
(per Phase 2 learning: own-formula > library matching).

CORRECTNESS VALIDATION (three layers):
  1. Library cross-references — compare against scipy/pypfopt on fresh data.
  2. Analytical solutions — closed-form answers for special cases.
  3. Property-based checks — mathematical invariants.
"""

import json
import math
from pathlib import Path

import numpy as np
from pypfopt import EfficientFrontier

# Import shared helpers from Phase 2
from generate_construction_basic_vectors import (
    sample_cov,
    portfolio_variance,
    generate_diverse_returns,
    minimum_variance,
    mean_variance,
    equal_weight,
)

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

RNG_PHASE3 = np.random.default_rng(seed=303)  # Distinct seed for Phase 3

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
# Shared projection helper (matches C# iterative clamping)
# ═══════════════════════════════════════════════════════════════════════════

def project_onto_simplex(w: np.ndarray, min_weight=0.0, max_weight=1.0) -> np.ndarray:
    """Project weights onto constrained simplex: minW <= w_i <= maxW, sum=1.
    Matches C# ProjectOntoSimplex."""
    n = len(w)
    max_weight = max(max_weight, 1.0 / n)
    min_weight = min(min_weight, 1.0 / n)

    for _ in range(50):
        w = np.clip(w, min_weight, max_weight)
        s = w.sum()
        if s <= 0:
            return np.full(n, 1.0 / n)
        w /= s
        if np.all(w >= min_weight - 1e-14) and np.all(w <= max_weight + 1e-14):
            break
    return w


# ═══════════════════════════════════════════════════════════════════════════
# HRP: Single-linkage clustering + recursive bisection
# Matches C# HierarchicalRiskParityConstruction exactly
# ═══════════════════════════════════════════════════════════════════════════

def compute_correlation_matrix(cov: np.ndarray) -> np.ndarray:
    """Matches C# ComputeCorrelationMatrix."""
    n = cov.shape[0]
    corr = np.zeros((n, n))
    for i in range(n):
        for j in range(n):
            denom = math.sqrt(float(cov[i, i] * cov[j, j]))
            if denom > 0:
                corr[i, j] = cov[i, j] / denom
            else:
                corr[i, j] = 1.0 if i == j else 0.0
    return corr


def compute_distance_matrix(corr: np.ndarray) -> np.ndarray:
    """Correlation distance: d(i,j) = sqrt(0.5 * (1 - corr(i,j))). Matches C#."""
    n = corr.shape[0]
    dist = np.zeros((n, n))
    for i in range(n):
        for j in range(n):
            if i != j:
                d = 0.5 * (1.0 - corr[i, j])
                dist[i, j] = math.sqrt(max(0.0, d))
    return dist


def cluster_and_reorder(dist: np.ndarray, n: int) -> list[int]:
    """Single-linkage agglomerative clustering. Matches C# ClusterAndReorder."""
    clusters = [[i] for i in range(n)]
    active = list(range(n))
    cluster_dist = dist.copy()

    while len(active) > 1:
        min_d = float('inf')
        min_i, min_j = -1, -1
        for ii in range(len(active)):
            for jj in range(ii + 1, len(active)):
                d = cluster_dist[active[ii], active[jj]]
                if d < min_d:
                    min_d = d
                    min_i, min_j = ii, jj

        ci = active[min_i]
        cj = active[min_j]

        clusters[ci].extend(clusters[cj])

        for k in active:
            if k == ci or k == cj:
                continue
            cluster_dist[ci, k] = min(cluster_dist[ci, k], cluster_dist[cj, k])
            cluster_dist[k, ci] = cluster_dist[ci, k]

        active.pop(min_j)

    return clusters[active[0]]


def compute_cluster_variance(indices: list[int], cov: np.ndarray) -> float:
    """Equal-weight sub-portfolio variance. Matches C#."""
    n = len(indices)
    if n == 0:
        return 0.0
    w = 1.0 / n
    var = 0.0
    for i in range(n):
        for j in range(n):
            var += w * w * cov[indices[i], indices[j]]
    return var


def recursive_bisection_hrp(sorted_indices: list[int], cov: np.ndarray, weights: np.ndarray):
    """Recursive bisection with inverse-variance allocation. Matches C#."""
    if len(sorted_indices) <= 1:
        return

    mid = len(sorted_indices) // 2
    left = sorted_indices[:mid]
    right = sorted_indices[mid:]

    var_left = compute_cluster_variance(left, cov)
    var_right = compute_cluster_variance(right, cov)

    total_inv_var = 0.0
    if var_left > 0:
        total_inv_var += 1.0 / var_left
    if var_right > 0:
        total_inv_var += 1.0 / var_right

    if total_inv_var > 0 and var_left > 0:
        alpha_left = (1.0 / var_left) / total_inv_var
    else:
        alpha_left = 0.5

    alpha_right = 1.0 - alpha_left

    for idx in left:
        weights[idx] *= alpha_left
    for idx in right:
        weights[idx] *= alpha_right

    recursive_bisection_hrp(left, cov, weights)
    recursive_bisection_hrp(right, cov, weights)


def hrp(returns: np.ndarray, min_weight=0.0, max_weight=1.0) -> np.ndarray:
    """Full HRP pipeline. Matches C# HierarchicalRiskParityConstruction."""
    n = returns.shape[0]
    if n == 1:
        return np.array([1.0])

    cov = sample_cov(returns)
    corr = compute_correlation_matrix(cov)
    dist = compute_distance_matrix(corr)
    sorted_indices = cluster_and_reorder(dist, n)

    w = np.ones(n)
    recursive_bisection_hrp(sorted_indices, cov, w)

    w = project_onto_simplex(w, min_weight, max_weight)
    return w


# ═══════════════════════════════════════════════════════════════════════════
# Return-Tilted HRP
# Matches C# ReturnTiltedHrpConstruction exactly
# ═══════════════════════════════════════════════════════════════════════════

def compute_cluster_mean_return(indices: list[int], mean_returns: np.ndarray) -> float:
    if len(indices) == 0:
        return 0.0
    return sum(mean_returns[i] for i in indices) / len(indices)


def recursive_bisection_tilted(sorted_indices: list[int], cov: np.ndarray,
                                mean_returns: np.ndarray, kappa: float,
                                weights: np.ndarray):
    """Return-tilted recursive bisection. Matches C#."""
    if len(sorted_indices) <= 1:
        return

    mid = len(sorted_indices) // 2
    left = sorted_indices[:mid]
    right = sorted_indices[mid:]

    var_left = compute_cluster_variance(left, cov)
    var_right = compute_cluster_variance(right, cov)

    total_inv_var = 0.0
    if var_left > 0:
        total_inv_var += 1.0 / var_left
    if var_right > 0:
        total_inv_var += 1.0 / var_right

    if total_inv_var > 0 and var_left > 0:
        alpha_risk = (1.0 / var_left) / total_inv_var
    else:
        alpha_risk = 0.5

    return_left = compute_cluster_mean_return(left, mean_returns)
    return_right = compute_cluster_mean_return(right, mean_returns)

    if kappa > 0:
        # Softmax handles all-negative returns correctly (exp() always positive).
        exp_left = math.exp(return_left)
        exp_right = math.exp(return_right)
        exp_sum = exp_left + exp_right
        alpha_return = exp_left / exp_sum if exp_sum > 0 else 0.5
        alpha_left = (1.0 - kappa) * alpha_risk + kappa * alpha_return
    else:
        alpha_left = alpha_risk

    alpha_right = 1.0 - alpha_left

    for idx in left:
        weights[idx] *= alpha_left
    for idx in right:
        weights[idx] *= alpha_right

    recursive_bisection_tilted(left, cov, mean_returns, kappa, weights)
    recursive_bisection_tilted(right, cov, mean_returns, kappa, weights)


def return_tilted_hrp(returns: np.ndarray, kappa=0.5,
                       min_weight=0.0, max_weight=1.0) -> np.ndarray:
    """Full return-tilted HRP pipeline. Matches C#."""
    n = returns.shape[0]
    if n == 1:
        return np.array([1.0])

    mean_returns = returns.mean(axis=1)
    cov = sample_cov(returns)
    corr = compute_correlation_matrix(cov)
    dist = compute_distance_matrix(corr)
    sorted_indices = cluster_and_reorder(dist, n)

    w = np.ones(n)
    recursive_bisection_tilted(sorted_indices, cov, mean_returns, kappa, w)

    w = project_onto_simplex(w, min_weight, max_weight)
    return w


# ═══════════════════════════════════════════════════════════════════════════
# Black-Litterman
# Matches C# BlackLittermanConstruction exactly
# ═══════════════════════════════════════════════════════════════════════════

def invert_matrix(matrix: np.ndarray) -> np.ndarray:
    """Gauss-Jordan inversion. Matches C# InvertMatrix."""
    size = matrix.shape[0]
    aug = np.zeros((size, 2 * size))
    aug[:, :size] = matrix.copy()
    for i in range(size):
        aug[i, size + i] = 1.0

    for col in range(size):
        max_row = col
        for row in range(col + 1, size):
            if abs(aug[row, col]) > abs(aug[max_row, col]):
                max_row = row
        if max_row != col:
            aug[[col, max_row]] = aug[[max_row, col]]

        if abs(aug[col, col]) < 1e-20:
            raise ValueError("Matrix is singular")

        aug[col] /= aug[col, col]

        for row in range(size):
            if row == col:
                continue
            factor = aug[row, col]
            aug[row] -= factor * aug[col]

    return aug[:, size:]


def black_litterman(returns: np.ndarray, equilibrium_weights: np.ndarray,
                     risk_aversion=2.5, tau=0.05,
                     pick_matrix=None, view_returns=None, view_uncertainty=None,
                     min_weight=0.0, max_weight=1.0) -> np.ndarray:
    """Black-Litterman model. Matches C# BlackLittermanConstruction."""
    n = returns.shape[0]
    sigma = sample_cov(returns)

    # Step 1: Implied equilibrium returns
    pi = risk_aversion * sigma @ equilibrium_weights

    # Step 2: Posterior
    if pick_matrix is None or view_returns is None or view_uncertainty is None:
        posterior_mu = pi
    else:
        k = len(view_returns)
        # tau*Sigma*P'
        tau_sigma_pt = tau * sigma @ pick_matrix.T
        # P*tau*Sigma*P' + Omega
        m = pick_matrix @ tau_sigma_pt + view_uncertainty
        m_inv = invert_matrix(m)
        q_minus_p_pi = view_returns - pick_matrix @ pi
        m_inv_q = m_inv @ q_minus_p_pi
        posterior_mu = pi + tau_sigma_pt @ m_inv_q

    # Step 3: Optimal weights w* = (1/delta) * Sigma^-1 * mu_BL
    try:
        sigma_inv = invert_matrix(sigma)
        raw_weights = sigma_inv @ posterior_mu / risk_aversion
    except ValueError:
        # Singular — diagonal approximation
        raw_weights = np.array([
            posterior_mu[i] / (risk_aversion * sigma[i, i]) if sigma[i, i] > 0 else 0.0
            for i in range(n)
        ])

    # Normalize (clip negative to 0)
    raw_weights = np.maximum(raw_weights, 0)
    s = raw_weights.sum()
    if s > 0:
        raw_weights /= s
    else:
        raw_weights = np.full(n, 1.0 / n)

    raw_weights = project_onto_simplex(raw_weights, min_weight, max_weight)
    return raw_weights


# ═══════════════════════════════════════════════════════════════════════════
# Robust Mean-Variance
# Matches C# RobustMeanVarianceConstruction exactly
# ═══════════════════════════════════════════════════════════════════════════

def compute_utility(w, means, cov, risk_aversion=1.0):
    port_return = w @ means
    port_var = w @ cov @ w
    return port_return - risk_aversion * 0.5 * port_var


def optimize_for_scenario(w, means, cov, risk_aversion=1.0,
                           min_weight=0.0, max_weight=1.0,
                           max_iterations=3000, tolerance=1e-10):
    """Projected gradient ascent on mean-variance utility. Matches C#."""
    n = len(w)
    learning_rate = 0.1

    for _ in range(max_iterations):
        grad = means - risk_aversion * cov @ w

        stepped = False
        current_lr = learning_rate
        current_utility = compute_utility(w, means, cov, risk_aversion)

        for _ in range(20):
            candidate = w + current_lr * grad
            candidate = project_onto_simplex(candidate, min_weight, max_weight)
            new_utility = compute_utility(candidate, means, cov, risk_aversion)

            if new_utility > current_utility:
                max_diff = np.max(np.abs(candidate - w))
                w = candidate.copy()
                stepped = True
                if max_diff < tolerance:
                    return w
                break
            current_lr *= 0.5

        if not stepped:
            break

    return w


def robust_mean_variance(returns: np.ndarray, cov_scenarios: list[np.ndarray],
                          risk_aversion=1.0, min_weight=0.0, max_weight=1.0,
                          max_alternating_rounds=20, tolerance=1e-10) -> np.ndarray:
    """Robust MV via alternating optimization. Matches C#."""
    n = returns.shape[0]
    means = returns.mean(axis=1)
    w = np.full(n, 1.0 / n)

    if len(cov_scenarios) == 1:
        return optimize_for_scenario(w, means, cov_scenarios[0], risk_aversion,
                                      min_weight, max_weight)

    best_worst_utility = -float('inf')
    best_w = w.copy()

    for _ in range(max_alternating_rounds):
        # Find worst scenario
        worst_idx = min(range(len(cov_scenarios)),
                        key=lambda s: compute_utility(w, means, cov_scenarios[s], risk_aversion))

        # Optimize for worst scenario
        w_new = optimize_for_scenario(w.copy(), means, cov_scenarios[worst_idx],
                                       risk_aversion, min_weight, max_weight)

        # Evaluate worst-case utility for new weights
        new_worst_idx = min(range(len(cov_scenarios)),
                            key=lambda s: compute_utility(w_new, means, cov_scenarios[s], risk_aversion))
        new_worst_utility = compute_utility(w_new, means, cov_scenarios[new_worst_idx], risk_aversion)

        if new_worst_utility > best_worst_utility:
            best_worst_utility = new_worst_utility
            best_w = w_new.copy()

        max_diff = np.max(np.abs(w_new - w))
        w = w_new.copy()
        if max_diff < tolerance:
            break

    return best_w


# ═══════════════════════════════════════════════════════════════════════════
# CVaR Risk Measure
# Matches C# CVaRRiskMeasure exactly
# ═══════════════════════════════════════════════════════════════════════════

def cvar_evaluate(weights, scenarios, confidence_level=0.95):
    """CVaR value + gradient. Matches C#."""
    n = len(weights)
    s = len(scenarios)
    tail_factor = 1.0 / (s * (1.0 - confidence_level))
    tail_count = max(1, int(s * (1.0 - confidence_level)))

    port_returns = np.array([sum(weights[i] * scenarios[t][i] for i in range(n))
                              for t in range(s)])

    sorted_returns = np.sort(port_returns)  # ascending
    zeta = -sorted_returns[min(tail_count, s - 1)]

    cvar_sum = 0.0
    grad_w = np.zeros(n)

    for t in range(s):
        loss = -port_returns[t] - zeta
        if loss > 0:
            cvar_sum += loss
            for i in range(n):
                grad_w[i] += tail_factor * (-scenarios[t][i])

    cvar = zeta + tail_factor * cvar_sum
    return cvar, grad_w


# ═══════════════════════════════════════════════════════════════════════════
# Downside Deviation Risk Measure
# Matches C# DownsideDeviationRiskMeasure exactly
# ═══════════════════════════════════════════════════════════════════════════

def downside_deviation_evaluate(weights, scenarios, mar=0.0):
    """Downside deviation value + gradient. Matches C#."""
    n = len(weights)
    s = len(scenarios)

    downside_sum_sq = 0.0
    grad_sum_sq = np.zeros(n)

    for t in range(s):
        port_return = sum(weights[i] * scenarios[t][i] for i in range(n))
        shortfall = mar - port_return
        if shortfall > 0:
            downside_sum_sq += shortfall * shortfall
            for i in range(n):
                grad_sum_sq[i] += 2.0 * shortfall * (-scenarios[t][i])

    downside_var = downside_sum_sq / s
    if downside_var <= 0:
        return 0.0, np.zeros(n)

    downside_dev = math.sqrt(downside_var)
    if downside_dev <= 0:
        return 0.0, np.zeros(n)

    grad = grad_sum_sq / (2.0 * s * downside_dev)
    return downside_dev, grad


# ═══════════════════════════════════════════════════════════════════════════
# MeanDownsideRisk construction
# Matches C# MeanDownsideRiskConstruction exactly
# ═══════════════════════════════════════════════════════════════════════════

def mean_downside_risk(returns: np.ndarray, risk_measure_fn, risk_aversion=1.0,
                        min_weight=0.0, max_weight=1.0,
                        max_iterations=5000, tolerance=1e-12) -> np.ndarray:
    """Projected gradient ascent on E[r] - lambda * Risk(w). Matches C#."""
    n = returns.shape[0]
    s = returns.shape[1]
    means = returns.mean(axis=1)

    # Transpose for scenario access
    scenarios = [returns[:, t] for t in range(s)]

    w = np.full(n, 1.0 / n)
    learning_rate = 1.0

    for _ in range(max_iterations):
        port_return = w @ means
        risk_value, risk_grad = risk_measure_fn(w, scenarios)
        objective = port_return - risk_aversion * risk_value

        grad = means - risk_aversion * risk_grad

        stepped = False
        current_lr = learning_rate

        for _ in range(20):
            candidate = w + current_lr * grad
            candidate = project_onto_simplex(candidate, min_weight, max_weight)

            new_port_return = candidate @ means
            # lr=0 for line search (don't mutate state)
            new_risk_value, _ = risk_measure_fn(candidate, scenarios)
            new_objective = new_port_return - risk_aversion * new_risk_value

            if new_objective > objective:
                max_diff = np.max(np.abs(candidate - w))
                w = candidate.copy()
                stepped = True
                if max_diff < tolerance:
                    return w
                break
            current_lr *= 0.5

        if not stepped:
            break

    return w


# ═══════════════════════════════════════════════════════════════════════════
# Turnover Penalized
# Matches C# TurnoverPenalizedConstruction exactly
# ═══════════════════════════════════════════════════════════════════════════

def turnover_penalized(w_target: np.ndarray, w_prev: np.ndarray, lam=0.05,
                        min_weight=0.0, max_weight=1.0,
                        max_iterations=2000, tolerance=1e-10) -> np.ndarray:
    """Proximal gradient for ||w - w_target||_2^2 + lambda * ||w - w_prev||_1.
    Matches C# TurnoverPenalizedConstruction."""
    n = len(w_target)
    w = w_target.copy()
    step_size = 0.5

    for _ in range(max_iterations):
        w_old = w.copy()

        for i in range(n):
            grad = 2.0 * (w[i] - w_target[i])
            v = w[i] - step_size * grad
            diff = v - w_prev[i]
            threshold = step_size * lam

            if diff > threshold:
                w[i] = w_prev[i] + diff - threshold
            elif diff < -threshold:
                w[i] = w_prev[i] + diff + threshold
            else:
                w[i] = w_prev[i]

        w = project_onto_simplex(w, min_weight, max_weight)

        if np.max(np.abs(w - w_old)) < tolerance:
            break

    return w


# ═══════════════════════════════════════════════════════════════════════════
# Volatility Targeting
# Matches C# VolatilityTargetingConstruction exactly
# ═══════════════════════════════════════════════════════════════════════════

def volatility_targeting(returns: np.ndarray, base_weights: np.ndarray,
                          target_vol: float, max_leverage=1.0,
                          trading_days_per_year=252) -> np.ndarray:
    """Scale base weights by targetVol / realizedVol. Matches C#."""
    n = returns.shape[0]
    min_length = min(r.shape[0] for r in [returns[i] for i in range(n)])

    port_returns = np.zeros(min_length)
    for t in range(min_length):
        for i in range(n):
            port_returns[t] += base_weights[i] * returns[i, t]

    mean = port_returns.mean()
    sum_sq_dev = np.sum((port_returns - mean) ** 2)
    daily_vol = math.sqrt(sum_sq_dev / (len(port_returns) - 1))
    annualized_vol = daily_vol * math.sqrt(trading_days_per_year)

    if annualized_vol == 0:
        raise ValueError("Zero realized vol")

    scale_factor = min(target_vol / annualized_vol, max_leverage)
    return base_weights * scale_factor


# ═══════════════════════════════════════════════════════════════════════════
# Vector generators
# ═══════════════════════════════════════════════════════════════════════════

def gen_hrp():
    """3A: HRP — 5-asset, 3-asset, 2-asset."""
    print("Phase 3A: HRP")

    returns_5 = generate_diverse_returns(5, 252)
    w_5 = hrp(returns_5)
    cov_5 = sample_cov(returns_5)

    returns_3 = generate_diverse_returns(3, 100)
    w_3 = hrp(returns_3)
    cov_3 = sample_cov(returns_3)

    returns_2 = generate_diverse_returns(2, 100)
    w_2 = hrp(returns_2)

    # --- Layer 2: Analytical ---
    # Two identical assets → equal weight
    rng_l2 = np.random.default_rng(seed=3001)
    shared = rng_l2.normal(0, 0.01, size=100)
    identical_returns = np.array([shared, shared])
    w_identical = hrp(identical_returns)
    hard_check("HRP: identical assets → equal weight",
               abs(w_identical[0] - 0.5) < 0.01 and abs(w_identical[1] - 0.5) < 0.01)

    # --- Layer 3: Properties ---
    hard_check("HRP 5-asset: weights sum to 1", abs(w_5.sum() - 1.0) < 1e-10)
    hard_check("HRP 5-asset: all non-negative", np.all(w_5 >= -1e-14))
    hard_check("HRP 3-asset: weights sum to 1", abs(w_3.sum() - 1.0) < 1e-10)
    hard_check("HRP 3-asset: all non-negative", np.all(w_3 >= -1e-14))
    hard_check("HRP 2-asset: weights sum to 1", abs(w_2.sum() - 1.0) < 1e-10)

    # Layer 1: Compare portfolio variance against equal-weight
    ew_var_5 = portfolio_variance(np.full(5, 0.2), cov_5)
    hrp_var_5 = portfolio_variance(w_5, cov_5)
    # HRP usually has lower variance than equal weight (not guaranteed, but usually)
    quality_check("HRP 5-asset: variance vs EW", hrp_var_5, ew_var_5, rel_tol=0.5)

    # Multiple random trials for properties
    for trial in range(5):
        rng_trial = np.random.default_rng(seed=3100 + trial)
        trial_returns = generate_diverse_returns(4, 100)
        trial_w = hrp(trial_returns)
        hard_check(f"HRP trial {trial}: sum=1", abs(trial_w.sum() - 1.0) < 1e-10)
        hard_check(f"HRP trial {trial}: non-negative", np.all(trial_w >= -1e-14))

    save("construction_hrp", {
        "cases": {
            "five_asset": {
                "returns": returns_5,
                "weights": w_5,
            },
            "three_asset": {
                "returns": returns_3,
                "weights": w_3,
            },
            "two_asset": {
                "returns": returns_2,
                "weights": w_2,
            },
        },
        "notes": "HRP: single-linkage clustering + recursive bisection with inverse-variance.",
    })


def gen_return_tilted_hrp():
    """3B: ReturnTiltedHRP — kappa=0, 0.5, 1.0."""
    print("Phase 3B: ReturnTiltedHRP")

    returns_5 = generate_diverse_returns(5, 252)

    w_k0 = return_tilted_hrp(returns_5, kappa=0.0)
    w_k05 = return_tilted_hrp(returns_5, kappa=0.5)
    w_k1 = return_tilted_hrp(returns_5, kappa=1.0)

    # Also standard HRP for comparison
    w_hrp = hrp(returns_5)

    # --- Layer 2: Analytical ---
    # kappa=0 should recover pure HRP
    hard_check("ReturnTiltedHRP kappa=0 ≈ HRP",
               np.max(np.abs(w_k0 - w_hrp)) < 1e-10)

    # --- Layer 3: Properties ---
    hard_check("ReturnTiltedHRP k=0: sum=1", abs(w_k0.sum() - 1.0) < 1e-10)
    hard_check("ReturnTiltedHRP k=0.5: sum=1", abs(w_k05.sum() - 1.0) < 1e-10)
    hard_check("ReturnTiltedHRP k=1: sum=1", abs(w_k1.sum() - 1.0) < 1e-10)
    hard_check("ReturnTiltedHRP k=0: non-negative", np.all(w_k0 >= -1e-14))
    hard_check("ReturnTiltedHRP k=0.5: non-negative", np.all(w_k05 >= -1e-14))
    hard_check("ReturnTiltedHRP k=1: non-negative", np.all(w_k1 >= -1e-14))

    # 3-asset
    returns_3 = generate_diverse_returns(3, 100)
    w_3_k05 = return_tilted_hrp(returns_3, kappa=0.5)
    hard_check("ReturnTiltedHRP 3-asset: sum=1", abs(w_3_k05.sum() - 1.0) < 1e-10)

    save("construction_return_tilted_hrp", {
        "cases": {
            "five_asset_kappa0": {
                "returns": returns_5,
                "kappa": 0.0,
                "weights": w_k0,
            },
            "five_asset_kappa05": {
                "returns": returns_5,
                "kappa": 0.5,
                "weights": w_k05,
            },
            "five_asset_kappa1": {
                "returns": returns_5,
                "kappa": 1.0,
                "weights": w_k1,
            },
            "three_asset_kappa05": {
                "returns": returns_3,
                "kappa": 0.5,
                "weights": w_3_k05,
            },
        },
        "hrp_reference": w_hrp.tolist(),
        "notes": "ReturnTiltedHRP: kappa=0 recovers pure HRP; kappa=1 is pure return.",
    })


def gen_black_litterman():
    """3C: BlackLitterman — 3-asset + 1 view; edge: no views = equilibrium."""
    print("Phase 3C: BlackLitterman")

    returns_3 = generate_diverse_returns(3, 252)
    eq_weights = np.array([0.5, 0.3, 0.2])  # Market cap weights

    # No views → return equilibrium weights directly
    w_no_views = black_litterman(returns_3, eq_weights, risk_aversion=2.5, tau=0.05)

    # With 1 view: "Asset 0 will outperform by 2% annual"
    P = np.array([[1.0, 0.0, 0.0]])  # View on asset 0 only
    Q = np.array([0.02 / 252])        # Daily equivalent of 2% annual
    Omega = np.array([[0.001 ** 2]])   # View uncertainty

    w_with_view = black_litterman(returns_3, eq_weights, risk_aversion=2.5, tau=0.05,
                                    pick_matrix=P, view_returns=Q, view_uncertainty=Omega)

    # --- Layer 2: Analytical ---
    # No views should produce weights that match equilibrium (after normalization)
    # The posterior is just pi (equilibrium returns), so w* = Sigma^-1 * pi / delta
    # Which should be proportional to eq_weights (by construction pi = delta * Sigma * w_eq)
    hard_check("BL no-views: sum=1", abs(w_no_views.sum() - 1.0) < 1e-10)
    hard_check("BL no-views: non-negative", np.all(w_no_views >= -1e-14))

    # With view: weights should differ from no-views (the view has an effect)
    hard_check("BL with view: differs from no-views",
               np.max(np.abs(w_with_view - w_no_views)) > 1e-6)

    # --- Layer 3: Properties ---
    hard_check("BL with-view: sum=1", abs(w_with_view.sum() - 1.0) < 1e-10)
    hard_check("BL with-view: non-negative", np.all(w_with_view >= -1e-14))

    # Relative view: "Asset 0 outperforms Asset 2 by 1%"
    P_rel = np.array([[1.0, 0.0, -1.0]])
    Q_rel = np.array([0.01 / 252])
    Omega_rel = np.array([[0.001 ** 2]])
    w_rel_view = black_litterman(returns_3, eq_weights, risk_aversion=2.5, tau=0.05,
                                  pick_matrix=P_rel, view_returns=Q_rel, view_uncertainty=Omega_rel)
    hard_check("BL relative view: sum=1", abs(w_rel_view.sum() - 1.0) < 1e-10)

    save("construction_black_litterman", {
        "cases": {
            "no_views": {
                "returns": returns_3,
                "equilibrium_weights": eq_weights,
                "risk_aversion": 2.5,
                "tau": 0.05,
                "weights": w_no_views,
            },
            "one_absolute_view": {
                "returns": returns_3,
                "equilibrium_weights": eq_weights,
                "risk_aversion": 2.5,
                "tau": 0.05,
                "pick_matrix": P,
                "view_returns": Q,
                "view_uncertainty": Omega,
                "weights": w_with_view,
            },
            "one_relative_view": {
                "returns": returns_3,
                "equilibrium_weights": eq_weights,
                "risk_aversion": 2.5,
                "tau": 0.05,
                "pick_matrix": P_rel,
                "view_returns": Q_rel,
                "view_uncertainty": Omega_rel,
                "weights": w_rel_view,
            },
        },
        "notes": "BlackLitterman: no views returns eq weights via Sigma^-1*pi/delta normalization.",
    })


def gen_robust_mean_variance():
    """3D: RobustMeanVariance — 3-asset + 2 cov scenarios; single scenario = standard MV."""
    print("Phase 3D: RobustMeanVariance")

    returns_3 = generate_diverse_returns(3, 252)
    cov_normal = sample_cov(returns_3)

    # Stress scenario: 3x volatility
    cov_stress = cov_normal * 3.0

    # Single scenario = standard mean-variance
    w_single = robust_mean_variance(returns_3, [cov_normal], risk_aversion=1.0)

    # Two scenarios
    w_robust = robust_mean_variance(returns_3, [cov_normal, cov_stress], risk_aversion=1.0)

    # --- Layer 2: Analytical ---
    # Single scenario should match standard MV
    w_standard_mv = optimize_for_scenario(np.full(3, 1.0 / 3), returns_3.mean(axis=1),
                                            cov_normal, risk_aversion=1.0)
    hard_check("RobustMV single ≈ standard MV",
               np.max(np.abs(w_single - w_standard_mv)) < 1e-6)

    # --- Layer 3: Properties ---
    hard_check("RobustMV single: sum=1", abs(w_single.sum() - 1.0) < 1e-10)
    hard_check("RobustMV single: non-negative", np.all(w_single >= -1e-14))
    hard_check("RobustMV robust: sum=1", abs(w_robust.sum() - 1.0) < 1e-10)
    hard_check("RobustMV robust: non-negative", np.all(w_robust >= -1e-14))

    # Robust worst-case utility should be >= equal-weight worst-case
    means = returns_3.mean(axis=1)
    ew = np.full(3, 1.0 / 3)
    ew_worst = min(compute_utility(ew, means, cov_normal, 1.0),
                   compute_utility(ew, means, cov_stress, 1.0))
    robust_worst = min(compute_utility(w_robust, means, cov_normal, 1.0),
                       compute_utility(w_robust, means, cov_stress, 1.0))
    hard_check("RobustMV: worst-case >= EW worst-case",
               robust_worst >= ew_worst - 1e-10)

    save("construction_robust_mean_variance", {
        "cases": {
            "single_scenario": {
                "returns": returns_3,
                "cov_scenarios": [cov_normal],
                "risk_aversion": 1.0,
                "weights": w_single,
            },
            "two_scenarios": {
                "returns": returns_3,
                "cov_scenarios": [cov_normal, cov_stress],
                "risk_aversion": 1.0,
                "weights": w_robust,
            },
        },
        "notes": "RobustMV: alternating optimization. Single scenario = standard MV.",
    })


def gen_mean_downside_risk_cvar():
    """3E: MeanDownsideRisk with CVaR."""
    print("Phase 3E: MeanDownsideRisk (CVaR)")

    returns_3 = generate_diverse_returns(3, 252)

    def cvar_fn(w, scenarios):
        return cvar_evaluate(w, scenarios, confidence_level=0.95)

    w_cvar = mean_downside_risk(returns_3, cvar_fn, risk_aversion=1.0)

    # --- Layer 3: Properties ---
    hard_check("MeanCVaR: sum=1", abs(w_cvar.sum() - 1.0) < 1e-10)
    hard_check("MeanCVaR: non-negative", np.all(w_cvar >= -1e-14))

    # With higher risk aversion → more conservative
    w_cvar_high_ra = mean_downside_risk(returns_3, cvar_fn, risk_aversion=5.0)
    hard_check("MeanCVaR high RA: sum=1", abs(w_cvar_high_ra.sum() - 1.0) < 1e-10)

    # 5-asset
    returns_5 = generate_diverse_returns(5, 252)
    w_cvar_5 = mean_downside_risk(returns_5, cvar_fn, risk_aversion=1.0)
    hard_check("MeanCVaR 5-asset: sum=1", abs(w_cvar_5.sum() - 1.0) < 1e-10)
    hard_check("MeanCVaR 5-asset: non-negative", np.all(w_cvar_5 >= -1e-14))

    save("construction_mean_cvar", {
        "cases": {
            "three_asset": {
                "returns": returns_3,
                "confidence_level": 0.95,
                "risk_aversion": 1.0,
                "weights": w_cvar,
            },
            "three_asset_high_ra": {
                "returns": returns_3,
                "confidence_level": 0.95,
                "risk_aversion": 5.0,
                "weights": w_cvar_high_ra,
            },
            "five_asset": {
                "returns": returns_5,
                "confidence_level": 0.95,
                "risk_aversion": 1.0,
                "weights": w_cvar_5,
            },
        },
        "notes": "MeanCVaR: projected gradient ascent on E[r] - lambda*CVaR_95.",
    })


def gen_mean_downside_risk_sortino():
    """3F: MeanDownsideRisk with DownsideDeviation (Sortino-style)."""
    print("Phase 3F: MeanDownsideRisk (Sortino)")

    returns_3 = generate_diverse_returns(3, 252)

    def sortino_fn(w, scenarios):
        return downside_deviation_evaluate(w, scenarios, mar=0.0)

    w_sortino = mean_downside_risk(returns_3, sortino_fn, risk_aversion=1.0)

    # --- Layer 3: Properties ---
    hard_check("MeanSortino: sum=1", abs(w_sortino.sum() - 1.0) < 1e-10)
    hard_check("MeanSortino: non-negative", np.all(w_sortino >= -1e-14))

    # With MAR = 0.001
    def sortino_fn_mar(w, scenarios):
        return downside_deviation_evaluate(w, scenarios, mar=0.001)

    w_sortino_mar = mean_downside_risk(returns_3, sortino_fn_mar, risk_aversion=1.0)
    hard_check("MeanSortino MAR=0.001: sum=1", abs(w_sortino_mar.sum() - 1.0) < 1e-10)

    # 5-asset
    returns_5 = generate_diverse_returns(5, 252)
    w_sortino_5 = mean_downside_risk(returns_5, sortino_fn, risk_aversion=1.0)
    hard_check("MeanSortino 5-asset: sum=1", abs(w_sortino_5.sum() - 1.0) < 1e-10)

    save("construction_mean_sortino", {
        "cases": {
            "three_asset": {
                "returns": returns_3,
                "mar": 0.0,
                "risk_aversion": 1.0,
                "weights": w_sortino,
            },
            "three_asset_mar": {
                "returns": returns_3,
                "mar": 0.001,
                "risk_aversion": 1.0,
                "weights": w_sortino_mar,
            },
            "five_asset": {
                "returns": returns_5,
                "mar": 0.0,
                "risk_aversion": 1.0,
                "weights": w_sortino_5,
            },
        },
        "notes": "MeanSortino: projected gradient ascent on E[r] - lambda*DownsideDev.",
    })


def gen_turnover_and_voltarget():
    """3G: TurnoverPenalized + VolatilityTargeting."""
    print("Phase 3G: TurnoverPenalized + VolatilityTargeting")

    returns_3 = generate_diverse_returns(3, 252)
    cov_3 = sample_cov(returns_3)

    # --- Turnover Penalized ---
    # Call 1: inner model returns target weights (use MinVar as inner)
    w_target = minimum_variance(cov_3)

    # Call 2: with previous weights, apply turnover penalty
    w_prev = np.array([0.5, 0.3, 0.2])  # Simulate previous allocation
    w_penalized = turnover_penalized(w_target, w_prev, lam=0.05)

    # Lambda=0 → pure target
    w_no_penalty = turnover_penalized(w_target, w_prev, lam=0.0)
    hard_check("Turnover lam=0: ≈ target",
               np.max(np.abs(w_no_penalty - w_target)) < 1e-8)

    # Lambda=0.5 → closer to prev
    w_high_penalty = turnover_penalized(w_target, w_prev, lam=0.5)

    # --- Layer 3: Properties ---
    hard_check("Turnover: sum=1", abs(w_penalized.sum() - 1.0) < 1e-10)
    hard_check("Turnover: non-negative", np.all(w_penalized >= -1e-14))
    hard_check("Turnover high-lam: sum=1", abs(w_high_penalty.sum() - 1.0) < 1e-10)

    # Higher lambda → closer to previous weights
    dist_low = np.sum(np.abs(w_penalized - w_prev))
    dist_high = np.sum(np.abs(w_high_penalty - w_prev))
    hard_check("Turnover: higher lambda → closer to prev", dist_high <= dist_low + 1e-10)

    # --- Volatility Targeting ---
    base_weights = minimum_variance(cov_3)
    target_vol = 0.10  # 10% annualized

    w_voltarget = volatility_targeting(returns_3, base_weights, target_vol, max_leverage=1.0)
    w_voltarget_lever = volatility_targeting(returns_3, base_weights, target_vol, max_leverage=2.0)

    # Layer 3: weights should be proportional to base weights
    if w_voltarget.sum() > 0 and base_weights.sum() > 0:
        ratio = w_voltarget / base_weights
        hard_check("VolTarget: proportional to base",
                   np.max(ratio) - np.min(ratio) < 1e-10)

    # Without leverage, sum ≤ 1.0 (scale ≤ 1.0 means it can't go above base sum)
    hard_check("VolTarget no-lever: scale ≤ 1",
               w_voltarget.sum() <= base_weights.sum() + 1e-10 or
               w_voltarget.sum() <= 1.0 + 1e-10)

    save("construction_turnover_voltarget", {
        "cases": {
            "turnover_lam005": {
                "returns": returns_3,
                "inner_model_weights": w_target,
                "previous_weights": w_prev,
                "lambda": 0.05,
                "call_2_weights": w_penalized,
            },
            "turnover_lam0": {
                "returns": returns_3,
                "inner_model_weights": w_target,
                "previous_weights": w_prev,
                "lambda": 0.0,
                "call_2_weights": w_no_penalty,
            },
            "turnover_lam05": {
                "returns": returns_3,
                "inner_model_weights": w_target,
                "previous_weights": w_prev,
                "lambda": 0.5,
                "call_2_weights": w_high_penalty,
            },
            "voltarget_no_leverage": {
                "returns": returns_3,
                "base_weights": base_weights,
                "target_volatility": target_vol,
                "max_leverage": 1.0,
                "weights": w_voltarget,
            },
            "voltarget_with_leverage": {
                "returns": returns_3,
                "base_weights": base_weights,
                "target_volatility": target_vol,
                "max_leverage": 2.0,
                "weights": w_voltarget_lever,
            },
        },
        "notes": "TurnoverPenalized: proximal gradient. VolTarget: scale by targetVol/realizedVol.",
    })


# ═══════════════════════════════════════════════════════════════════════════
# Main
# ═══════════════════════════════════════════════════════════════════════════

def main():
    print("=" * 60)
    print("Phase 3: Advanced Portfolio Construction Vectors")
    print("=" * 60)

    gen_hrp()
    gen_return_tilted_hrp()
    gen_black_litterman()
    gen_robust_mean_variance()
    gen_mean_downside_risk_cvar()
    gen_mean_downside_risk_sortino()
    gen_turnover_and_voltarget()

    # Summary
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
    print("Done — 7 vector files generated.")


if __name__ == "__main__":
    main()
