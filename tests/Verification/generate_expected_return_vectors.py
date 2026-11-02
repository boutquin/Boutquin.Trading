#!/usr/bin/env python3
# Copyright (c) 2023-2026 Pierre G. Boutquin. All rights reserved.
#
#   Licensed under the Apache License, Version 2.0 (the "License").
#   You may not use this file except in compliance with the License.
#
"""Generate cross-language verification vectors for the M-7 expected-return estimators.

Two estimators (Boutquin.Trading.Application.ExpectedReturns):

  * HistoricalMeanExpectedReturnEstimator: E[r_i] = sample mean of returns[i] (the behavior-preserving
    extraction of MeanVarianceConstruction's inline returns[i].Average()). Ignores auxiliarySeries.

  * YieldAnchoredExpectedReturnEstimator: Ilmanen (2012) building-block decomposition, by asset class:
        Bond   : E[r] = current yield                       -> latest obs of the asset's per-symbol series
        Equity : E[r] = income yield + trend real growth + inflation
                 -> latest(per-asset income yield) + latest(REALGROWTH) + latest(T10YIE), both market-wide.
    No term reads the realized-return series, so with the aux series held constant the estimate is constant
    regardless of realized-price noise (the M-7 forward-looking fidelity property).

This generator INDEPENDENTLY re-implements that arithmetic (own-formula, no external library). A C# test
(ExpectedReturnEstimatorCrossLanguageTests, matching ~ExpectedReturn) loads the vectors and asserts the C#
estimators reproduce them.

Usage:
    python generate_expected_return_vectors.py
"""

import json
from pathlib import Path

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

REAL_GROWTH_ID = "REALGROWTH"
INFLATION_ID = "T10YIE"


def save(name, data):
    with open(VECTORS_DIR / f"{name}.json", "w") as f:
        json.dump(data, f, indent=2)
    print(f"  -> {name}.json")


def _check(condition, label):
    print(f"    {'[PASS]' if condition else '[FAIL]'} {label}")
    if not condition:
        raise AssertionError(label)


def sample_mean(series):
    """Sample mean of a return series (matches C# decimal[].Average())."""
    return sum(series) / len(series)


def yield_anchored_score(asset_class, per_asset_series, market_series):
    """Independent re-implementation of C# YieldAnchoredExpectedReturnEstimator's per-asset E[r]
    (Ilmanen 2012), by asset class. Every input is the MOST RECENT (last) observation of its series.
    Returns None when a required series is absent/empty (the missing-input contract -> the C# estimator
    throws CalculationException naming the asset; a None here marks a case the positive vector does not emit)."""
    def latest(series):
        if series is None or len(series) == 0:
            return None
        return series[-1]

    if asset_class == "Bond":
        # E[r] = current yield (Foreword p. vi), the latest obs of the asset's own per-symbol yield series.
        return latest(per_asset_series)

    if asset_class == "Equity":
        # E[r] = income yield (D/P) + trend real growth (G_real) + inflation (eq. 1 p. 29, nominal form p. 29).
        dp = latest(per_asset_series)
        g = latest(market_series.get(REAL_GROWTH_ID))
        infl = latest(market_series.get(INFLATION_ID))
        if dp is None or g is None or infl is None:
            return None
        return dp + g + infl

    return None


def gen_historical_mean():
    """HistoricalMeanExpectedReturnEstimator: E[r_i] = sample mean of returns[i]."""
    cases = {}

    def build(name, returns):
        expected = [sample_mean(r) for r in returns]
        cases[name] = {"n_assets": len(returns), "returns": returns, "expected": expected}
        return expected

    e1 = build("single_asset", [[0.01, 0.02, 0.03, -0.01, 0.05]])
    _check(abs(e1[0] - (0.01 + 0.02 + 0.03 - 0.01 + 0.05) / 5) < 1e-15,
           "historical_mean: single-asset mean")

    e3 = build("three_asset", [
        [0.010, -0.020, 0.030, 0.000, 0.015],
        [0.005, 0.005, 0.005, 0.005, 0.005],
        [-0.010, 0.040, -0.030, 0.020, 0.010],
    ])
    _check(abs(e3[1] - 0.005) < 1e-15, "historical_mean: constant series mean == the constant")

    return cases


