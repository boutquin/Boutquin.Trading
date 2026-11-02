#!/usr/bin/env python3
"""
Generate cross-language verification vectors for the M-1 signal-tilt layer.

Component: SignalTiltConstruction (the additive cross-sectional signal-tilt decorator).

The Python reference below INDEPENDENTLY re-implements the C# SignalTiltConstruction
transform (own-formula, not a library):

    for each (signal, strength):
        scored   = assets the signal scored this rebalance (absent key => omitted)
        mean     = average score across scored assets
        variance = sum((score - mean)^2) / count        # population divisor (cross-section)
        sigma    = sqrt(variance)
        if scored is empty or strength == 0 or sigma <= 1e-12: no tilt from this signal
        else: w_i += strength * (score_i - mean) / sigma   for each scored asset i
    w_i = max(w_i, 0)                                    # clamp negatives (long-only)
    w  /= sum(w)  (or equal-weight fallback if sum <= 0) # renormalize to the simplex

A C# test (SignalTiltCrossLanguageTests) loads these vectors and asserts
SignalTiltConstruction.ComputeTargetWeights reproduces the expected weights within
PrecisionNumeric (1e-6). The point is to catch cross-language drift in the
z-score / additive-tilt / clamp / renormalize arithmetic — no oracle library required
(the transform is a deterministic affine map + simplex projection with no external
reference implementation).

Vectors are checked in (repo convention). Regenerate with:
    python generate_signal_vectors.py
"""

import json
import math
from pathlib import Path

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

SIGMA_FLOOR = 1e-12


def signal_tilt(base_weights, signals, n):
    """Reference re-implementation of C# SignalTiltConstruction.ComputeTargetWeights.

    base_weights: list[float] of length n (weight per asset, in asset order).
    signals: list of {"scores": list[float | None], "strength": float}; a None score
             marks an asset the signal omitted (insufficient history => absent key).
    """
    tilted = list(base_weights)

    for signal in signals:
        scores = signal["scores"]
        strength = signal["strength"]

        scored_idx = [i for i in range(n) if scores[i] is not None]
        if len(scored_idx) == 0 or strength == 0.0:
            continue

        vals = [scores[i] for i in scored_idx]
        mean = sum(vals) / len(vals)
        variance = sum((v - mean) ** 2 for v in vals) / len(vals)
        sigma = math.sqrt(variance)
        if sigma <= SIGMA_FLOOR:
            continue

        for i in scored_idx:
            z = (scores[i] - mean) / sigma
            tilted[i] += strength * z

    # Clamp negatives (long-only).
    tilted = [w if w > 0.0 else 0.0 for w in tilted]

    total = sum(tilted)
    if total > 0.0:
        tilted = [w / total for w in tilted]
    else:
        tilted = [1.0 / n] * n

    return tilted


def save(name, data):
    with open(VECTORS_DIR / f"{name}.json", "w") as f:
        json.dump(data, f, indent=2)
    print(f"  -> {name}.json")


def _check(condition, label):
    print(f"    {'[PASS]' if condition else '[FAIL]'} {label}")
    if not condition:
        raise AssertionError(label)


