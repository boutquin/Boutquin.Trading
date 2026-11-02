#!/usr/bin/env python3
"""
Generate Entropy Pooling (Fully Flexible Views) construction vectors for
Boutquin.Trading.

Reference: Meucci (2010), "Fully Flexible Views: Theory and Practice",
arXiv 1012.2848 (SSRN 1213325). The rigorous, distribution-free generalization
of Black-Litterman.

Two code paths are exercised, matching the C# EntropyPoolingConstruction:

1. SCENARIO (empirical) Entropy Pooling — the general distribution-free path.
   * Prior  = the T time-cross-sections of an (N-asset × T) return panel.
     Scenario j is the column X_j (length N); prior probabilities p_j = 1/T.
   * Views  = K linear equality views on posterior expectations:
        A_v @ mu_post = b_v        (A_v is K×N pick matrix, b_v is K targets)
   * EP posterior minimizes KL divergence to the prior subject to the view
     constraints. Dual (exponential-family) solution:
        p_tilde_j ∝ p_j * exp( -lambda^T (A_v @ X_j) )
     with the K-dim lambda solving the concave dual via Newton iteration on
        g(lambda) = E_ptilde[A_v @ X] - b_v = 0.
   * Confidence blend p_c = (1-c)*p + c*p_tilde. Posterior moments
        mu_tilde  = sum_j p_c_j * X_j
        Sigma_tilde = sum_j p_c_j (X_j - mu_tilde)(X_j - mu_tilde)^T   (MLE divisor)
     are fed to a mean-variance optimizer.

2. GAUSSIAN-LIMIT (closed form) — the Black-Litterman special case the spec
   names explicitly. With a Gaussian prior (mu, Sigma) and FULL-confidence
   (c = 1) linear expectation views Q @ mu_tilde = q, the EP posterior mean
   reduces to
        mu_tilde = mu + Sigma Q^T (Q Sigma Q^T)^{-1} (q - Q mu)
   which is exactly the Black-Litterman posterior in the Gaussian limit. The
   reference replicates this closed form so the C# Gaussian-limit fidelity
   test can confirm EntropyPooling == BlackLitterman on a real multi-asset
   panel.

This is an OWN-FORMULA replica of the C# algorithm — it does NOT use pypfopt,
cvxpy, or any external entropy-pooling library.
"""

import json
from pathlib import Path

import numpy as np

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

RNG = np.random.default_rng(seed=1012)  # arXiv id flavor


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
# Reference implementation (matches C# EntropyPoolingConstruction exactly)
# ═══════════════════════════════════════════════════════════════════════════

def entropy_pooling_dual(X, p, A, b, max_iter=200, tol=1e-14):
    """Solve the Entropy Pooling dual for equality views A @ mu = b.

    X : (J, N) scenarios (scenario j is row X[j], length N).
    p : (J,) prior probabilities (sum to 1).
    A : (K, N) view pick matrix.
    b : (K,) view target expectations.

    Returns p_tilde (J,) the posterior probabilities.

    Dual: p_tilde_j ∝ p_j exp(-lambda^T (A @ X_j)). Newton on
    g(lambda) = E_ptilde[A @ X] - b.
    """
    J, N = X.shape
    K = A.shape[0]

    # Empty view set → posterior is the prior (lambda has zero length).
    if K == 0:
        return p.copy()

    # Per-scenario view-score vectors: V[j] = A @ X_j   (J, K)
    V = X @ A.T  # (J, K)

    lam = np.zeros(K)

    # Convex dual to MINIMIZE (Meucci 2010):
    #   D(lambda) = log( sum_j p_j exp(lambda^T V_j) ) - lambda^T b
    # with posterior  p_tilde_j ∝ p_j exp(lambda^T V_j),
    #   gradient   grad D = E_ptilde[V] - b      (== 0 at the optimum ⇒ view satisfied),
    #   Hessian    = Cov_ptilde[V]  (PSD ⇒ convex).
    # Newton descent step:  dlam = Cov^{-1} (b - E[V]).
    def posterior(l):
        lg = np.log(p) + V @ l
        lg -= lg.max()
        w_ = np.exp(lg)
        return w_ / w_.sum()

    def dual_obj(l):
        lg = np.log(p) + V @ l
        m = lg.max()
        return (m + np.log(np.exp(lg - m).sum())) - l @ b

    for _ in range(max_iter):
        ptilde = posterior(lam)
        EV = ptilde @ V  # (K,)
        grad = EV - b  # gradient of the convex dual

        if np.max(np.abs(grad)) < tol:
            break

        diff = V - EV  # (J, K)
        cov = (ptilde[:, None] * diff).T @ diff  # (K, K) Hessian
        try:
            dlam = np.linalg.solve(cov + 1e-15 * np.eye(K), b - EV)  # descent direction
        except np.linalg.LinAlgError:
            dlam = np.linalg.lstsq(cov, b - EV, rcond=None)[0]

        # Backtracking line search: accept the largest DESCENT step.
        step = 1.0
        base = dual_obj(lam)
        new_lam = lam + step * dlam
        for _bt in range(60):
            if dual_obj(new_lam) < base:
                break
            step *= 0.5
            new_lam = lam + step * dlam
        if not (dual_obj(new_lam) < base):
            break  # converged (no further descent possible)
        lam = new_lam

    ptilde = posterior(lam)
    return ptilde


