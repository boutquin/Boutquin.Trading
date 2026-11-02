#!/usr/bin/env python3
"""
Generate Sparse (ℓ₁-regularized) Minimum-Variance construction vectors for
Boutquin.Trading — Husmann, Shivarova & Steinert (2019), "Sparsity and Stability
for Minimum-Variance Portfolios", arXiv 1910.11840 — **Model 2 (LASSO)**.

Model 2 (LASSO):
    min_w  wᵀΣ̂w   s.t.   1ᵀw = 1,   ‖w‖₁ ≤ δ
Reformulated for QP via the augmented variables w = w⁺ − w⁻, w⁺,w⁻ ≥ 0, on the
[[Σ̂,−Σ̂],[−Σ̂,Σ̂]] objective (so xᵀQx = wᵀΣ̂w) with the gross-exposure (ℓ₁) budget δ.

This is an OWN-FORMULA replica of the C# SparseMinimumVarianceConstruction — it does
NOT use pypfopt, cvxpy, scikit-learn, or any external optimizer. The augmented-QP
optimum is computed from first principles so the C# cross-language tests verify
agreement at PrecisionNumeric (1e-6).

Determinism / exactness note
----------------------------
The single biggest cross-language divergence risk for a QP is the optimizer's
iteration path on an *active boundary*. To keep every vector reproducible to machine
precision in both languages, all vectors are constructed so the optimum is an
**analytic, closed-form solution** of its (fixed) active set:

  * Slack budget (δ ≥ ‖w_u‖₁, incl. δ = ∞): the budget does not bind and the optimum
    is the unconstrained sum-to-one min-var
        w_u = Σ⁻¹·1 / (1ᵀΣ⁻¹·1).
    The interior panels emit a δ in the slack region so w_u is the analytic target.

  * Binding budget with an interior sign pattern (1 < δ < ‖w_u‖₁ and the optimal sign
    vector s has every free coordinate strictly off its bound): the optimum solves the
    two-equality KKT system
        min wᵀΣw  s.t. 1ᵀw = 1,  sᵀw = δ
    in closed form. The Panel-B binding vector (δ = 1.3) has the unconstrained sign
    pattern, which is its own optimal sign pattern, so the closed-form is exact.

The defining fidelity reduction (δ = ∞ ⇒ standard min-var §13) and the long-only
corner (δ = 1) are asserted in C# as solver-to-solver equivalences against
MinimumVarianceConstruction (both call ActiveSetQpSolver), so they need no Python
boundary value and are not emitted here.

Reference: Husmann, Shivarova & Steinert (2019), arXiv 1910.11840 (Model 2).
"""

import json
from pathlib import Path

import numpy as np

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)


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
# Reference implementation (matches C# SparseMinimumVarianceConstruction)
# ═══════════════════════════════════════════════════════════════════════════

def sample_covariance(returns):
    """Sample covariance (N-1 divisor), matching SampleCovarianceEstimator.

    returns : (N, T) — one row per asset.
    """
    X = np.asarray(returns, dtype=float)
    _, t = X.shape
    centered = X - X.mean(axis=1, keepdims=True)
    return (centered @ centered.T) / (t - 1)


def unconstrained_minvar(cov):
    """Unconstrained (sum-to-one) min-var: w = Σ⁻¹·1 / (1ᵀΣ⁻¹·1)."""
    n = cov.shape[0]
    inv = np.linalg.inv(cov)
    ones = np.ones(n)
    z = inv @ ones
    return z / z.sum()


def two_equality_kkt(cov, sign, delta):
    """Closed-form optimum of  min wᵀΣw  s.t. 1ᵀw=1, sᵀw=δ.

    KKT: 2Σw = ν·1 + μ·s ; with the two linear equalities. Let p = Σ⁻¹·1,
    q = Σ⁻¹·s; then w = (ν·p + μ·q)/2 with [[1ᵀp,1ᵀq],[sᵀp,sᵀq]]·[ν;μ]/2 = [1;δ].
    """
    inv = np.linalg.inv(cov)
    ones = np.ones(cov.shape[0])
    p = inv @ ones
    q = inv @ sign
    mat = np.array([[ones @ p, ones @ q], [sign @ p, sign @ q]]) / 2.0
    lam = np.linalg.solve(mat, np.array([1.0, delta]))
    return (lam[0] * p + lam[1] * q) / 2.0