def gen_signal_tilt():
    cases = {}

    # ── decorator-identity: all strengths 0 => output == base weights ──────────
    base_id = [0.5, 0.3, 0.2]
    signals_id = [{"scores": [5.0, -5.0, 0.0], "strength": 0.0}]
    w_id = signal_tilt(base_id, signals_id, 3)
    cases["identity_zero_strength"] = {
        "n_assets": 3,
        "base_weights": base_id,
        "signals": signals_id,
        "weights": w_id,
    }
    _check(max(abs(a - b) for a, b in zip(w_id, base_id)) < 1e-12,
           "identity: zero-strength output == base weights")

    # ── single signal, symmetric dispersion, weights stay on the simplex ───────
    base_1 = [1.0 / 3, 1.0 / 3, 1.0 / 3]
    signals_1 = [{"scores": [2.0, 0.0, -2.0], "strength": 0.1}]
    w_1 = signal_tilt(base_1, signals_1, 3)
    cases["single_signal_three_asset"] = {
        "n_assets": 3,
        "base_weights": base_1,
        "signals": signals_1,
        "weights": w_1,
    }
    _check(abs(sum(w_1) - 1.0) < 1e-12, "single signal: weights sum to 1")
    _check(all(w >= 0.0 for w in w_1), "single signal: weights non-negative")
    _check(w_1[0] > base_1[0] and w_1[2] < base_1[2], "single signal: tilt direction")

    # ── absent key: an omitted asset receives exactly zero tilt ────────────────
    base_ak = [1.0 / 3, 1.0 / 3, 1.0 / 3]
    signals_ak = [{"scores": [1.0, -1.0, None], "strength": 0.1}]  # 3rd asset omitted
    w_ak = signal_tilt(base_ak, signals_ak, 3)
    cases["absent_key_three_asset"] = {
        "n_assets": 3,
        "base_weights": base_ak,
        "signals": signals_ak,
        "weights": w_ak,
    }
    _check(abs(w_ak[2] - 1.0 / 3) < 1e-12, "absent key: omitted asset weight unchanged (zero tilt)")

    # ── clamp: a strong negative tilt drives a weight below zero, clamped to 0 ──
    base_cl = [0.5, 0.4, 0.1]
    signals_cl = [{"scores": [3.0, 0.0, -3.0], "strength": 0.5}]  # large strength
    w_cl = signal_tilt(base_cl, signals_cl, 3)
    cases["clamp_negative_three_asset"] = {
        "n_assets": 3,
        "base_weights": base_cl,
        "signals": signals_cl,
        "weights": w_cl,
    }
    _check(all(w >= 0.0 for w in w_cl), "clamp: all weights non-negative after clamp")
    _check(abs(sum(w_cl) - 1.0) < 1e-12, "clamp: weights sum to 1 after renormalize")

    # ── two signals over five assets, one omission in the second signal ────────
    base_2 = [0.30, 0.25, 0.20, 0.15, 0.10]
    signals_2 = [
        {"scores": [1.5, 0.5, -0.5, -1.5, 0.0], "strength": 0.10},
        {"scores": [0.8, None, 0.2, -0.4, -0.6], "strength": 0.05},  # 2nd asset omitted here
    ]
    w_2 = signal_tilt(base_2, signals_2, 5)
    cases["two_signals_five_asset"] = {
        "n_assets": 5,
        "base_weights": base_2,
        "signals": signals_2,
        "weights": w_2,
    }
    _check(abs(sum(w_2) - 1.0) < 1e-12, "two signals: weights sum to 1")
    _check(all(w >= 0.0 for w in w_2), "two signals: weights non-negative")

    save("signal_tilt_basic", {
        "cases": cases,
        "notes": (
            "SignalTiltConstruction: w_i += sum_s strength_s * zscore_s(i), "
            "cross-sectional z-score (population sigma across scored assets), "
            "clamp negatives to 0, renormalize to sum 1. Absent (None) score => "
            "omitted asset => zero tilt. Own-formula reference; PrecisionNumeric (1e-6)."
        ),
    })


def xs_momentum_score(daily_returns, total_months, skip_months, dpm):
    """Independent re-implementation of C# CrossSectionalMomentumSignal's per-asset score.

    Mirrors MomentumScore.Compute exactly (own-formula, not a library): the trailing
    12-1 cumulative return over the window
        [len - total_months*dpm, len - skip_months*dpm)
    i.e. a `total_months`-month lookback with the most recent `skip_months` month(s)
    excluded (the standard 12-1 operationalization of Jegadeesh & Titman's
    reversal-avoidance principle). Returns ``None`` when the series is shorter than the
    lookback (the insufficient-history omission contract), never an exception.
    """
    required = total_months * dpm
    if len(daily_returns) < required:
        return None  # omitted (absent key)

    skip_days = skip_months * dpm
    start = len(daily_returns) - required
    end = len(daily_returns) - skip_days

    cum = 1.0
    for i in range(start, end):
        cum *= (1.0 + daily_returns[i])
    return cum - 1.0


def gen_xs_momentum():
    """M-2: CrossSectionalMomentumSignal (Jegadeesh & Titman 1993, 12-1 adaptation).

    Compact parameters (lookback=3, skip=1, dpm=4 => required=12, window [len-12, len-4))
    keep the vectors small while exercising the identical window arithmetic the default
    12/1/21 signal uses. A None expected score marks an asset the signal omits.
    """
    cases = {}

    # ── rank-monotone: higher trailing 12-1 return => strictly higher score ─────
    lb, sk, dpm = 3, 1, 4
    rm_returns = [
        [0.02] * 12,   # highest constant return
        [0.01] * 12,   # middle
        [-0.01] * 12,  # lowest (negative)
    ]
    rm_scores = [xs_momentum_score(r, lb, sk, dpm) for r in rm_returns]
    cases["three_asset_rank_monotone"] = {
        "lookback_months": lb, "skip_months": sk, "trading_days_per_month": dpm,
        "n_assets": 3, "returns": rm_returns, "scores": rm_scores,
    }
    _check(rm_scores[0] > rm_scores[1] > rm_scores[2],
           "xs momentum: rank-monotone (score strictly decreasing in past return)")

    # ── skip-month: a spike in the most-recent (excluded) month must not move it ─
    base_series = [0.01] * 12
    spike_last = [0.01] * 8 + [0.5] * 4          # spike in indices 8..11 (excluded window)
    spike_in_window = [0.01] * 4 + [0.5] * 4 + [0.01] * 4  # spike in indices 4..7 (scored window)
    s_base = xs_momentum_score(base_series, lb, sk, dpm)
    s_last = xs_momentum_score(spike_last, lb, sk, dpm)
    s_in = xs_momentum_score(spike_in_window, lb, sk, dpm)
    cases["skip_month"] = {
        "lookback_months": lb, "skip_months": sk, "trading_days_per_month": dpm,
        "n_assets": 2,
        "returns": [base_series, spike_last],
        "scores": [s_base, s_last],
    }
    _check(abs(s_base - s_last) < 1e-15,
           "xs momentum: last-month spike excluded (score unchanged)")
    _check(abs(s_base - s_in) > 1e-6,
           "xs momentum: same spike inside the scored window DOES move the score (non-vacuous)")

    # ── insufficient history: a too-short series is omitted (null score) ─────────
    ih_returns = [
        [0.015] * 12,  # scored
        [0.02] * 8,    # length 8 < required 12 => omitted
    ]
    ih_scores = [xs_momentum_score(r, lb, sk, dpm) for r in ih_returns]
    cases["insufficient_history"] = {
        "lookback_months": lb, "skip_months": sk, "trading_days_per_month": dpm,
        "n_assets": 2, "returns": ih_returns, "scores": ih_scores,
    }
    _check(ih_scores[0] is not None and ih_scores[1] is None,
           "xs momentum: short series omitted (null), long series scored")

    save("signal_xs_momentum", {
        "cases": cases,
        "notes": (
            "CrossSectionalMomentumSignal (Jegadeesh-Titman 12-1 adaptation): per-asset "
            "score = trailing cumulative return over window [len - total*dpm, len - skip*dpm), "
            "excluding the most-recent skip month(s). Own-formula reference re-implementing "
            "MomentumScore.Compute; None score => omitted asset (insufficient history). "
            "PrecisionNumeric (1e-6)."
        ),
    })


