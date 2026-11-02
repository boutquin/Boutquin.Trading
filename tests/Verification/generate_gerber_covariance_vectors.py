#!/usr/bin/env python3
"""
Generate Gerber covariance estimator vectors for Boutquin.Trading.

Reference: Gerber, Markowitz, Ernst, Miao, Javid & Sargen (2023),
"Proofs that the Gerber Statistic is Positive Semidefinite", arXiv 2305.05663
(companion to "The Gerber Statistic: A Robust Co-Movement Measure for Portfolio
Optimization", J. Portfolio Mgmt 48(3), 2022).

Ships the PSD-proven Gerber Statistic 2 variant:

  Threshold per asset    H_k = c * sigma_k          (canonical c = 1/2)
  Indicator matrices     U: u_tk = 1 if r_tk >= +H_k
                         D: d_tk = 1 if r_tk <= -H_k
  Concordant/discordant  N^UU = U'U,  N^DD = D'D,  N^UD = U'D,  N^DU = D'U
  Gerber-2 statistic     g2_ij = (N^UU + N^DD - N^UD - N^DU)_ij / (T - N^NN_ij)
                         N^NN_ij = <P_i, P_j>,  P_tk = 1 iff |r_tk| < H_k (within threshold)
                         => N^NN_ij = both-quiet count (both assets within threshold);
                            denominator T - N^NN_ij = #obs where at least one asset is large
                            (the UNION of large moves). This is the PSD-proven denominator
                            (Prop. 2: X = P'P / T is PSD), NOT the both-large intersection.
                         Diagonal forced to g2_ii = 1 (asset perfectly concordant with itself;
                            the removable-singularity limit when no obs is large for asset i).
  Rescale to covariance  Sigma_ij = g2_ij * sigma_i * sigma_j

sigma_k is the per-asset SAMPLE standard deviation (N-1 divisor), matching the
repo convention. This is an OWN-FORMULA replica of the C# algorithm — it does
NOT use any external Gerber library.
"""

import json
from pathlib import Path

import numpy as np

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

RNG = np.random.default_rng(seed=2305)  # arXiv id flavor


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
# Reference implementation (matches C# GerberCovarianceEstimator exactly)
# ═══════════════════════════════════════════════════════════════════════════

def sample_std(returns: np.ndarray) -> np.ndarray:
    """Per-asset sample standard deviation (N-1 divisor). returns: (n_assets, T)."""
    return np.std(returns, axis=1, ddof=1)


def gerber2_cov(returns: np.ndarray, c: float = 0.5) -> np.ndarray:
    """Gerber Statistic 2 covariance. returns shape: (n_assets, T).

    Returns the n×n covariance matrix Sigma_ij = g2_ij * sigma_i * sigma_j.
    """
    n, t = returns.shape
    sigma = sample_std(returns)  # (n,)
    thresh = c * sigma           # H_k, (n,)

    # Indicator matrices, shape (T, n): U[t,k]=1 if r_tk >= +H_k, D[t,k]=1 if r_tk <= -H_k
    # returns is (n, T); transpose to (T, n) for the t×k indexing of the paper.
    r = returns.T  # (T, n)
    u = (r >= thresh[np.newaxis, :]).astype(np.float64)   # (T, n)
    d = (r <= -thresh[np.newaxis, :]).astype(np.float64)  # (T, n)

    n_uu = u.T @ u  # (n, n)
    n_dd = d.T @ d
    n_ud = u.T @ d
    n_du = d.T @ u

    numerator = n_uu + n_dd - n_ud - n_du          # concordant - discordant = <F_i, F_j>

    # N^NN_ij = <P_i, P_j>, P_tk = 1 iff asset k is WITHIN its threshold (both-quiet).
    # P = 1 - U - D (U and D are mutually exclusive 0/1 indicators).
    p = 1.0 - u - d                                  # (T, n)
    n_nn = p.T @ p                                   # (n, n) both-quiet count
    denominator = t - n_nn                           # #obs where at least one asset is large

    g2 = np.zeros((n, n))
    for i in range(n):
        for j in range(n):
            if i == j:
                # Diagonal forced to 1 (asset perfectly concordant with itself), matching
                # the C# forced g2_ii = 1: the removable-singularity limit holds even when
                # no observation is large for asset i (denominator 0).
                g2[i, j] = 1.0
                continue
            denom = denominator[i, j]
            # denom == 0 ⇒ no observation had either asset large ⇒ no co-movement signal.
            g2[i, j] = numerator[i, j] / denom if denom != 0 else 0.0

    cov = g2 * np.outer(sigma, sigma)
    # Symmetrize against floating dust (matrix is symmetric by construction).
    cov = 0.5 * (cov + cov.T)
    return cov, g2, sigma


# ═══════════════════════════════════════════════════════════════════════════
# Vector generators
# ═══════════════════════════════════════════════════════════════════════════

def gen_main_3asset():
    """Reuse the canonical 3-asset / 252-obs panel for a happy-path vector."""
    d = json.load(open(VECTORS_DIR / "multi_asset_returns.json"))
    returns = np.array(d["values"])  # (3, 252)

    cov, g2, sigma = gerber2_cov(returns, c=0.5)

    save("gerber_covariance_3asset", {
        "inputs": {"returns": returns, "c": 0.5},
        "expected": {
            "gerber_covariance": cov,
            "gerber_statistic": g2,
            "sample_std": sigma,
        },
        "notes": "Gerber Statistic 2, c=0.5, canonical 3-asset/252-obs panel. "
                 "Own-formula replica (NOT an external Gerber library).",
    })


