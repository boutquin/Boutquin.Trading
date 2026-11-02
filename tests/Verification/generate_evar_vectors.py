#!/usr/bin/env python3
"""
Generate cross-language verification vectors for the Entropic Value-at-Risk (EVaR)
downside risk measure.

Milestone M-7 (build-arena 2026-06-23). Component: EVaR — value + gradient.

Ground truth: Ahmadi-Javid (2012), restated/confirmed coherent by Delbaen (2015)
arXiv 1504.00640.

Finite-sample primal (X = loss = -portfolio return):

    EVaR_{1-alpha}(w) = inf_{z>0} z^{-1} * [ ln( (1/J) * sum_j exp(z * loss_j) ) - ln(alpha) ]

The objective g(z) is unimodal in z (the MGF is log-convex), so the optimal z* is
found by GOLDEN-SECTION search over a bracket [z_lo, z_hi]. A log-sum-exp shift by
max_j(z * loss_j) guards against exp overflow.

Gradient at z*:

    dEVaR/dw_i = sum_j [ exp(z* * loss_j) / sum_k exp(z* * loss_k) ] * (-r_{ji})

(an exponentially-tilted expectation of the asset returns).

This Python reference replicates the EXACT C# algorithm (own-formula, per the repo's
cross-language convention — NO external risk library). The golden-section bracket,
iteration count, and log-sum-exp shift MUST match EntropicValueAtRiskMeasure.cs so the
two agree to PrecisionNumeric (1e-6).
"""

import json
import math
from pathlib import Path

import numpy as np

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

RNG = np.random.default_rng(seed=707)  # Distinct seed for M-7 EVaR

# Golden-section search constants — MUST match the C# implementation.
Z_LO = 1e-6
Z_HI = 10000.0          # ceiling wide enough that the EVaR VALUE has converged for all
                        # realistic alpha (the minimizer can ride the ceiling when the
                        # worst loss is unique — the value asymptotes regardless).
GS_ITERS = 200          # iterations of golden-section bracket shrink
INV_PHI = (math.sqrt(5.0) - 1.0) / 2.0          # 1/phi  ≈ 0.6180339887
INV_PHI2 = (3.0 - math.sqrt(5.0)) / 2.0         # 1/phi^2 ≈ 0.3819660113

HARD_CHECKS: list[tuple[str, bool]] = []
QUALITY_CHECKS: list[tuple[str, bool, str]] = []


def hard_check(name: str, condition: bool):
    HARD_CHECKS.append((name, condition))
    assert condition, f"HARD CHECK FAILED: {name}"


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
# EVaR objective, value, gradient (matches C# EntropicValueAtRiskMeasure exactly)
# ═══════════════════════════════════════════════════════════════════════════

def evar_objective(z, losses, ln_alpha):
    """g(z) = z^{-1} * [ ln( (1/J) sum exp(z*loss) ) - ln(alpha) ], log-sum-exp guarded."""
    j = len(losses)
    m = max(z * loss for loss in losses)  # shift for overflow guard
    s = sum(math.exp(z * loss - m) for loss in losses)
    log_mean_mgf = m + math.log(s / j)
    return (log_mean_mgf - ln_alpha) / z