def realized_volatility(series, window, dpy):
    """Independent re-implementation of C# RealizedVolatility.Compute (own-formula, not a library).

    Sample standard deviation of the trailing ``window`` returns, annualized by sqrt(dpy):
        mean     = sum(last window) / window
        variance = sum((r - mean)^2) / (window - 1)      # sample divisor
        sigma    = sqrt(variance) * sqrt(dpy)
    """
    tail = series[len(series) - window:]
    mean = sum(tail) / window
    variance = sum((r - mean) ** 2 for r in tail) / (window - 1)
    return math.sqrt(variance) * math.sqrt(dpy)


def cumulative_return(series, window):
    """Trailing compounded cumulative return over the last ``window`` observations: prod(1+r) - 1."""
    tail = series[len(series) - window:]
    cum = 1.0
    for r in tail:
        cum *= (1.0 + r)
    return cum - 1.0


def tsmom_score(series, lookback_months, dpm, vol_window, dpy, risk_free=None):
    """Independent re-implementation of C# TimeSeriesMomentumSignal's per-asset score.

    score = effectiveSign * (1 / sigma), where
        trend         = cumulative(asset, lookbackDays) - cumulative(rf, lookbackDays)   [excess]
                        (raw cumulative(asset) when the risk-free series is absent/too-short)
        effectiveSign = 1 if trend > 0 else 0                        (long-only clamp)
        sigma         = RealizedVolatility(series, vol_window, dpy)   (only needed when up-trend)

    Returns ``None`` when the series is shorter than requiredDays = max(lookbackDays, vol_window)
    (the insufficient-history omission contract). A down-trend/flat asset scores exactly 0 without
    touching sigma. (Zero-vol up-trend — a CalculationException in C# — is never emitted as a vector.)
    """
    lookback_days = lookback_months * dpm
    required = max(lookback_days, vol_window)
    if len(series) < required:
        return None  # omitted (absent key)

    trend = cumulative_return(series, lookback_days)
    if risk_free is not None and len(risk_free) >= lookback_days:
        trend -= cumulative_return(risk_free, lookback_days)

    if trend <= 0.0:
        return 0.0  # long-only clamp: down-trend/flat -> zero, no inverse-vol needed

    sigma = realized_volatility(series, vol_window, dpy)
    return 1.0 / sigma


def _alternating(mean, dispersion, length):
    """Alternating series: even idx = mean+dispersion, odd idx = mean-dispersion (matches the C# test
    helper). Over an even trailing window the sample mean is exactly ``mean`` and the deviation is
    exactly ±dispersion, so realized vol scales linearly in dispersion."""
    return [mean + (dispersion if i % 2 == 0 else -dispersion) for i in range(length)]