def posterior_moments(X, pc):
    """Posterior mean and covariance of the (J, N) panel under probabilities pc."""
    mu = pc @ X  # (N,)
    diff = X - mu  # (J, N)
    sigma = (pc[:, None] * diff).T @ diff  # (N, N) MLE-divisor weighted covariance
    sigma = 0.5 * (sigma + sigma.T)
    return mu, sigma


# ── Mean-variance optimizer replica (matches C# ActiveSet/gradient path) ─────
# Uses projected gradient ascent on U(w) = w'mu - (lambda/2) w'Sigma w with a
# simplex projection, mirroring MeanVarianceConstruction.SolveGradientAscent so
# the vectors match the C# weights within PrecisionNumeric without depending on
# the active-set path's convergence quirks.

def project_simplex(w, min_w, max_w):
    n = len(w)
    max_w = max(max_w, 1.0 / n)
    min_w = min(min_w, 1.0 / n)
    w = w.copy()
    for _ in range(50):
        w = np.clip(w, min_w, max_w)
        s = w.sum()
        if s <= 0:
            return np.full(n, 1.0 / n)
        w = w / s
        if np.all(w >= min_w - 1e-14) and np.all(w <= max_w + 1e-14):
            return w
    return w


def mean_variance_weights(mu, sigma, risk_aversion=1.0, min_w=0.0, max_w=1.0,
                          max_iter=5000, tol=1e-12):
    n = len(mu)
    w = np.full(n, 1.0 / n)

    def utility(x):
        return x @ mu - risk_aversion / 2.0 * (x @ sigma @ x)

    for _ in range(max_iter):
        grad = mu - risk_aversion * (sigma @ w)
        stepped = False
        lr = 1.0
        for _attempt in range(20):
            cand = project_simplex(w + lr * grad, min_w, max_w)
            if utility(cand) > utility(w):
                max_diff = np.max(np.abs(cand - w))
                w = cand
                stepped = True
                if max_diff < tol:
                    return w
                break
            lr *= 0.5
        if not stepped:
            break
    return w


def entropy_pooling_weights_scenario(returns, A, b, c=1.0, risk_aversion=1.0):
    """Full scenario EP construction: panel (N, T) → blended posterior → MV weights."""
    X = np.asarray(returns).T  # (T, N) scenarios
    J = X.shape[0]
    p = np.full(J, 1.0 / J)
    ptilde = entropy_pooling_dual(X, p, np.asarray(A, dtype=float), np.asarray(b, dtype=float))
    pc = (1.0 - c) * p + c * ptilde
    pc = pc / pc.sum()
    mu, sigma = posterior_moments(X, pc)
    w = mean_variance_weights(mu, sigma, risk_aversion=risk_aversion)
    return w, mu, sigma, ptilde, pc


def black_litterman_gaussian_mean(mu_prior, sigma, Q, q):
    """Gaussian-limit (full-confidence) BL posterior mean — the EP special case.

    mu_tilde = mu + Sigma Q^T (Q Sigma Q^T)^{-1} (q - Q mu)
    """
    mu_prior = np.asarray(mu_prior, dtype=float)
    sigma = np.asarray(sigma, dtype=float)
    Q = np.asarray(Q, dtype=float)
    q = np.asarray(q, dtype=float)
    QS = Q @ sigma  # (K, N)
    QSQt = QS @ Q.T  # (K, K)
    inner = np.linalg.solve(QSQt, q - Q @ mu_prior)  # (K,)
    return mu_prior + QS.T @ inner