def evar_evaluate(weights, scenarios, confidence_level=0.95):
    """EVaR value + gradient. Matches C#."""
    n = len(weights)
    j = len(scenarios)
    # The spec's primal parameter alpha is the TAIL probability (alpha->0 => ess.sup,
    # alpha->1 => mean). The repo's CVaR-style `confidence_level` (0.95) is 1 - tail, so
    # the inner formula uses alpha_inner = 1 - confidence_level. Higher confidence =>
    # smaller alpha_inner => larger -ln(alpha_inner) => larger (more extreme) EVaR.
    alpha_inner = 1.0 - confidence_level
    ln_alpha = math.log(alpha_inner)

    # Losses are negative portfolio returns.
    losses = [-(sum(weights[i] * scenarios[t][i] for i in range(n))) for t in range(j)]

    # --- Golden-section minimization of g(z) over [Z_LO, Z_HI] ---
    a, b = Z_LO, Z_HI
    c = a + INV_PHI2 * (b - a)
    d = a + INV_PHI * (b - a)
    fc = evar_objective(c, losses, ln_alpha)
    fd = evar_objective(d, losses, ln_alpha)
    for _ in range(GS_ITERS):
        if fc < fd:
            b = d
            d = c
            fd = fc
            c = a + INV_PHI2 * (b - a)
            fc = evar_objective(c, losses, ln_alpha)
        else:
            a = c
            c = d
            fc = fd
            d = a + INV_PHI * (b - a)
            fd = evar_objective(d, losses, ln_alpha)

    z_star = (a + b) / 2.0
    value = evar_objective(z_star, losses, ln_alpha)

    # --- Gradient: exponentially-tilted expectation of -returns ---
    m = max(z_star * loss for loss in losses)
    tilt = [math.exp(z_star * loss - m) for loss in losses]
    tilt_sum = sum(tilt)
    grad = np.zeros(n)
    for t in range(j):
        p = tilt[t] / tilt_sum
        for i in range(n):
            grad[i] += p * (-scenarios[t][i])

    return value, grad, z_star


# ═══════════════════════════════════════════════════════════════════════════
# Generator
# ═══════════════════════════════════════════════════════════════════════════