def gen_tsmom():
    """M-3: TimeSeriesMomentumSignal (Moskowitz, Ooi & Pedersen 2012, sign x inverse-vol, long-only).

    Compact params (lookback=3, dpm=4 => lookbackDays=12; vol_window=6; dpy=252) keep the vectors small
    while exercising the identical sign / cumulative-return / realized-vol arithmetic the default
    12/21/60/252 signal uses. A None expected score marks an asset the signal omits.
    """
    lb, dpm, vw, dpy = 3, 4, 6, 252
    rf_id = "DGS3MO"
    cases = {}

    # ── sign rule: up-trend scores positive (1/sigma), down-trend clamps to exactly 0 ──────────────
    sr_returns = [
        _alternating(0.001, 0.003, 12),   # up-trend
        _alternating(-0.001, 0.003, 12),  # down-trend -> 0
    ]
    sr_scores = [tsmom_score(r, lb, dpm, vw, dpy) for r in sr_returns]
    cases["sign_rule"] = {
        "lookback_months": lb, "trading_days_per_month": dpm, "vol_window": vw,
        "trading_days_per_year": dpy, "n_assets": 2,
        "returns": sr_returns, "scores": sr_scores,
    }
    _check(sr_scores[0] > 0.0 and sr_scores[1] == 0.0,
           "tsmom: up-trend positive, down-trend clamped to zero")

    # ── inverse-vol magnitude: same trend, 2x dispersion over last 6 => exactly half the score ─────
    iv_returns = [
        _alternating(0.001, 0.003, 12),  # dispersion d
        _alternating(0.001, 0.006, 12),  # dispersion 2d => sigma x2 => score /2
    ]
    iv_scores = [tsmom_score(r, lb, dpm, vw, dpy) for r in iv_returns]
    cases["inverse_vol_half"] = {
        "lookback_months": lb, "trading_days_per_month": dpm, "vol_window": vw,
        "trading_days_per_year": dpy, "n_assets": 2,
        "returns": iv_returns, "scores": iv_scores,
    }
    _check(abs(iv_scores[1] - iv_scores[0] / 2.0) < 1e-9,
           "tsmom: 2x volatility => half the score (magnitude ~ 1/sigma)")

    # ── excess vs raw: rf present makes the excess negative (score 0); absent => raw positive ──────
    asset = _alternating(0.0005, 0.002, 12)          # small positive raw trend
    rf = [0.001] * 12                                # rf cumulative > asset cumulative => excess < 0
    score_excess = tsmom_score(asset, lb, dpm, vw, dpy, risk_free=rf)
    score_raw = tsmom_score(asset, lb, dpm, vw, dpy)
    cases["excess_with_rf"] = {
        "lookback_months": lb, "trading_days_per_month": dpm, "vol_window": vw,
        "trading_days_per_year": dpy, "risk_free_id": rf_id, "risk_free_series": rf,
        "n_assets": 1, "returns": [asset], "scores": [score_excess],
    }
    cases["raw_fallback_no_rf"] = {
        "lookback_months": lb, "trading_days_per_month": dpm, "vol_window": vw,
        "trading_days_per_year": dpy,
        "n_assets": 1, "returns": [asset], "scores": [score_raw],
    }
    _check(score_excess == 0.0 and score_raw > 0.0,
           "tsmom: rf present => excess clamps to 0; rf absent => raw positive")

    # ── insufficient history: a series shorter than requiredDays is omitted (null score) ───────────
    ih_returns = [
        _alternating(0.001, 0.003, 12),  # scored
        _alternating(0.001, 0.003, 11),  # length 11 < required 12 => omitted
    ]
    ih_scores = [tsmom_score(r, lb, dpm, vw, dpy) for r in ih_returns]
    cases["insufficient_history"] = {
        "lookback_months": lb, "trading_days_per_month": dpm, "vol_window": vw,
        "trading_days_per_year": dpy, "n_assets": 2,
        "returns": ih_returns, "scores": ih_scores,
    }
    _check(ih_scores[0] is not None and ih_scores[1] is None,
           "tsmom: short series omitted (null), long series scored")

    save("signal_tsmom", {
        "cases": cases,
        "notes": (
            "TimeSeriesMomentumSignal (Moskowitz-Ooi-Pedersen 2012, long-only adaptation): per-asset "
            "score = effectiveSign * (1/sigma), effectiveSign = 1 if trailing 12-month EXCESS return > 0 "
            "else 0 (long-only clamp), sigma = annualized RealizedVolatility over vol_window. Excess = "
            "cumulative(asset) - cumulative(risk_free) when the risk_free_id series is present and long "
            "enough, else raw cumulative(asset) (documented fallback). None score => omitted asset "
            "(insufficient history). Own-formula reference re-implementing RealizedVolatility.Compute + "
            "the cumulative-return window; PrecisionNumeric (1e-6)."
        ),
    })


def long_horizon_reversal_value(series, lookback_months, dpm):
    """Independent re-implementation of C# LongHorizonReversalValueSignal's per-asset score.

    Value proxy of Asness, Moskowitz & Pedersen (2013): the NEGATIVE of the trailing five-year
    cumulative return, run through the CURRENT price with NO skip at the recent end (window
    [len - lookback_months*dpm, len), i.e. the whole trailing window including the most recent
    month). Returns ``None`` when the series is shorter than the window (insufficient-history
    omission), never an exception.
    """
    required = lookback_months * dpm
    if len(series) < required:
        return None  # omitted (absent key)

    start = len(series) - required
    cum = 1.0
    for k in range(start, len(series)):
        cum *= (1.0 + series[k])
    return -(cum - 1.0)