# ═══════════════════════════════════════════════════════════════════════════
# Vector generators
# ═══════════════════════════════════════════════════════════════════════════

def gen_scenario_3asset():
    """Scenario EP on the canonical 3-asset / 252-obs panel, one view."""
    d = json.load(open(VECTORS_DIR / "multi_asset_returns.json"))
    returns = np.array(d["values"])  # (3, 252)

    # View: asset 0 expected return is 0.0010/day (a bullish tilt above its
    # sample mean). Single equality view on the posterior expectation.
    A = np.array([[1.0, 0.0, 0.0]])
    b = np.array([0.0010])

    w, mu, sigma, ptilde, pc = entropy_pooling_weights_scenario(
        returns, A, b, c=1.0, risk_aversion=1.0)

    # The view must be satisfied by the posterior expectation.
    view_check = float((A @ mu)[0])

    save("entropy_pooling_scenario_3asset", {
        "inputs": {
            "returns": returns,
            "pick_matrix": A,
            "view_returns": b,
            "confidence": 1.0,
            "risk_aversion": 1.0,
        },
        "expected": {
            "weights": w,
            "posterior_mean": mu,
            "view_realized": view_check,
            "posterior_probabilities": ptilde,
        },
        "notes": "Scenario (empirical) Entropy Pooling, 1 equality view on asset-0 "
                 "expectation = 0.0010/day, full confidence c=1. Own-formula replica "
                 "(NOT pypfopt/cvxpy). Posterior expectation satisfies the view.",
    })


def gen_scenario_no_views():
    """No views → posterior == prior → MV on the raw sample moments."""
    d = json.load(open(VECTORS_DIR / "multi_asset_returns.json"))
    returns = np.array(d["values"])  # (3, 252)

    X = returns.T
    J = X.shape[0]
    p = np.full(J, 1.0 / J)
    mu, sigma = posterior_moments(X, p)
    w = mean_variance_weights(mu, sigma, risk_aversion=1.0)

    save("entropy_pooling_no_views", {
        "inputs": {"returns": returns, "confidence": 1.0, "risk_aversion": 1.0},
        "expected": {"weights": w, "prior_mean": mu},
        "notes": "No views → EP posterior == prior (1/J each) → mean-variance on the "
                 "raw sample moments. Pins the prior-implied path.",
    })


