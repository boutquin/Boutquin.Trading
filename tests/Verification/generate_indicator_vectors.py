#!/usr/bin/env python3
"""
Generate cross-language verification vectors for indicators.

Phase 5A of the verification roadmap.
Components: SimpleMovingAverage, ExponentialMovingAverage, RealizedVolatility,
            MomentumScore, SpreadIndicator, RateOfChangeIndicator.

All Python reference implementations replicate the EXACT C# algorithms.

CORRECTNESS VALIDATION (three layers):
  1. Library cross-references — SMA/EMA vs pandas.rolling/pandas.ewm,
     RealizedVol vs numpy std * sqrt(252).
  2. Analytical solutions — SMA of constant = constant, EMA(span=1) = last value,
     RealizedVol of constant = 0, Momentum of zero returns = 0.
  3. Property-based checks — SMA is bounded by min/max of window,
     EMA is bounded by min/max of series, RealizedVol >= 0,
     Spread = series1[-1] - series2[-1].
"""

import json
import math
from pathlib import Path

import numpy as np
import pandas as pd

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

RNG = np.random.default_rng(seed=501)  # Distinct seed for Phase 5 indicators

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

    path = VECTORS_DIR / f"{name}.json"
    with open(path, "w") as f:
        json.dump(data, f, indent=2, default=convert)
    print(f"  ✓ Saved {path.name}")


# ═══════════════════════════════════════════════════════════════════════════
# Own-formula indicator implementations (matching C#)
# ═══════════════════════════════════════════════════════════════════════════

def sma(values: np.ndarray, period: int) -> float:
    """Simple Moving Average: mean of last `period` values."""
    return float(np.mean(values[-period:]))


def ema(values: np.ndarray, period: int) -> float:
    """EMA seeded with SMA of first `period` values, then recursive."""
    multiplier = 2.0 / (period + 1)
    seed = float(np.mean(values[:period]))
    result = seed
    for i in range(period, len(values)):
        result = (float(values[i]) - result) * multiplier + result
    return result


def realized_volatility(returns: np.ndarray, window: int, trading_days: int = 252) -> float:
    """Annualized realized volatility: sample std of last `window` returns * sqrt(trading_days)."""
    windowed = returns[-window:]
    mean = float(np.mean(windowed))
    sum_sq_dev = float(np.sum((windowed - mean) ** 2))
    variance = sum_sq_dev / (window - 1)
    std_dev = math.sqrt(variance)
    return std_dev * math.sqrt(trading_days)


def momentum_score(daily_returns: np.ndarray, total_months: int = 12,
                   skip_months: int = 1, trading_days_per_month: int = 21) -> float:
    """12-1 month cumulative return (excludes most recent `skip_months`)."""
    required_days = total_months * trading_days_per_month
    skip_days = skip_months * trading_days_per_month
    start = len(daily_returns) - required_days
    end = len(daily_returns) - skip_days
    cum = 1.0
    for i in range(start, end):
        cum *= (1.0 + float(daily_returns[i]))
    return cum - 1.0


def spread_indicator(series1: np.ndarray, series2: np.ndarray) -> float:
    """Spread: difference of latest values."""
    return float(series1[-1]) - float(series2[-1])


def rate_of_change(series1: np.ndarray, series2: np.ndarray, lookback: int) -> float:
    """Rate of change of spread over lookback period."""
    current_spread = float(series1[-1]) - float(series2[-1])
    prior_spread = float(series1[-(1 + lookback)]) - float(series2[-(1 + lookback)])
    return (current_spread - prior_spread) / abs(prior_spread)


# ═══════════════════════════════════════════════════════════════════════════
# 5A. Indicator vectors
# ═══════════════════════════════════════════════════════════════════════════