def gen_value():
    """M-4: LongHorizonReversalValueSignal (Asness et al. 2013, negative 5y return, no skip).

    Compact params (lookback=5, dpm=4 => required=20, window [len-20, len)) keep the vectors small
    while exercising the identical no-skip full-window arithmetic the default 60/21 signal uses. A None
    expected score marks an asset the signal omits.
    """
    lb, dpm = 5, 4
    req = lb * dpm  # 20
    cases = {}

    # ── reversal-monotone: higher past-5y return => strictly LOWER (more negative) value score ──────
    rv_returns = [
        [0.02] * req,   # highest past return  => most negative value score
        [0.01] * req,   # middle
        [-0.01] * req,  # lowest past return   => highest value score
    ]
    rv_scores = [long_horizon_reversal_value(r, lb, dpm) for r in rv_returns]
    cases["three_asset_reversal_monotone"] = {
        "lookback_months": lb, "trading_days_per_month": dpm,
        "n_assets": 3, "returns": rv_returns, "scores": rv_scores,
    }
    _check(rv_scores[0] < rv_scores[1] < rv_scores[2],
           "value: reversal-monotone (score strictly decreasing in past return)")

    # ── no-skip window: a spike in the MOST RECENT month DOES move the score (contrast M-2) ─────────
    base_series = [0.005] * req
    spike_last = [0.005] * (req - dpm) + [0.2] * dpm  # spike in the last month (indices req-dpm..req)
    s_base = long_horizon_reversal_value(base_series, lb, dpm)
    s_last = long_horizon_reversal_value(spike_last, lb, dpm)
    cases["no_skip_window"] = {
        "lookback_months": lb, "trading_days_per_month": dpm,
        "n_assets": 2, "returns": [base_series, spike_last], "scores": [s_base, s_last],
    }
    _check(abs(s_base - s_last) > 1e-6,
           "value: last-month spike MOVES the score (no skip at the recent end)")
    _check(s_last < s_base,
           "value: a bigger 5y return (last-month spike) => a lower value score")

    # ── insufficient history: a too-short series is omitted (null score) ────────────────────────────
    ih_returns = [
        [0.015] * req,      # scored
        [0.02] * (req - 1),  # one day short => omitted
    ]
    ih_scores = [long_horizon_reversal_value(r, lb, dpm) for r in ih_returns]
    cases["insufficient_history"] = {
        "lookback_months": lb, "trading_days_per_month": dpm,
        "n_assets": 2, "returns": ih_returns, "scores": ih_scores,
    }
    _check(ih_scores[0] is not None and ih_scores[1] is None,
           "value: short series omitted (null), long series scored")

    save("signal_value", {
        "cases": cases,
        "notes": (
            "LongHorizonReversalValueSignal (Asness-Moskowitz-Pedersen 2013): per-asset score = "
            "-(prod(1+r) - 1) over the WHOLE trailing window [len - lookback*dpm, len) -- the negative "
            "of the trailing five-year cumulative return, run through the CURRENT price with NO skip at "
            "the recent end (contrast M-2's 12-1 skip-month). None score => omitted asset (insufficient "
            "history). Own-formula reference; PrecisionNumeric (1e-6)."
        ),
    })


def _zscore_pop(vals):
    """Population (divide-by-N) cross-sectional z-scores, matching C# ValueMomentumCompositeSignal
    and M-1's SignalTiltConstruction. Raises on zero variance (a degenerate z-score)."""
    n = len(vals)
    mean = sum(vals) / n
    variance = sum((v - mean) ** 2 for v in vals) / n
    sigma = math.sqrt(variance)
    if sigma <= SIGMA_FLOOR:
        raise AssertionError("zero cross-sectional variance (z-score undefined)")
    return [(v - mean) / sigma for v in vals]


def value_momentum_composite(returns, value_lb, mom_lb, mom_skip, dpm,
                             value_weight=0.5, momentum_weight=0.5):
    """Independent re-implementation of C# ValueMomentumCompositeSignal.

    composite_i = value_weight * z(value)_i + momentum_weight * z(momentum)_i, where each z is a
    cross-sectional population z-score over the INTERSECTION of assets scored by BOTH legs (an asset
    omitted by either leg is omitted from the composite). value = long_horizon_reversal_value;
    momentum = xs_momentum_score (12-1). Returns a dict {asset_index: composite_score}.
    """
    n = len(returns)
    value_raw = [long_horizon_reversal_value(returns[i], value_lb, dpm) for i in range(n)]
    mom_raw = [xs_momentum_score(returns[i], mom_lb, mom_skip, dpm) for i in range(n)]

    common = [i for i in range(n) if value_raw[i] is not None and mom_raw[i] is not None]
    if not common:
        return {}

    z_value = _zscore_pop([value_raw[i] for i in common])
    z_mom = _zscore_pop([mom_raw[i] for i in common])

    out = {}
    for j, i in enumerate(common):
        out[i] = value_weight * z_value[j] + momentum_weight * z_mom[j]
    return out


