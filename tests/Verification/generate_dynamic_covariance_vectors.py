#!/usr/bin/env python3
"""Generate the DCC-NL dynamic-covariance cross-language verification vector.

For V1 (per spec) this is a *self-consistency* vector: it ships a deterministic
N=2, T=50 synthetic return panel together with the DCC parameters (alpha, beta).
The C# test fits ``DccNlCovarianceEstimator`` on this panel and asserts that every
per-date conditional covariance H_t is positive semi-definite (the defining
accuracy contract: ``alpha + beta < 1  =>  H_t PSD``).

Because a faithful Python DCC-NL reference (per-asset GARCH MLE + QuEST/Jacobi
NL-shrunk intercept) would duplicate the Numerics kernel, the vector intentionally
carries only the *inputs* and the PSD expectation, not pre-computed H_t values.
Regenerate with:  python generate_dynamic_covariance_vectors.py
"""

import json
import math
import os
import random

OUT = os.path.join(os.path.dirname(__file__), "vectors", "dynamic_covariance_dcc_nl.json")


def synthetic_panel(n: int, t: int, seed: int = 7):
    """Mirror of the C# SyntheticReturns generator's *shape* (not bit-identical).

    A shared market factor induces cross-asset correlation; a GARCH-like recursion
    induces volatility clustering so per-asset GARCH(1,1) fitting is well-posed.
    """
    rng = random.Random(seed)
    market = [0.008 * (rng.random() - 0.5) for _ in range(t)]
    panel = []
    for _ in range(n):
        series = [0.0] * t
        vol = 0.01
        for k in range(t):
            prev = abs(series[k - 1]) if k > 0 else 0.0
            vol = 0.0008 + 0.85 * vol + 0.1 * prev
            idio = vol * (rng.random() - 0.5)
            series[k] = 0.6 * market[k] + idio
        panel.append(series)
    return panel


def main():
    n, t = 2, 50
    panel = synthetic_panel(n, t, seed=7)
    alpha, beta = 0.02, 0.96
    assert alpha + beta < 1.0, "DCC stationarity requires alpha + beta < 1"

    vector = {
        "_description": (
            "DCC-NL (Engle, Ledoit & Wolf 2019) self-consistency vector. "
            "C# fits DccNlCovarianceEstimator on these returns and asserts every "
            "per-date H_t is PSD (alpha+beta<1 => PSD)."
        ),
        "n_assets": n,
        "n_obs": t,
        "alpha": alpha,
        "beta": beta,
        "returns": panel,  # returns[asset][time]
        "expect_all_psd": True,
        "psd_eigenvalue_tolerance": -1e-9,
    }

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w") as f:
        json.dump(vector, f, indent=2)
    print(f"wrote {OUT}: n={n} t={t} alpha={alpha} beta={beta}")


if __name__ == "__main__":
    main()