def gen_yield_anchored():
    """YieldAnchoredExpectedReturnEstimator: Ilmanen (2012) building-block decomposition by asset class."""
    cases = {}

    def build(name, asset_classes, per_asset, market, returns):
        expected = [
            yield_anchored_score(asset_classes[i], per_asset[i], market)
            for i in range(len(asset_classes))
        ]
        cases[name] = {
            "n_assets": len(asset_classes),
            "asset_classes": asset_classes,
            "per_asset_series": per_asset,
            "market_series": market,
            "returns": returns,
            "expected": expected,
        }
        return expected

    # ── multi-class: a bond (current yield) and an equity (income yield + growth + inflation). ──────────
    #    Multi-observation series prove the estimator reads the LAST observation, not the first, and the
    #    returns series (deliberately noisy) is NOT consumed by any building block.
    e_mc = build(
        "multi_class",
        ["Bond", "Equity"],
        [[0.038, 0.040], [0.019, 0.020]],
        {REAL_GROWTH_ID: [0.014, 0.015], INFLATION_ID: [0.024, 0.025]},
        [[0.10, -0.30, 0.22, -0.05, 0.40], [-0.20, 0.33, -0.11, 0.08, -0.02]],
    )
    _check(abs(e_mc[0] - 0.040) < 1e-15, "yield_anchored: bond E[r] == current yield (last obs 0.040)")
    _check(abs(e_mc[1] - (0.020 + 0.015 + 0.025)) < 1e-15,
           "yield_anchored: equity E[r] == D/P + G_real + inflation == 0.060")

    # ── bond-only: two bonds, higher current yield => higher E[r] ───────────────────────────────────────
    e_b = build(
        "bond_pair",
        ["Bond", "Bond"],
        [[0.030], [0.045]],
        {},
        [[0.0] * 5, [0.0] * 5],
    )
    _check(abs(e_b[0] - 0.030) < 1e-15 and abs(e_b[1] - 0.045) < 1e-15, "yield_anchored: bond pair yields")
    _check(e_b[1] > e_b[0], "yield_anchored: higher-yield bond has strictly higher E[r]")

    # ── equity-only: two equities sharing the market-wide growth/inflation, differing only in income yield ─
    e_e = build(
        "equity_pair",
        ["Equity", "Equity"],
        [[0.015], [0.028]],
        {REAL_GROWTH_ID: [0.012], INFLATION_ID: [0.020]},
        [[0.0] * 5, [0.0] * 5],
    )
    _check(abs(e_e[0] - (0.015 + 0.012 + 0.020)) < 1e-15, "yield_anchored: equity0 E[r] == 0.047")
    _check(abs(e_e[1] - (0.028 + 0.012 + 0.020)) < 1e-15, "yield_anchored: equity1 E[r] == 0.060")

    return cases


def main():
    print("M-7 expected-return estimator cross-language vectors...")
    print("HistoricalMean:")
    hm = gen_historical_mean()
    print("YieldAnchored:")
    ya = gen_yield_anchored()

    save("expected_return_estimators", {
        "historical_mean": {"cases": hm},
        "yield_anchored": {
            "real_growth_id": REAL_GROWTH_ID,
            "inflation_id": INFLATION_ID,
            "cases": ya,
        },
        "notes": (
            "M-7 expected-return estimators (Ilmanen 2012). HistoricalMean: E[r_i] = sample mean of "
            "returns[i]. YieldAnchored building blocks by class -- Bond = current yield (last obs of the "
            "asset's per-symbol yield series); Equity/REIT = income yield (per-asset) + trend real growth "
            "(market-wide REALGROWTH) + inflation (market-wide T10YIE), all last observations. No term reads "
            "the realized-return series. Own-formula reference; PrecisionExact (1e-10)."
        ),
    })
    print("Done. 1 vector file in", VECTORS_DIR)


if __name__ == "__main__":
    main()