def sparse_minvar(cov, delta):
    """ℓ₁-budgeted (Model 2 LASSO) minimum variance for δ ≥ 1.

    Slack budget → unconstrained min-var. Binding budget with a stable sign
    pattern (seeded from the unconstrained signs) → two-equality KKT closed form.
    """
    n = cov.shape[0]
    if delta < 1.0 - 1e-12:
        raise ValueError("delta < 1 is infeasible for a long-only sum-to-one portfolio")

    w_u = unconstrained_minvar(cov)
    if delta >= np.abs(w_u).sum() - 1e-12:
        return w_u

    sign = np.sign(w_u)
    sign[sign == 0] = 1.0
    last = None
    for _ in range(8 * n + 8):
        w = two_equality_kkt(cov, sign, delta)
        new_sign = np.sign(w)
        new_sign[new_sign == 0] = 1.0
        if np.array_equal(new_sign, sign):
            return w
        last = w
        sign = new_sign
    return last


# ═══════════════════════════════════════════════════════════════════════════
# Panels
# ═══════════════════════════════════════════════════════════════════════════

def make_block_panel(cov, t, seed):
    """Synthesize a (N×T) panel whose sample covariance ≈ cov (for AC-1/AC-2
    panels that go through the real SampleCovarianceEstimator path). The numeric
    vectors below instead inject the covariance directly to remove sampling noise.
    """
    rng = np.random.default_rng(seed)
    n = cov.shape[0]
    chol = np.linalg.cholesky(cov)
    z = rng.standard_normal((n, t))
    return chol @ z


# ═══════════════════════════════════════════════════════════════════════════
# Vector 1 — slack budget on an interior panel: optimum = analytic unconstrained
#            min-var (δ ≥ gross). This is the AC-2 cross-language numeric anchor.
# ═══════════════════════════════════════════════════════════════════════════

def vector_interior_slack():
    vols = np.array([0.09, 0.13, 0.17, 0.11])
    corr = np.array([
        [1.00, 0.25, 0.15, 0.30],
        [0.25, 1.00, 0.20, 0.10],
        [0.15, 0.20, 1.00, 0.18],
        [0.30, 0.10, 0.18, 1.00],
    ])
    cov = np.outer(vols, vols) * corr
    delta = 5.0  # well above gross (≈1.0); budget is slack → unconstrained min-var
    w = sparse_minvar(cov, delta)

    save("sparse_minvar_interior_slack", {
        "description": "Slack ℓ₁ budget (δ=5 ≥ gross): optimum = analytic unconstrained min-var.",
        "reference": "Husmann, Shivarova & Steinert (2019), arXiv 1910.11840, Model 2.",
        "inputs": {
            "covariance": cov.tolist(),
            "delta": delta,
        },
        "expected": {
            "weights": w.tolist(),
            "weights_sum": float(w.sum()),
            "gross": float(np.abs(w).sum()),
        },
    })


# ═══════════════════════════════════════════════════════════════════════════
# Vector 2 — binding budget on Panel B (the unconstrained min-var goes short, so
#            the budget binds). δ=1.3 with the unconstrained sign pattern → exact
#            two-equality KKT. Proves the ℓ₁ budget genuinely binds (the AC-3
#            "binding δ" cross-language anchor).
# ═══════════════════════════════════════════════════════════════════════════

def vector_binding_budget():
    vols = np.array([0.10, 0.11, 0.18])
    corr = np.array([
        [1.00, 0.95, 0.10],
        [0.95, 1.00, 0.10],
        [0.10, 0.10, 1.00],
    ])
    cov = np.outer(vols, vols) * corr
    delta = 1.3
    w = sparse_minvar(cov, delta)
    w_u = unconstrained_minvar(cov)

    save("sparse_minvar_binding_budget", {
        "description": "Binding ℓ₁ budget (δ=1.3 < gross≈1.656): caps gross exposure.",
        "reference": "Husmann, Shivarova & Steinert (2019), arXiv 1910.11840, Model 2.",
        "inputs": {
            "covariance": cov.tolist(),
            "delta": delta,
        },
        "expected": {
            "weights": w.tolist(),
            "weights_sum": float(w.sum()),
            "gross": float(np.abs(w).sum()),
            "unconstrained_weights": w_u.tolist(),
            "unconstrained_gross": float(np.abs(w_u).sum()),
        },
    })


if __name__ == "__main__":
    print("Generating Sparse Minimum-Variance (Husmann Model 2) vectors...")
    vector_interior_slack()
    vector_binding_budget()
    print("Done.")