def gen_evar():
    print("M-7: EVaR Risk Measure")

    # 3-asset, 100 scenarios
    n, s = 3, 100
    scenarios = RNG.normal(0.001, 0.02, (s, n)).tolist()
    weights = [0.5, 0.3, 0.2]

    val_95, grad_95, z_95 = evar_evaluate(weights, scenarios, 0.95)
    val_99, grad_99, z_99 = evar_evaluate(weights, scenarios, 0.99)
    val_50, grad_50, z_50 = evar_evaluate(weights, scenarios, 0.50)

    hard_check("EVaR: val_95 finite", math.isfinite(val_95))
    hard_check("EVaR: val_99 finite", math.isfinite(val_99))
    hard_check("EVaR: z_95 > 0", z_95 > 0)
    hard_check("EVaR: gradient length", len(grad_95) == n)
    hard_check("EVaR: gradient finite", all(math.isfinite(g) for g in grad_95))
    # Higher confidence → more extreme tail → EVaR α=0.99 ≥ α=0.95
    hard_check("EVaR: α=0.99 ≥ α=0.95", val_99 >= val_95 - 1e-10)
    hard_check("EVaR: α=0.95 ≥ α=0.50", val_95 >= val_50 - 1e-10)

    # Ordering: EVaR_alpha >= CVaR_alpha at the same tail probability.
    # NOTE: the M-7 spawn prompt / reference-planned.md prose states "CVaR_alpha >=
    # EVaR_alpha", but that is BACKWARDS relative to Ahmadi-Javid (2012, Prop. 3.2):
    # EVaR is the Chernoff-bound UPPER envelope of CVaR, so VaR <= CVaR <= EVaR. The
    # committed C# fidelity test asserts the mathematically-correct EVaR >= CVaR, which
    # still proves the inf-over-z is genuinely solved (a constant/E[X] stub would fail it).
    # CVaR via Rockafellar-Uryasev empirical quantile (matches C# CVaR).
    def cvar_evaluate(weights, scenarios, cl):
        nn = len(weights)
        ss = len(scenarios)
        tf = 1.0 / (ss * (1.0 - cl))
        tc = max(1, int(ss * (1.0 - cl)))
        pr = np.array([sum(weights[i] * scenarios[t][i] for i in range(nn)) for t in range(ss)])
        sr = np.sort(pr)
        zeta = -sr[min(tc, ss - 1)]
        cs = 0.0
        for t in range(ss):
            loss = -pr[t] - zeta
            if loss > 0:
                cs += loss
        return zeta + tf * cs

    cvar_95 = cvar_evaluate(weights, scenarios, 0.95)
    hard_check("EVaR ≥ CVaR at α=0.95", val_95 >= cvar_95 - 1e-9)

    # Limit check: as confidence -> ~0 (tail -> 1, alpha_inner -> 1), EVaR -> mean loss = E[X].
    mean_loss = float(np.mean([-(sum(weights[i] * scenarios[t][i] for i in range(n)))
                               for t in range(s)]))
    val_lo, _, _ = evar_evaluate(weights, scenarios, 0.001)  # alpha_inner = 0.999 ~ 1
    hard_check("EVaR -> E[X] as alpha_inner -> 1", abs(val_lo - mean_loss) < 1e-3)
    # Limit check (other direction): as confidence -> 1 (alpha_inner -> 0), EVaR grows
    # toward the worst-case loss — it must climb well ABOVE both the mean loss and CVaR_95.
    # (The exact ess.sup asymptote needs an unbounded z-bracket; we assert the monotone
    # climb, which is what proves the inf-over-z tracks the tail, not a constant.)
    val_hi, _, _ = evar_evaluate(weights, scenarios, 0.999)  # alpha_inner = 1e-3
    hard_check("EVaR climbs toward worst loss as alpha_inner -> 0",
               val_hi > val_95 and val_hi > mean_loss)

    # Equal weights
    eq_weights = [1.0 / n] * n
    val_eq, grad_eq, _ = evar_evaluate(eq_weights, scenarios, 0.95)
    hard_check("EVaR: equal weights finite", math.isfinite(val_eq))

    # Single asset
    single_w = [1.0, 0.0, 0.0]
    val_single, grad_single, _ = evar_evaluate(single_w, scenarios, 0.95)
    hard_check("EVaR: single asset finite", math.isfinite(val_single))

    # 5-asset
    n5, s5 = 5, 200
    scenarios_5 = RNG.normal(0.0005, 0.015, (s5, n5)).tolist()
    w5 = [0.3, 0.25, 0.2, 0.15, 0.1]
    val_5, grad_5, _ = evar_evaluate(w5, scenarios_5, 0.95)
    hard_check("EVaR 5-asset: finite", math.isfinite(val_5))
    hard_check("EVaR 5-asset: gradient length", len(grad_5) == n5)

    def case(w, sc, cl, v, g):
        return {
            "weights": w,
            "scenarios": sc,
            "confidence_level": cl,
            "value": v,
            "gradient": g.tolist() if isinstance(g, np.ndarray) else g,
        }

    save("risk_measure_evar", {
        "cases": {
            "standard_95": case(weights, scenarios, 0.95, val_95, grad_95),
            "alpha_99": case(weights, scenarios, 0.99, val_99, grad_99),
            "alpha_50": case(weights, scenarios, 0.50, val_50, grad_50),
            "equal_weights": case(eq_weights, scenarios, 0.95, val_eq, grad_eq),
            "single_asset": case(single_w, scenarios, 0.95, val_single, grad_single),
            "five_asset": case(w5, scenarios_5, 0.95, val_5, grad_5),
        },
        "notes": "EVaR: Ahmadi-Javid (2012) finite-sample primal. inf_{z>0} of "
                 "z^{-1}[ln(mean(exp(z*loss))) - ln(alpha)] via golden-section; "
                 "exponential-tilt gradient. EVaR_alpha >= CVaR_alpha >= VaR_alpha "
                 "(Ahmadi-Javid 2012, Prop. 3.2).",
    })


def main():
    print("=" * 70)
    print("M-7 EVaR: Generating verification vectors")
    print("=" * 70)

    gen_evar()

    print()
    print("=" * 70)
    hard_passed = sum(1 for _, ok in HARD_CHECKS if ok)
    hard_total = len(HARD_CHECKS)
    print(f"HARD checks: {hard_passed}/{hard_total} passed")
    if hard_passed < hard_total:
        for name, ok in HARD_CHECKS:
            if not ok:
                print(f"  ✗ {name}")
    print("=" * 70)


if __name__ == "__main__":
    main()