def gen_composite():
    """M-4: ValueMomentumCompositeSignal (Asness et al. 2013, 50/50 COMBO of value & momentum).

    Compact params: value_lb=5, mom_lb=3, mom_skip=1, dpm=4 => value needs 20 days, momentum needs 12;
    series of length 20 satisfy both legs. Composite = 0.5 z(value) + 0.5 z(momentum) over the assets
    scored by both legs. A missing (null) composite entry marks an asset omitted by either leg.
    """
    value_lb, mom_lb, mom_skip, dpm = 5, 3, 1, 4
    req = value_lb * dpm  # 20
    cases = {}

    # ── anti-correlated panel: past-5y losers are also current 12-1 losers ─────────────────────────
    # Monotone-increasing constant returns => value strictly decreasing, momentum strictly increasing,
    # so the two legs anti-correlate (the paper's ~ -0.48 proxy correlation) and the composite's
    # cross-sectional variance is strictly below the mean of the components' (z-scored) variances.
    ac_returns = [
        [-0.02] * req,
        [-0.01] * req,
        [0.005] * req,
        [0.01] * req,
        [0.02] * req,
    ]
    comp = value_momentum_composite(ac_returns, value_lb, mom_lb, mom_skip, dpm)
    comp_scores = [comp.get(i) for i in range(len(ac_returns))]
    cases["anti_correlated_panel"] = {
        "value_lookback_months": value_lb, "momentum_lookback_months": mom_lb,
        "momentum_skip_months": mom_skip, "trading_days_per_month": dpm,
        "value_weight": 0.5, "momentum_weight": 0.5,
        "n_assets": len(ac_returns), "returns": ac_returns, "scores": comp_scores,
    }
    # Verify the anti-correlation the panel is built to exhibit (own-formula sanity).
    z_val = _zscore_pop([long_horizon_reversal_value(r, value_lb, dpm) for r in ac_returns])
    z_mom = _zscore_pop([xs_momentum_score(r, mom_lb, mom_skip, dpm) for r in ac_returns])
    cov = sum(a * b for a, b in zip(z_val, z_mom)) / len(z_val)  # z-scores => corr == cov
    _check(cov < 0.0, "composite: value & momentum anti-correlate on the loser-loser panel")
    var_comp = sum(s * s for s in comp_scores) / len(comp_scores)  # composite mean is ~0 (z-based)
    _check(var_comp < 1.0 - 1e-9,
           "composite: Var(composite) < mean(Var(value_z), Var(momentum_z)) == 1 (diversification)")

    # ── omission: an asset omitted by EITHER leg is omitted from the composite ──────────────────────
    om_returns = [
        [0.01] * req,        # scored by both
        [0.015] * req,       # scored by both
        [0.02] * (req - 1),  # 19 < value-required 20 => value omits => composite omits
    ]
    comp_om = value_momentum_composite(om_returns, value_lb, mom_lb, mom_skip, dpm)
    om_scores = [comp_om.get(i) for i in range(len(om_returns))]
    cases["either_leg_omits"] = {
        "value_lookback_months": value_lb, "momentum_lookback_months": mom_lb,
        "momentum_skip_months": mom_skip, "trading_days_per_month": dpm,
        "value_weight": 0.5, "momentum_weight": 0.5,
        "n_assets": len(om_returns), "returns": om_returns, "scores": om_scores,
    }
    _check(om_scores[2] is None and om_scores[0] is not None and om_scores[1] is not None,
           "composite: asset omitted by the value leg is omitted from the composite")

    save("signal_value_momentum_composite", {
        "cases": cases,
        "notes": (
            "ValueMomentumCompositeSignal (Asness-Moskowitz-Pedersen 2013 50/50 COMBO): composite_i = "
            "value_weight * z(value)_i + momentum_weight * z(momentum)_i, cross-sectional POPULATION "
            "z-scores over the INTERSECTION of assets scored by both legs (an asset omitted by either "
            "leg is omitted from the composite). value = negative trailing 5y return; momentum = 12-1. "
            "None score => omitted asset. Own-formula reference; PrecisionNumeric (1e-6)."
        ),
    })


def carry_score(asset_class, roll_down_duration, per_asset_series, market_series,
                long_curve_id, short_curve_id, local_short_rate_id):
    """Independent re-implementation of C# CarrySignal's per-asset carry score (own-formula, not a
    library). Carry = the return an asset earns if prices/curves stay unchanged (Koijen, Moskowitz,
    Pedersen & Vrugt 2018), by asset class:

        Bond     : y_long + roll_down_duration * (y_long - y_short)   (eq. 13-15; flat curve => == yield)
        Equity   : trailing income yield                             (eq. 8; per-asset series' last obs)
        FxHedged : (r_foreign - r_local) / (1 + r_local)             (eq. 7)

    Every input is the MOST RECENT (last) observation of its series. Returns ``None`` when a required
    series is absent/empty (the missing-series omission contract): a missing market-wide curve point omits
    the bond; a missing per-asset income-yield/foreign-rate omits the asset; a missing market-wide local
    rate omits the FX asset. (An FX local rate <= -100% is a C# CalculationException, never emitted as a
    vector.)
    """
    def latest(series):
        if series is None or len(series) == 0:
            return None
        return series[-1]

    if asset_class == "Bond":
        y_long = latest(market_series.get(long_curve_id))
        y_short = latest(market_series.get(short_curve_id))
        if y_long is None or y_short is None:
            return None
        return y_long + roll_down_duration * (y_long - y_short)

    if asset_class == "Equity":
        return latest(per_asset_series)  # None when absent => omitted

    if asset_class == "FxHedged":
        r_foreign = latest(per_asset_series)
        r_local = latest(market_series.get(local_short_rate_id))
        if r_foreign is None or r_local is None:
            return None
        return (r_foreign - r_local) / (1.0 + r_local)

    return None  # unmapped / unknown class => omitted