def generate_indicator_vectors():
    print("\n═══ Phase 5A: Indicator Vectors ═══")

    # Generate diverse test data
    prices = 100 + np.cumsum(RNG.normal(0.001, 0.02, 300))  # ~300 price points
    returns = np.diff(prices) / prices[:-1]  # ~299 return points
    series1 = 3.0 + np.cumsum(RNG.normal(0.0, 0.05, 50))  # yield-like series
    series2 = 1.5 + np.cumsum(RNG.normal(0.0, 0.03, 50))  # another yield series

    cases = {}

    # --- SMA cases ---
    for period in [5, 10, 20, 50]:
        val = sma(prices, period)
        cases[f"sma_period_{period}"] = {
            "indicator": "SMA",
            "period": period,
            "values": prices.tolist(),
            "expected": val,
        }
        # Layer 2: SMA bounded by min/max of window
        window_min = float(np.min(prices[-period:]))
        window_max = float(np.max(prices[-period:]))
        hard_check(f"SMA({period}) bounded", window_min <= val <= window_max)

    # Layer 1: SMA vs pandas rolling
    pd_sma = pd.Series(prices).rolling(20).mean().iloc[-1]
    quality_check("SMA(20) vs pandas", sma(prices, 20), float(pd_sma))

    # Layer 2: SMA of constant series
    constant = np.full(30, 42.0)
    sma_const = sma(constant, 10)
    hard_check("SMA(constant)=constant", abs(sma_const - 42.0) < 1e-12)
    cases["sma_constant"] = {
        "indicator": "SMA",
        "period": 10,
        "values": constant.tolist(),
        "expected": 42.0,
    }

    # --- EMA cases ---
    for period in [5, 10, 20, 50]:
        val = ema(prices, period)
        cases[f"ema_period_{period}"] = {
            "indicator": "EMA",
            "period": period,
            "values": prices.tolist(),
            "expected": val,
        }
        # Layer 3: EMA bounded by global min/max
        hard_check(f"EMA({period}) bounded", float(np.min(prices)) <= val <= float(np.max(prices)))

    # Layer 1: EMA vs pandas ewm
    # pandas ewm(span=period, adjust=False) uses the same SMA-seed + recursive formula
    # when min_periods=period
    pd_ema_series = pd.Series(prices).ewm(span=20, adjust=False).mean()
    pd_ema_val = float(pd_ema_series.iloc[-1])
    quality_check("EMA(20) vs pandas", ema(prices, 20), pd_ema_val)

    # Layer 2: EMA of constant
    ema_const = ema(constant, 10)
    hard_check("EMA(constant)=constant", abs(ema_const - 42.0) < 1e-12)
    cases["ema_constant"] = {
        "indicator": "EMA",
        "period": 10,
        "values": constant.tolist(),
        "expected": 42.0,
    }

    # Layer 2: EMA with period=1 => last value (multiplier = 2/2 = 1.0, but seed=first value,
    # then each step: (v-ema)*1 + ema = v, so final = last value)
    ema_span1 = ema(prices, 1)
    hard_check("EMA(1)=last value", abs(ema_span1 - float(prices[-1])) < 1e-12)
    cases["ema_span1"] = {
        "indicator": "EMA",
        "period": 1,
        "values": prices.tolist(),
        "expected": float(prices[-1]),
    }

    # --- RealizedVolatility cases ---
    for window in [20, 60, 120]:
        val = realized_volatility(returns, window)
        cases[f"realvol_window_{window}"] = {
            "indicator": "RealizedVolatility",
            "window": window,
            "trading_days_per_year": 252,
            "values": returns.tolist(),
            "expected": val,
        }
        hard_check(f"RealVol({window}) >= 0", val >= 0)

    # Layer 1: vs numpy
    w20 = returns[-20:]
    np_std = float(np.std(w20, ddof=1))
    np_vol = np_std * math.sqrt(252)
    quality_check("RealVol(20) vs numpy", realized_volatility(returns, 20), np_vol)

    # Layer 2: constant returns => vol = 0
    constant_ret = np.full(30, 0.01)
    rv_const = realized_volatility(constant_ret, 20)
    hard_check("RealVol(constant)=0", abs(rv_const) < 1e-12)
    cases["realvol_constant"] = {
        "indicator": "RealizedVolatility",
        "window": 20,
        "trading_days_per_year": 252,
        "values": constant_ret.tolist(),
        "expected": 0.0,
    }

    # --- MomentumScore cases ---
    # Need at least 12*21 = 252 daily returns
    mom_returns = RNG.normal(0.0005, 0.015, 300)
    for (total, skip) in [(12, 1), (6, 1), (3, 1)]:
        val = momentum_score(mom_returns, total, skip)
        cases[f"momentum_{total}_{skip}"] = {
            "indicator": "MomentumScore",
            "total_months": total,
            "skip_months": skip,
            "trading_days_per_month": 21,
            "values": mom_returns.tolist(),
            "expected": val,
        }

    # Layer 2: zero returns => momentum = 0
    zero_ret = np.zeros(300)
    mom_zero = momentum_score(zero_ret, 12, 1)
    hard_check("Momentum(zero)=0", abs(mom_zero) < 1e-12)
    cases["momentum_zero_returns"] = {
        "indicator": "MomentumScore",
        "total_months": 12,
        "skip_months": 1,
        "trading_days_per_month": 21,
        "values": zero_ret.tolist(),
        "expected": 0.0,
    }

    # Layer 3: momentum is a valid return (>= -1)
    hard_check("Momentum >= -1", momentum_score(mom_returns, 12, 1) >= -1.0)

    # --- SpreadIndicator cases ---
    spread_val = spread_indicator(series1, series2)
    hard_check("Spread = last diff", abs(spread_val - (float(series1[-1]) - float(series2[-1]))) < 1e-15)
    cases["spread_standard"] = {
        "indicator": "SpreadIndicator",
        "series1": series1.tolist(),
        "series2": series2.tolist(),
        "expected": spread_val,
    }

    # Edge: identical series => spread = 0
    spread_zero = spread_indicator(series1, series1)
    hard_check("Spread(identical)=0", abs(spread_zero) < 1e-15)
    cases["spread_identical"] = {
        "indicator": "SpreadIndicator",
        "series1": series1.tolist(),
        "series2": series1.tolist(),
        "expected": 0.0,
    }

    # --- RateOfChangeIndicator cases ---
    for lookback in [1, 5, 10]:
        val = rate_of_change(series1, series2, lookback)
        cases[f"roc_lookback_{lookback}"] = {
            "indicator": "RateOfChangeIndicator",
            "lookback": lookback,
            "series1": series1.tolist(),
            "series2": series2.tolist(),
            "expected": val,
        }

    # Layer 2: constant spread => ROC = 0
    constant_s1 = np.array([5.0] * 20)
    constant_s2 = np.array([3.0] * 20)
    roc_const = rate_of_change(constant_s1, constant_s2, 5)
    hard_check("ROC(constant spread)=0", abs(roc_const) < 1e-12)
    cases["roc_constant_spread"] = {
        "indicator": "RateOfChangeIndicator",
        "lookback": 5,
        "series1": constant_s1.tolist(),
        "series2": constant_s2.tolist(),
        "expected": 0.0,
    }

    # Layer 3: multiple random trials — ROC is finite
    for trial in range(5):
        s1 = RNG.normal(3.0, 0.5, 30)
        s2 = RNG.normal(1.5, 0.3, 30)
        # Ensure prior spread is non-zero
        prior = float(s1[-6]) - float(s2[-6])
        if abs(prior) > 1e-10:
            roc_val = rate_of_change(s1, s2, 5)
            hard_check(f"ROC trial {trial} finite", math.isfinite(roc_val))

    save("indicator_sma_ema", {
        "description": "Phase 5A: SMA and EMA indicator verification vectors",
        "cases": {k: v for k, v in cases.items() if v["indicator"] in ("SMA", "EMA")},
    })

    save("indicator_realvol_momentum", {
        "description": "Phase 5A: RealizedVolatility and MomentumScore verification vectors",
        "cases": {k: v for k, v in cases.items()
                  if v["indicator"] in ("RealizedVolatility", "MomentumScore")},
    })

    save("indicator_spread_roc", {
        "description": "Phase 5A: SpreadIndicator and RateOfChangeIndicator verification vectors",
        "cases": {k: v for k, v in cases.items()
                  if v["indicator"] in ("SpreadIndicator", "RateOfChangeIndicator")},
    })