def gen_5asset_factor():
    """5-asset panel with clear factor structure → representative PSD test."""
    base = RNG.normal(0.0003, 0.012, size=200)
    returns = np.array([
        base + RNG.normal(0, 0.003, 200),  # correlated cluster
        base + RNG.normal(0, 0.003, 200),
        base + RNG.normal(0, 0.003, 200),
        RNG.normal(0.0001, 0.015, 200),    # independent
        RNG.normal(0.0002, 0.008, 200),    # independent
    ])

    cov, g2, sigma = gerber2_cov(returns, c=0.5)
    eigenvalues = np.sort(np.linalg.eigvalsh(cov))[::-1]

    save("gerber_covariance_5asset", {
        "inputs": {"returns": returns, "c": 0.5},
        "expected": {
            "gerber_covariance": cov,
            "gerber_statistic": g2,
            "sample_std": sigma,
            "eigenvalues_descending": eigenvalues,
            "min_eigenvalue": float(eigenvalues[-1]),
        },
        "notes": "5-asset factor-structured panel, Gerber-2, c=0.5. min_eigenvalue ≥ 0 ⇒ PSD.",
    })


def gen_c_variant():
    """Same 3-asset data but c=0.25 (tighter threshold) to pin the c parameter."""
    d = json.load(open(VECTORS_DIR / "multi_asset_returns.json"))
    returns = np.array(d["values"])  # (3, 252)

    cov, g2, sigma = gerber2_cov(returns, c=0.25)

    save("gerber_covariance_c025", {
        "inputs": {"returns": returns, "c": 0.25},
        "expected": {
            "gerber_covariance": cov,
            "gerber_statistic": g2,
            "sample_std": sigma,
        },
        "notes": "Gerber Statistic 2 with c=0.25 (tighter threshold). Pins the c constructor parameter.",
    })


# ═══════════════════════════════════════════════════════════════════════════
# Correctness cross-checks
# ═══════════════════════════════════════════════════════════════════════════

_hard_total = 0
_hard_pass = 0


def _hard(condition: bool, label: str):
    global _hard_total, _hard_pass
    _hard_total += 1
    if condition:
        _hard_pass += 1
        print(f"    [PASS] {label}")
    else:
        print(f"    [HARD FAIL] {label}")


def run_cross_checks():
    global _hard_total, _hard_pass
    _hard_total = _hard_pass = 0

    print()
    print("=" * 70)
    print("CORRECTNESS CROSS-CHECKS — Gerber Statistic 2")
    print("=" * 70)

    check_rng = np.random.default_rng(seed=909090)
    TOL_EXACT = 1e-10
    TOL_PSD = 1e-9

    for label, returns in [
        ("3x200", check_rng.normal(0, 0.012, (3, 200))),
        ("5x150", check_rng.normal(0, 0.015, (5, 150))),
        ("2x500", check_rng.normal(0, 0.01, (2, 500))),
        ("8x300 factor", None),
    ]:
        if returns is None:
            base = check_rng.normal(0, 0.012, 300)
            returns = np.array(
                [base + check_rng.normal(0, 0.004, 300) for _ in range(4)]
                + [check_rng.normal(0, 0.01, 300) for _ in range(4)]
            )
        cov, g2, sigma = gerber2_cov(returns, c=0.5)
        n = returns.shape[0]
        print(f"  {label}:")

        # P1: Symmetry
        asym = np.max(np.abs(cov - cov.T))
        _hard(asym < TOL_EXACT, f"symmetric (max_asym={asym:.2e})")

        # P2: Gerber statistic diagonal == 1 (every obs is a large move against itself)
        diag_g2 = np.diag(g2)
        _hard(np.max(np.abs(diag_g2 - 1.0)) < TOL_EXACT,
              f"g2 diagonal == 1 (max_dev={np.max(np.abs(diag_g2 - 1.0)):.2e})")

        # P3: Rescale identity Sigma_ii == sigma_i^2
        var = sigma ** 2
        diag_dev = np.max(np.abs(np.diag(cov) - var))
        _hard(diag_dev < TOL_EXACT, f"Sigma_ii == sigma_i^2 (max_dev={diag_dev:.2e})")

        # P4: |g2| <= 1 (correlation-like statistic bounded)
        _hard(np.max(np.abs(g2)) <= 1.0 + TOL_EXACT,
              f"|g2| <= 1 (max={np.max(np.abs(g2)):.6f})")

        # P5: PSD — all eigenvalues >= -tol (Prop. 2)
        eigs = np.linalg.eigvalsh(cov)
        _hard(eigs[0] >= -TOL_PSD, f"PSD (min eigenvalue={eigs[0]:.3e})")

    print()
    print("=" * 70)
    hard_failed = _hard_total - _hard_pass
    print(f"HARD checks: {_hard_pass}/{_hard_total} passed"
          + ("" if hard_failed == 0 else f"  *** {hard_failed} FAILED ***"))
    print("=" * 70)
    if hard_failed > 0:
        raise AssertionError(f"{hard_failed} hard cross-check(s) failed")


def main():
    print("Generating Gerber covariance vectors...")
    print()
    print("3-asset (canonical panel):")
    gen_main_3asset()
    print("5-asset (factor structure):")
    gen_5asset_factor()
    print("c=0.25 variant:")
    gen_c_variant()
    print()
    print(f"Done. Gerber covariance vectors in {VECTORS_DIR}")

    run_cross_checks()


if __name__ == "__main__":
    main()