def gen_carry():
    """M-5: CarrySignal (Koijen, Moskowitz, Pedersen & Vrugt 2018, long-only multi-asset-class).

    Own-formula reference re-implementing the per-asset-class carry arithmetic. Each case pins the
    asset-class dispatch, the last-observation selection, and the missing-series omission contract. A None
    expected score marks an asset the signal omits.
    """
    long_id, short_id, local_id = "DGS10", "DGS2", "DGS3MO"
    cases = {}

    def build(name, rdd, asset_classes, per_asset, market, returns):
        scores = [
            carry_score(asset_classes[i], rdd, per_asset[i], market, long_id, short_id, local_id)
            for i in range(len(asset_classes))
        ]
        cases[name] = {
            "roll_down_duration": rdd,
            "long_curve_id": long_id, "short_curve_id": short_id, "local_short_rate_id": local_id,
            "n_assets": len(asset_classes),
            "asset_classes": asset_classes,
            "per_asset_series": per_asset,
            "market_series": market,
            "returns": returns,
            "scores": scores,
        }
        return scores

    # ── multi-class: bond (flat curve => carry == yield), equity (income yield), FX (differential) ────
    # Multi-observation series prove the signal reads the LAST observation, not the first.
    s_mc = build(
        "multi_class_basic", 1.0,
        ["Bond", "Equity", "FxHedged"],
        [None, [0.048, 0.05], [0.055, 0.06]],
        {"DGS10": [0.038, 0.04], "DGS2": [0.038, 0.04], "DGS3MO": [0.009, 0.01]},
        [[0.0] * 5, [0.0] * 5, [0.0] * 5],
    )
    _check(abs(s_mc[0] - 0.04) < 1e-12, "carry: flat-curve bond carry == yield (0.04)")
    _check(abs(s_mc[1] - 0.05) < 1e-12, "carry: equity carry == income yield (0.05)")
    _check(abs(s_mc[2] - (0.06 - 0.01) / 1.01) < 1e-12, "carry: FX carry == (r_f - r_l)/(1 + r_l)")

    # ── bond with an upward-sloping curve: carry = yield + roll_down_duration * term spread ──────────
    s_up = build(
        "bond_upward_curve", 2.0,
        ["Bond"],
        [None],
        {"DGS10": [0.05], "DGS2": [0.03]},
        [[0.0] * 5],
    )
    _check(abs(s_up[0] - (0.05 + 2.0 * (0.05 - 0.03))) < 1e-12,
           "carry: upward-curve bond carry = yield + rdd*(y_long - y_short) = 0.09")
    _check(s_up[0] > 0.05, "carry: upward curve => carry strictly above the raw yield (positive roll-down)")

    # ── per-asset omission: an equity missing its income yield is omitted (null) ─────────────────────
    s_po = build(
        "per_asset_omission", 1.0,
        ["Equity", "Equity"],
        [[0.03], None],
        {},
        [[0.0] * 5, [0.0] * 5],
    )
    _check(s_po[0] is not None and s_po[1] is None,
           "carry: equity missing its income yield is omitted (null)")

    # ── market-key omission: a bond missing the DGS2 curve point is omitted; the equity still scores ──
    s_mo = build(
        "market_key_omission_bond", 1.0,
        ["Bond", "Equity"],
        [None, [0.03]],
        {"DGS10": [0.04]},  # DGS2 absent => bond omitted
        [[0.0] * 5, [0.0] * 5],
    )
    _check(s_mo[0] is None and s_mo[1] is not None,
           "carry: bond missing a curve point omitted; equity still scored")

    save("signal_carry", {
        "cases": cases,
        "notes": (
            "CarrySignal (Koijen-Moskowitz-Pedersen-Vrugt 2018, long-only multi-asset-class): per-asset "
            "carry by class -- Bond = y_long + roll_down_duration*(y_long - y_short) from two market-wide "
            "curve points (flat curve => carry == yield); Equity/REIT = trailing income yield (per-asset); "
            "FxHedged = (r_foreign - r_local)/(1 + r_local), foreign per-asset, local market-wide. Every "
            "input is the LAST observation of its series. None score => omitted asset (missing series / "
            "unmapped class). Own-formula reference; PrecisionNumeric (1e-6)."
        ),
    })


def low_volatility(returns, window, dpy):
    """Independent re-implementation of C# LowVolatilitySignal (Baker-Bradley-Wurgler 2011).

    score = -z(realized sigma). Per rebalance: compute each asset's realized volatility with the
    own-formula ``realized_volatility`` (reused, NOT a library), OMIT any asset whose series is
    shorter than ``window`` (insufficient history => None), then cross-sectionally POPULATION
    z-score the surviving realized vols (``_zscore_pop``, divide-by-N, raises on zero dispersion)
    and NEGATE — a higher realized sigma maps to a strictly LOWER score (the tilt TOWARD
    individually low-vol assets). Returns a list positionally aligned with ``returns`` (None =>
    omitted asset). This is a cross-sectional characteristic sort of each asset's OWN realized
    risk -- NOT portfolio-variance minimization over a covariance matrix.
    """
    n = len(returns)
    scored = [i for i in range(n) if returns[i] is not None and len(returns[i]) >= window]
    if not scored:
        return [None] * n

    vols = [realized_volatility(returns[i], window, dpy) for i in scored]
    z = _zscore_pop(vols)  # raises on zero cross-sectional dispersion (a degenerate z-score)
    neg = {scored[k]: -z[k] for k in range(len(scored))}
    return [neg.get(i) for i in range(n)]