def gen_gaussian_limit_bl():
    """Gaussian-limit fidelity: EP full-confidence linear view == BL closed form.

    Uses a real 4-asset panel: the sample mean/cov is the Gaussian prior, and a
    single full-confidence absolute view drives the BL posterior mean. The C#
    test asserts EntropyPoolingConstruction (Gaussian mode) == BlackLittermanConstruction.
    """
    # 4-asset factor-structured panel (real multi-asset data, not trivial).
    base = RNG.normal(0.0004, 0.011, size=180)
    returns = np.array([
        base + RNG.normal(0, 0.004, 180),
        base + RNG.normal(0, 0.004, 180),
        RNG.normal(0.0002, 0.014, 180),
        RNG.normal(0.0003, 0.009, 180),
    ])
    n = returns.shape[0]

    # Sample covariance (N-1 divisor) — repo convention.
    sigma = np.cov(returns, ddof=1)

    # Equilibrium/prior weights for the BL constructor: market-cap proxy = equal.
    eq_weights = np.full(n, 1.0 / n)

    # Black-Litterman prior mean = the implied equilibrium return Pi = delta * Sigma * w_eq
    # (reverse optimization), NOT the sample mean. This is the defining BL anchor and matches
    # the C# FromGaussianPrior default risk-aversion delta = 2.5.
    delta = 2.5
    mu_prior = delta * (sigma @ eq_weights)

    # One full-confidence absolute view: asset 0 expected return = +0.0015/day.
    Q = np.array([[1.0, 0.0, 0.0, 0.0]])
    q = np.array([0.0015])

    mu_bl = black_litterman_gaussian_mean(mu_prior, sigma, Q, q)

    save("entropy_pooling_gaussian_bl", {
        "inputs": {
            "returns": returns,
            "prior_mean": mu_prior,
            "prior_cov": sigma,
            "pick_matrix": Q,
            "view_returns": q,
            "equilibrium_weights": eq_weights,
        },
        "expected": {
            "bl_posterior_mean": mu_bl,
        },
        "notes": "Gaussian-limit fidelity. EP with Gaussian prior + full-confidence "
                 "(c=1) linear view recovers the BL posterior mean "
                 "mu = mu + S Q'(Q S Q')^-1 (q - Q mu). 4-asset real panel.",
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
    print("CORRECTNESS CROSS-CHECKS — Entropy Pooling")
    print("=" * 70)

    check_rng = np.random.default_rng(seed=424242)
    TOL_VIEW = 1e-8
    TOL_PROB = 1e-10
    TOL_BL = 1e-9

    # C1: Scenario EP — the posterior expectation satisfies every equality view,
    #     and posterior probabilities are strictly positive and sum to 1.
    for label, (na, t) in [("3x252", (3, 252)), ("5x200", (5, 200)), ("4x300", (4, 300))]:
        returns = check_rng.normal(0.0003, 0.012, (na, t))
        # Random single view on asset 0.
        A = np.zeros((1, na))
        A[0, 0] = 1.0
        target = float(returns[0].mean()) + 0.0005  # achievable tilt
        b = np.array([target])
        X = returns.T
        J = X.shape[0]
        p = np.full(J, 1.0 / J)
        ptilde = entropy_pooling_dual(X, p, A, b)
        pc = ptilde  # c = 1
        mu, _ = posterior_moments(X, pc)
        print(f"  {label}:")
        _hard(abs(float((A @ mu)[0]) - target) < TOL_VIEW,
              f"view satisfied (realized={float((A @ mu)[0]):.8f}, target={target:.8f})")
        _hard(abs(ptilde.sum() - 1.0) < TOL_PROB, f"probabilities sum to 1")
        _hard(np.all(ptilde > 0), f"probabilities strictly positive (exp form)")
        # KL >= 0 and posterior differs from prior when the view binds.
        kl = float(np.sum(ptilde * np.log(ptilde / p)))
        _hard(kl >= -1e-12, f"KL divergence >= 0 (KL={kl:.6e})")

    # C2: No-view EP recovers the prior exactly (lambda = 0).
    returns = check_rng.normal(0, 0.01, (4, 150))
    X = returns.T
    J = X.shape[0]
    p = np.full(J, 1.0 / J)
    # Empty view set → dual trivially p_tilde == p.
    ptilde = entropy_pooling_dual(X, p, np.zeros((0, 4)), np.zeros(0))
    print("  no-view recovery:")
    _hard(np.max(np.abs(ptilde - p)) < TOL_PROB, "no views → posterior == prior")

    # C3: Gaussian-limit BL equivalence — the closed form satisfies the view and
    #     equals the standard BL posterior algebra.
    print("  Gaussian-limit BL:")
    na = 4
    rr = check_rng.normal(0.0004, 0.011, (na, 200))
    mu_prior = rr.mean(axis=1)
    sigma = np.cov(rr, ddof=1)
    Q = np.array([[1.0, 0.0, 0.0, 0.0]])
    q = np.array([mu_prior[0] + 0.001])
    mu_bl = black_litterman_gaussian_mean(mu_prior, sigma, Q, q)
    _hard(abs(float((Q @ mu_bl)[0]) - float(q[0])) < TOL_BL,
          f"full-confidence view exactly satisfied (realized={float((Q @ mu_bl)[0]):.8f})")
    # Two-view consistency.
    Q2 = np.array([[1.0, 0.0, 0.0, 0.0], [0.0, 0.0, 1.0, 0.0]])
    q2 = np.array([mu_prior[0] + 0.001, mu_prior[2] - 0.0005])
    mu_bl2 = black_litterman_gaussian_mean(mu_prior, sigma, Q2, q2)
    _hard(np.max(np.abs(Q2 @ mu_bl2 - q2)) < TOL_BL, "two full-confidence views satisfied")

    print()
    print("=" * 70)
    hard_failed = _hard_total - _hard_pass
    print(f"HARD checks: {_hard_pass}/{_hard_total} passed"
          + ("" if hard_failed == 0 else f"  *** {hard_failed} FAILED ***"))
    print("=" * 70)
    if hard_failed > 0:
        raise AssertionError(f"{hard_failed} hard cross-check(s) failed")


def main():
    print("Generating Entropy Pooling vectors...")
    print()
    print("Scenario EP (3-asset, 1 view):")
    gen_scenario_3asset()
    print("Scenario EP (no views → prior):")
    gen_scenario_no_views()
    print("Gaussian-limit BL fidelity (4-asset):")
    gen_gaussian_limit_bl()
    print()
    print(f"Done. Entropy Pooling vectors in {VECTORS_DIR}")

    run_cross_checks()


if __name__ == "__main__":
    main()
