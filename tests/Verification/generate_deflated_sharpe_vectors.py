#!/usr/bin/env python3
"""
Generate cross-language verification vectors for the M-8 overfitting-corrected
Sharpe analytics: ProbabilisticSharpeRatio + DeflatedSharpeRatio.

Own-formula reference — this generator INDEPENDENTLY re-implements the
Bailey & López de Prado (2014) PSR / DSR formulas (Eq. 2 + the expected-maximum
Sharpe "Snippet 1", Appendix A.2). It uses scipy.stats.norm ONLY for the
standard-normal CDF / inverse-CDF primitives; it imports NO deflated-Sharpe library.

Kurtosis convention: the paper's formula takes FULL kurtosis (Gaussian = 3). Every
vector below states the full kurtosis explicitly. The worked example reproduces
DSR ~= 0.9004 only with the full value (gamma_4 = 10), NOT the excess value (7).

Numeric note: the C# engine evaluates the normal CDF / inverse-CDF via the
Boutquin.Numerics Abramowitz-Stegun / rational approximations (~1.5e-7 error),
whereas this generator uses scipy's near-exact routines. The C# cross-language
tests therefore assert at the statistical precision tier (1e-4), mirroring the
Jacobi-vs-LAPACK precedent documented for the denoised covariance vectors.
"""

import json
from pathlib import Path

import numpy as np
import scipy.stats as ss

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

EMC = 0.5772156649  # Euler-Mascheroni constant


def save(name: str, data: dict) -> None:
    with open(VECTORS_DIR / f"{name}.json", "w") as f:
        json.dump(data, f, indent=2)
    print(f"  ✓ {name}.json")


# ── Own-formula PSR / DSR (Bailey & López de Prado 2014) ────────────────────

def psr(observed_sharpe, benchmark_sharpe, num_obs, skew, full_kurt):
    """Probabilistic Sharpe Ratio — Eq. (2), non-annualized inputs, FULL kurtosis."""
    num = (observed_sharpe - benchmark_sharpe) * np.sqrt(num_obs - 1)
    den = np.sqrt(1.0 - skew * observed_sharpe + (full_kurt - 1.0) / 4.0 * observed_sharpe ** 2)
    return float(ss.norm.cdf(num / den))


def expected_max_sharpe(num_trials, variance_of_trial_sharpes):
    """ŜR₀ — expected max Sharpe under the null (Snippet 1), in the variance's units."""
    n = num_trials
    max_z = (1.0 - EMC) * ss.norm.ppf(1.0 - 1.0 / n) + EMC * ss.norm.ppf(1.0 - 1.0 / (n * np.e))
    return float(np.sqrt(variance_of_trial_sharpes) * max_z)


def dsr(observed_sharpe, num_trials, variance_of_trial_sharpes, num_obs, skew, full_kurt,
        trading_days_per_year=1):
    """Deflated Sharpe Ratio — PSR against the expected-max-Sharpe benchmark."""
    ann = np.sqrt(trading_days_per_year)
    sr = observed_sharpe / ann
    var = variance_of_trial_sharpes / trading_days_per_year
    sr0 = expected_max_sharpe(num_trials, var)
    return psr(sr, sr0, num_obs, skew, full_kurt)


# ── Vectors ─────────────────────────────────────────────────────────────────

def main():
    print("Generating Deflated Sharpe Ratio vectors...")

    # 1. The paper's own worked example (pp. 9-10) — THE fidelity anchor.
    worked = {
        "description": "Bailey & Lopez de Prado (2014) worked example, pp. 9-10",
        "num_trials": 100,
        "variance_of_trial_sharpes_annualized": 0.5,
        "num_observations": 1250,
        "annualized_sharpe": 2.5,
        "skewness": -3.0,
        "full_kurtosis": 10.0,
        "trading_days_per_year": 250,
        "expected_max_sharpe_non_annualized": expected_max_sharpe(100, 0.5 / 250),
        "dsr": dsr(2.5, 100, 0.5, 1250, -3.0, 10.0, 250),
        # The trap: feeding the excess kurtosis (7) as if it were full inflates the DSR.
        "dsr_excess_kurtosis_as_full": dsr(2.5, 100, 0.5, 1250, -3.0, 7.0, 250),
    }
    save("deflated_sharpe_worked_example", worked)

    # 2. Gaussian comparison point (paper p. 10): gamma3=0, gamma4=3, N=88 => DSR=0.9505.
    gaussian = {
        "description": "Gaussian comparison point, paper p. 10",
        "num_trials": 88,
        "variance_of_trial_sharpes_annualized": 0.5,
        "num_observations": 1250,
        "annualized_sharpe": 2.5,
        "skewness": 0.0,
        "full_kurtosis": 3.0,
        "trading_days_per_year": 250,
        "dsr": dsr(2.5, 88, 0.5, 1250, 0.0, 3.0, 250),
    }
    save("deflated_sharpe_gaussian_point", gaussian)

    # 3. PSR analytic property vectors (non-annualized, FULL kurtosis).
    sr = 2.5 / np.sqrt(250)  # 0.15811...
    psr_cases = []
    psr_cases.append({"observed_sharpe": sr, "benchmark_sharpe": sr, "num_observations": 1250,
                      "skewness": 0.0, "full_kurtosis": 3.0, "psr": psr(sr, sr, 1250, 0.0, 3.0)})
    for bench in [0.05, 0.10, 0.14, 0.17, 0.20]:
        psr_cases.append({"observed_sharpe": sr, "benchmark_sharpe": bench, "num_observations": 1250,
                          "skewness": -0.5, "full_kurtosis": 4.0, "psr": psr(sr, bench, 1250, -0.5, 4.0)})
    save("probabilistic_sharpe_cases", {"cases": psr_cases})

    # 4. DSR monotonicity-in-N series (annualized inputs, worked-example moments).
    monotone = []
    for n in [2, 5, 10, 50, 100, 500, 1000, 5000]:
        monotone.append({"num_trials": n, "dsr": dsr(2.5, n, 0.5, 1250, -3.0, 10.0, 250)})
    save("deflated_sharpe_monotone_n", {
        "variance_of_trial_sharpes_annualized": 0.5, "num_observations": 1250,
        "annualized_sharpe": 2.5, "skewness": -3.0, "full_kurtosis": 10.0,
        "trading_days_per_year": 250, "series": monotone,
    })


if __name__ == "__main__":
    main()
    print("Done.")