def gen_lowvol():
    """M-6: LowVolatilitySignal (Baker et al. 2011 low-volatility anomaly, score = -z(realized sigma)).

    Compact params (window=4, dpy=252) keep the vectors small while exercising the identical
    RealizedVolatility + cross-sectional population z-score arithmetic the default 60/252 signal uses.
    A None expected score marks an asset the signal omits (insufficient history).
    """
    window, dpy = 4, 252
    cases = {}

    # ── strictly decreasing in realized vol: higher sigma => strictly LOWER score ──────────────────
    sd_returns = [
        [0.00, 0.01, 0.00, 0.01],  # lowest realized sigma  => HIGHEST score
        [0.00, 0.02, 0.00, 0.02],  # middle
        [0.00, 0.04, 0.00, 0.04],  # highest realized sigma => LOWEST score
    ]
    sd_scores = low_volatility(sd_returns, window, dpy)
    cases["three_asset_strictly_decreasing"] = {
        "volatility_window": window, "trading_days_per_year": dpy,
        "n_assets": 3, "returns": sd_returns, "scores": sd_scores,
    }
    _check(sd_scores[0] > sd_scores[1] > sd_scores[2],
           "lowvol: score strictly decreasing in realized volatility")

    # ── equal-vol: two assets with identical realized sigma tie; a third differs (non-degenerate) ──
    ev_returns = [
        [0.00, 0.02, 0.00, 0.02],  # sigma_A
        [0.02, 0.00, 0.02, 0.00],  # sigma_B == sigma_A (same values, reordered)
        [0.00, 0.05, 0.00, 0.05],  # sigma_C != sigma_A  => cross-section has non-zero dispersion
    ]
    ev_scores = low_volatility(ev_returns, window, dpy)
    cases["equal_vol_two_tie"] = {
        "volatility_window": window, "trading_days_per_year": dpy,
        "n_assets": 3, "returns": ev_returns, "scores": ev_scores,
    }
    _check(abs(ev_scores[0] - ev_scores[1]) < 1e-12,
           "lowvol: equal-vol assets score equally")
    _check(abs(ev_scores[0] - ev_scores[2]) > 1e-9,
           "lowvol: a different-vol asset scores differently")

    # ── insufficient history: a too-short series is omitted (null score) ───────────────────────────
    ih_returns = [
        [0.00, 0.03, 0.00, 0.03],  # scored
        [0.00, 0.01, 0.00, 0.01],  # scored
        [0.00, 0.02, 0.00],        # one day short (len 3 < 4) => omitted
    ]
    ih_scores = low_volatility(ih_returns, window, dpy)
    cases["insufficient_history"] = {
        "volatility_window": window, "trading_days_per_year": dpy,
        "n_assets": 3, "returns": ih_returns, "scores": ih_scores,
    }
    _check(ih_scores[0] is not None and ih_scores[1] is not None and ih_scores[2] is None,
           "lowvol: short series omitted (null), long series scored")

    save("signal_lowvol", {
        "cases": cases,
        "notes": (
            "LowVolatilitySignal (Baker-Bradley-Wurgler 2011, low-volatility anomaly): per-rebalance "
            "score = -z(realized sigma over the lookback) -- reuse own-formula realized_volatility for "
            "each asset's OWN trailing sigma, then cross-sectional POPULATION z-score (divide-by-N) and "
            "NEGATE, so a higher realized sigma => a strictly LOWER score (a tilt TOWARD individually "
            "low-vol assets). A cross-sectional characteristic sort of each asset's own realized risk, "
            "NOT portfolio-variance minimization over a covariance matrix. None score => omitted asset "
            "(insufficient history). Own-formula reference; PrecisionNumeric (1e-6)."
        ),
    })


def main():
    print("Signal-layer cross-language vectors...")
    print()
    print("M-1 signal-tilt decorator:")
    gen_signal_tilt()
    print()
    print("M-2 cross-sectional momentum signal:")
    gen_xs_momentum()
    print()
    print("M-3 time-series momentum signal:")
    gen_tsmom()
    print()
    print("M-4 long-horizon-reversal value signal:")
    gen_value()
    print()
    print("M-4 value-momentum composite signal:")
    gen_composite()
    print()
    print("M-5 carry signal:")
    gen_carry()
    print()
    print("M-6 low-volatility signal:")
    gen_lowvol()
    print()
    print(f"Done. 7 vector files in {VECTORS_DIR}")


if __name__ == "__main__":
    main()