# ═══════════════════════════════════════════════════════════════════════════
# Main
# ═══════════════════════════════════════════════════════════════════════════

if __name__ == "__main__":
    generate_indicator_vectors()

    # Report
    print(f"\n{'═' * 60}")
    hard_pass = sum(1 for _, p in HARD_CHECKS if p)
    hard_fail = sum(1 for _, p in HARD_CHECKS if not p)
    print(f"HARD CHECKS: {hard_pass}/{len(HARD_CHECKS)} passed" +
          (f" ({hard_fail} FAILED)" if hard_fail else ""))

    qual_pass = sum(1 for _, p, _ in QUALITY_CHECKS if p)
    qual_fail = sum(1 for _, p, _ in QUALITY_CHECKS if not p)
    print(f"QUALITY CHECKS: {qual_pass}/{len(QUALITY_CHECKS)} passed" +
          (f" ({qual_fail} FAILED)" if qual_fail else ""))

    if hard_fail:
        print("\nFAILED HARD CHECKS:")
        for name, passed in HARD_CHECKS:
            if not passed:
                print(f"  ✗ {name}")

    if qual_fail:
        print("\nFAILED QUALITY CHECKS:")
        for name, passed, detail in QUALITY_CHECKS:
            if not passed:
                print(f"  ✗ {name} ({detail})")

    print(f"\nDone. Generated 3 vector files.")
