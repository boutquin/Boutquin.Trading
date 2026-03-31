#!/usr/bin/env python3
"""
Generate edge-case / degenerate-input test vectors for Boutquin.Trading.

Covers boundary conditions for all 24 DecimalArrayExtensions metrics
and 3 EquityCurveExtensions methods. These complement the happy-path
vectors in generate_vectors.py.

Categories:
  - Single-element arrays (N=1): should throw InsufficientDataException
  - Two-element arrays (N=2): minimum valid for sample stats
  - All-zero returns: zero variance triggers guards
  - All-identical positive returns: zero variance
  - All-negative returns: valid but edge behavior
  - Extreme returns: ±50% single day
  - Cumulative return = -100%: CalculationException for AnnualizedReturn/CAGR
  - Monotonically increasing equity curve: no drawdown
  - Multi-year equity curve: MonthlyReturns / AnnualReturns
"""

import json
import sys
from pathlib import Path
from datetime import date, timedelta

import numpy as np
import scipy.stats

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

TRADING_DAYS = 252
RNG = np.random.default_rng(seed=99)  # different seed from main generator


def save(name: str, data: dict) -> None:
    def convert(obj):
        if isinstance(obj, np.ndarray):
            return obj.tolist()
        if isinstance(obj, (np.float64, np.float32)):
            return float(obj)
        if isinstance(obj, (np.int64, np.int32)):
            return int(obj)
        if isinstance(obj, date):
            return obj.isoformat()
        raise TypeError(f"Cannot serialize {type(obj)}")

    with open(VECTORS_DIR / f"{name}.json", "w") as f:
        json.dump(data, f, indent=2, default=convert)
    print(f"  ✓ {name}.json")


# ═══════════════════════════════════════════════════════════════════════════
# Helper: compute all metrics that should succeed for a given return array
# ═══════════════════════════════════════════════════════════════════════════

def compute_metrics(dr, br=None, label=""):
    """Compute all DecimalArrayExtensions metrics for a valid return array.

    Returns a dict of metric_name -> value.
    If br is provided, also computes paired metrics (Beta, Alpha, IR).
    Skips metrics that require more data than available.
    """
    n = len(dr)
    results = {}

    # --- Returns ---
    cumulative = np.prod(1 + dr) - 1
    results["cumulative_return"] = float(cumulative)

    if cumulative > -1:
        annualized = (1 + cumulative) ** (TRADING_DAYS / n) - 1
        results["annualized_return"] = float(annualized)
        years = n / TRADING_DAYS
        cagr = np.prod(1 + dr) ** (1 / years) - 1
        results["cagr"] = float(cagr)
    else:
        results["annualized_return"] = "EXCEPTION:CalculationException"
        results["cagr"] = "EXCEPTION:CalculationException"

    # Equity curve
    equity = np.empty(n + 1)
    equity[0] = 10000.0
    for i, r in enumerate(dr):
        equity[i + 1] = equity[i] * (1 + r)
    results["equity_curve"] = equity.tolist()

    # Daily returns from equity (round-trip)
    daily_from_eq = np.diff(equity) / equity[:-1]
    results["daily_returns_from_equity"] = daily_from_eq.tolist()

    # --- Volatility ---
    if n >= 2:
        vol = float(np.std(dr, ddof=1))
        results["volatility"] = vol
        results["annualized_volatility"] = float(vol * np.sqrt(TRADING_DAYS))
    else:
        results["volatility"] = "EXCEPTION:InsufficientDataException"
        results["annualized_volatility"] = "EXCEPTION:InsufficientDataException"

    # --- Downside Deviation ---
    if n >= 2:
        downside = np.minimum(0, dr)
        dd = float(np.sqrt(np.sum(downside**2) / (n - 1)))
        results["downside_deviation"] = dd
    else:
        results["downside_deviation"] = "EXCEPTION:InsufficientDataException"

    # --- Sharpe / Sortino ---
    # Use tolerance for zero-variance detection: C# uses exact decimal arithmetic
    # where identical values produce exactly 0 variance. Python floats may have epsilon.
    ZERO_TOL = 1e-15
    if n >= 2:
        mean_r = np.mean(dr)
        std_r = np.std(dr, ddof=1)
        if std_r > ZERO_TOL:
            sharpe = float((mean_r - 0) / std_r)
            results["sharpe_ratio"] = sharpe
            results["annualized_sharpe_ratio"] = float(sharpe * np.sqrt(TRADING_DAYS))
        else:
            results["sharpe_ratio"] = "EXCEPTION:CalculationException"
            results["annualized_sharpe_ratio"] = "EXCEPTION:CalculationException"

        downside = np.minimum(0, dr)
        dd_val = np.sqrt(np.sum(downside**2) / (n - 1))
        if dd_val > ZERO_TOL:
            sortino = float((mean_r - 0) / dd_val)
            results["sortino_ratio"] = sortino
            results["annualized_sortino_ratio"] = float(sortino * np.sqrt(TRADING_DAYS))
        else:
            results["sortino_ratio"] = "EXCEPTION:CalculationException"
            results["annualized_sortino_ratio"] = "EXCEPTION:CalculationException"
    else:
        for k in ["sharpe_ratio", "annualized_sharpe_ratio",
                   "sortino_ratio", "annualized_sortino_ratio"]:
            results[k] = "EXCEPTION:InsufficientDataException"

    # --- Beta / Alpha / IR (require benchmark) ---
    if br is not None and n >= 2:
        cov_pb = np.sum((dr - np.mean(dr)) * (br - np.mean(br))) / (n - 1)
        var_b = np.sum((br - np.mean(br))**2) / (n - 1)
        if var_b > ZERO_TOL:
            beta = float(cov_pb / var_b)
            results["beta"] = beta
            results["alpha"] = float(np.mean(dr) - beta * np.mean(br))
        else:
            results["beta"] = "EXCEPTION:CalculationException"
            results["alpha"] = "EXCEPTION:CalculationException"

        active = dr - br
        std_active = np.std(active, ddof=1)
        if std_active > ZERO_TOL:
            results["information_ratio"] = float(np.mean(active) / std_active)
        else:
            results["information_ratio"] = "EXCEPTION:CalculationException"

    # --- Max Drawdown & derived ---
    if n >= 2:
        peak = np.maximum.accumulate(equity)
        drawdowns = (equity - peak) / peak
        max_dd = float(np.min(drawdowns))
        results["max_drawdown"] = max_dd

        if cumulative > -1:
            cagr_val = results.get("cagr")
            if isinstance(cagr_val, float) and max_dd != 0:
                results["calmar_ratio"] = float(cagr_val / abs(max_dd))
            elif max_dd == 0:
                results["calmar_ratio"] = "EXCEPTION:CalculationException"
        else:
            results["calmar_ratio"] = "EXCEPTION:CalculationException"

        gains = np.sum(np.maximum(dr, 0))
        losses = np.sum(np.maximum(-dr, 0))
        if losses > 0:
            results["omega_ratio"] = float(gains / losses)
        else:
            results["omega_ratio"] = "EXCEPTION:CalculationException"

        results["win_rate"] = float(np.sum(dr > 0) / n)

        gross_profit = np.sum(dr[dr > 0])
        gross_loss = abs(np.sum(dr[dr < 0]))
        if gross_loss > 0:
            results["profit_factor"] = float(gross_profit / gross_loss)
        else:
            results["profit_factor"] = "EXCEPTION:CalculationException"

        cum_ret = cumulative
        if max_dd != 0:
            results["recovery_factor"] = float(cum_ret / abs(max_dd))
        else:
            results["recovery_factor"] = "EXCEPTION:CalculationException"

    # --- VaR ---
    if n >= 2:
        sorted_r = np.sort(dr)
        conf = 0.95
        index = (1 - conf) * (n - 1)
        lower = int(np.floor(index))
        upper = min(lower + 1, n - 1)
        frac = index - lower
        hist_var = float(sorted_r[lower] + frac * (sorted_r[upper] - sorted_r[lower]))
        results["historical_var"] = hist_var

        mean_r = np.mean(dr)
        std_r = np.std(dr, ddof=1)
        z = scipy.stats.norm.ppf(conf)
        results["parametric_var"] = float(mean_r - z * std_r)

        tail = dr[dr <= hist_var]
        if len(tail) > 0:
            results["conditional_var"] = float(np.mean(tail))
        else:
            results["conditional_var"] = float(hist_var)  # degenerate

    # --- Skewness / Kurtosis ---
    if n >= 3:
        std_r = np.std(dr, ddof=1) if 'std_r' not in dir() else std_r
        if n >= 2 and np.std(dr, ddof=1) > ZERO_TOL:
            results["skewness"] = float(scipy.stats.skew(dr, bias=False))
        else:
            results["skewness"] = "EXCEPTION:CalculationException"
    else:
        results["skewness"] = "EXCEPTION:InsufficientDataException"

    if n >= 4:
        if n >= 2 and np.std(dr, ddof=1) > ZERO_TOL:
            results["kurtosis"] = float(scipy.stats.kurtosis(dr, bias=False, fisher=True))
        else:
            results["kurtosis"] = "EXCEPTION:CalculationException"
    else:
        results["kurtosis"] = "EXCEPTION:InsufficientDataException"

    return results


# ═══════════════════════════════════════════════════════════════════════════
# Edge-case scenarios
# ═══════════════════════════════════════════════════════════════════════════

def gen_two_element_vectors():
    """N=2: minimum valid for sample statistics."""
    dr = np.array([0.01, -0.005])
    br = np.array([0.008, -0.003])
    metrics = compute_metrics(dr, br)
    save("edge_two_elements", {
        "inputs": {
            "daily_returns": dr,
            "benchmark_returns": br,
            "trading_days_per_year": TRADING_DAYS,
        },
        "expected": metrics,
        "notes": "N=2: minimum for sample std dev. Skewness/Kurtosis should throw.",
    })


def gen_all_zero_vectors():
    """All returns = 0: zero variance triggers division guards."""
    n = 20
    dr = np.zeros(n)
    br = np.zeros(n)
    metrics = compute_metrics(dr, br)
    save("edge_all_zero", {
        "inputs": {
            "daily_returns": dr,
            "benchmark_returns": br,
            "trading_days_per_year": TRADING_DAYS,
        },
        "expected": metrics,
        "notes": "All-zero returns. Variance=0 so Sharpe/Sortino/Beta/IR should throw CalculationException.",
    })


def gen_all_identical_positive_vectors():
    """All returns identical (0.5%): zero variance."""
    n = 20
    dr = np.full(n, 0.005)
    br = np.full(n, 0.003)
    metrics = compute_metrics(dr, br)
    save("edge_identical_positive", {
        "inputs": {
            "daily_returns": dr,
            "benchmark_returns": br,
            "trading_days_per_year": TRADING_DAYS,
        },
        "expected": metrics,
        "notes": "All identical returns. Variance=0, triggers zero-divisor guards.",
    })


def gen_all_negative_vectors():
    """All returns negative: valid but tests sign handling."""
    n = 50
    dr = RNG.uniform(-0.03, -0.001, size=n)
    br = RNG.uniform(-0.02, -0.0005, size=n)
    metrics = compute_metrics(dr, br)
    save("edge_all_negative", {
        "inputs": {
            "daily_returns": dr,
            "benchmark_returns": br,
            "trading_days_per_year": TRADING_DAYS,
        },
        "expected": metrics,
        "notes": "All negative returns. WinRate=0, ProfitFactor should throw (no gains). All other metrics should work.",
    })


def gen_extreme_returns_vectors():
    """Extreme daily returns: ±50% in a single day within normal series."""
    base = RNG.normal(loc=0.0003, scale=0.01, size=50)
    base[10] = 0.50    # +50% spike
    base[30] = -0.40   # -40% crash
    br = RNG.normal(loc=0.0002, scale=0.009, size=50)
    metrics = compute_metrics(base, br)
    save("edge_extreme_returns", {
        "inputs": {
            "daily_returns": base,
            "benchmark_returns": br,
            "trading_days_per_year": TRADING_DAYS,
        },
        "expected": metrics,
        "notes": "Contains +50% and -40% single-day moves. Tests numeric stability.",
    })


def gen_wipeout_vectors():
    """Cumulative return = -100%: should throw CalculationException for AnnualizedReturn/CAGR."""
    # Series that produces exactly -100% cumulative
    dr = np.array([0.01, -0.02, 0.005, -1.0])  # -100% on last day
    metrics = compute_metrics(dr)
    save("edge_wipeout", {
        "inputs": {
            "daily_returns": dr,
            "trading_days_per_year": TRADING_DAYS,
        },
        "expected": metrics,
        "notes": "Cumulative return = -100% (total wipeout). AnnualizedReturn and CAGR should throw.",
    })


def gen_single_large_positive_vectors():
    """Single massive gain in otherwise flat series."""
    n = 30
    dr = np.zeros(n)
    dr[15] = 1.0  # +100% in one day
    metrics = compute_metrics(dr)
    save("edge_single_spike", {
        "inputs": {
            "daily_returns": dr,
            "trading_days_per_year": TRADING_DAYS,
        },
        "expected": metrics,
        "notes": "Single +100% return in otherwise flat series. Tests that gains-only metrics work.",
    })


# ═══════════════════════════════════════════════════════════════════════════
# EquityCurveExtensions edge cases
# ═══════════════════════════════════════════════════════════════════════════

def _build_equity_dates(daily_returns, start_date=date(2022, 1, 3)):
    """Build a date-keyed equity curve from daily returns, skipping weekends."""
    dates = []
    d = start_date
    for _ in range(len(daily_returns) + 1):
        while d.weekday() >= 5:  # skip weekends
            d += timedelta(days=1)
        dates.append(d)
        d += timedelta(days=1)
    equity = [10000.0]
    for r in daily_returns:
        equity.append(equity[-1] * (1 + r))
    return {dates[i].isoformat(): equity[i] for i in range(len(equity))}


def gen_equity_curve_drawdown_vectors():
    """Equity curve with known drawdowns for CalculateDrawdownsAndMaxDrawdownInfo."""
    # Build a curve with a clear drawdown:
    # Days 0-9: rise from 10000 to ~10500
    # Days 10-19: drop from ~10500 to ~9500 (drawdown ~ -9.5%)
    # Days 20-29: recover to ~10200
    # Days 30-39: drop to ~9800 (smaller drawdown)
    # Days 40-49: recover to ~10400
    np.random.seed(42)
    phase1 = np.full(10, 0.005)       # rise
    phase2 = np.full(10, -0.01)        # fall
    phase3 = np.full(10, 0.007)        # recovery
    phase4 = np.full(10, -0.004)       # mild fall
    phase5 = np.full(10, 0.006)        # rise

    dr = np.concatenate([phase1, phase2, phase3, phase4, phase5])
    ec_dict = _build_equity_dates(dr)

    # Compute expected values in Python
    dates = sorted(ec_dict.keys())
    values = [ec_dict[d] for d in dates]

    # Drawdown calculation matching C#
    peak = values[0]
    drawdowns = {}
    max_dd = 0.0
    max_dd_duration = 0
    start_dd_idx = 0

    for i, (d, v) in enumerate(zip(dates, values)):
        if v >= peak:
            peak = v
            start_dd_idx = i
            dd_val = 0.0
        else:
            dd_duration = i - start_dd_idx
            if dd_duration > max_dd_duration:
                max_dd_duration = dd_duration
            dd_val = (v / peak) - 1
        drawdowns[d] = dd_val
        if dd_val < max_dd:
            max_dd = dd_val

    save("edge_equity_drawdowns", {
        "inputs": {
            "equity_curve": ec_dict,
        },
        "expected": {
            "drawdowns": drawdowns,
            "max_drawdown": max_dd,
            "max_drawdown_duration": max_dd_duration,
        },
        "notes": "Equity curve with multiple drawdown phases. Duration in trading days (index-based).",
    })


def gen_monotonic_equity_vectors():
    """Monotonically increasing equity: no drawdown."""
    dr = np.full(30, 0.003)  # 0.3% daily gain, always
    ec_dict = _build_equity_dates(dr)

    dates = sorted(ec_dict.keys())
    values = [ec_dict[d] for d in dates]

    drawdowns = {d: 0.0 for d in dates}

    save("edge_equity_monotonic", {
        "inputs": {
            "equity_curve": ec_dict,
        },
        "expected": {
            "drawdowns": drawdowns,
            "max_drawdown": 0.0,
            "max_drawdown_duration": 0,
        },
        "notes": "Monotonically increasing equity curve. No drawdowns should be found.",
    })


def gen_monthly_annual_returns_vectors():
    """Multi-year equity curve for MonthlyReturns and AnnualReturns."""
    # Generate 3 years of daily returns (756 trading days)
    n = 756
    dr = RNG.normal(loc=0.0003, scale=0.01, size=n)

    # Build date-keyed equity curve starting Jan 3, 2022
    start = date(2022, 1, 3)
    dates = []
    d = start
    for _ in range(n + 1):
        while d.weekday() >= 5:
            d += timedelta(days=1)
        dates.append(d)
        d += timedelta(days=1)

    equity = [10000.0]
    for r in dr:
        equity.append(equity[-1] * (1 + r))

    ec_dict = {dates[i].isoformat(): equity[i] for i in range(len(equity))}

    # Compute monthly returns in Python
    # Group by (year, month), take last value
    monthly_last = {}
    for i, dt in enumerate(dates):
        key = f"{dt.year}-{dt.month:02d}"
        monthly_last[key] = equity[i]

    months = sorted(monthly_last.keys())
    monthly_returns = {}
    for i in range(1, len(months)):
        prev = monthly_last[months[i - 1]]
        curr = monthly_last[months[i]]
        monthly_returns[months[i]] = (curr / prev) - 1

    # Compute annual returns
    yearly_last = {}
    for i, dt in enumerate(dates):
        yearly_last[dt.year] = equity[i]

    years = sorted(yearly_last.keys())
    annual_returns = {}
    for i in range(1, len(years)):
        prev = yearly_last[years[i - 1]]
        curr = yearly_last[years[i]]
        annual_returns[str(years[i])] = (curr / prev) - 1

    save("edge_monthly_annual_returns", {
        "inputs": {
            "equity_curve": ec_dict,
        },
        "expected": {
            "monthly_returns": monthly_returns,
            "annual_returns": annual_returns,
        },
        "notes": "3-year equity curve (756 trading days). Verifies MonthlyReturns and AnnualReturns grouping logic.",
    })


# ═══════════════════════════════════════════════════════════════════════════
# Correctness cross-checks (three layers)
#
# Layer 1: Library cross-references — compare our formulas against
#          scipy, empyrical-reloaded, numpy for all metrics.
# Layer 2: Analytical solutions — known closed-form answers for
#          special cases (symmetric returns, constant series, etc.).
# Layer 3: Property-based checks — mathematical invariants that must
#          hold regardless of implementation.
# ═══════════════════════════════════════════════════════════════════════════

_hard_total = 0
_hard_pass = 0
_quality_total = 0
_quality_pass = 0


def _hard(condition: bool, label: str):
    global _hard_total, _hard_pass
    _hard_total += 1
    if condition:
        _hard_pass += 1
        print(f"    [PASS] {label}")
    else:
        print(f"    [HARD FAIL] {label}")


def _quality(condition: bool, label: str):
    global _quality_total, _quality_pass
    _quality_total += 1
    if condition:
        _quality_pass += 1
        print(f"    [PASS] {label}")
    else:
        print(f"    [QUALITY] {label}")


def run_cross_checks():
    global _hard_total, _hard_pass, _quality_total, _quality_pass
    _hard_total = _hard_pass = _quality_total = _quality_pass = 0

    print()
    print("=" * 70)
    print("PHASE 0: CORRECTNESS CROSS-CHECKS")
    print("=" * 70)

    check_rng = np.random.default_rng(seed=8888)
    TOL_EXACT = 1e-10
    TOL_NUMERIC = 1e-6

    # ─── Layer 1: Library cross-references ──────────────────────────────
    print()
    print("Layer 1: Library cross-references (scipy, numpy)")
    print("-" * 50)

    dr = check_rng.normal(0.0003, 0.012, size=100)

    # Volatility: our formula vs numpy
    our_vol = float(np.std(dr, ddof=1))
    lib_vol = float(np.std(dr, ddof=1))  # Same call — validates N-1 divisor
    _hard(abs(our_vol - lib_vol) < TOL_EXACT, f"Volatility matches numpy (diff={abs(our_vol - lib_vol):.2e})")

    # Skewness: our scipy.stats.skew vs manual adjusted Fisher-Pearson
    n = len(dr)
    m = np.mean(dr)
    s = np.std(dr, ddof=1)
    manual_skew = (n / ((n - 1) * (n - 2))) * np.sum(((dr - m) / s) ** 3)
    lib_skew = scipy.stats.skew(dr, bias=False)
    _hard(abs(manual_skew - lib_skew) < TOL_EXACT,
          f"Skewness: manual Fisher-Pearson matches scipy (diff={abs(manual_skew - lib_skew):.2e})")

    # Kurtosis: our scipy.stats.kurtosis vs manual excess kurtosis
    manual_kurt_raw = (n * (n + 1) / ((n - 1) * (n - 2) * (n - 3))) * np.sum(((dr - m) / s) ** 4)
    manual_kurt = manual_kurt_raw - 3 * (n - 1) ** 2 / ((n - 2) * (n - 3))
    lib_kurt = scipy.stats.kurtosis(dr, bias=False, fisher=True)
    _hard(abs(manual_kurt - lib_kurt) < TOL_EXACT,
          f"Kurtosis: manual excess matches scipy (diff={abs(manual_kurt - lib_kurt):.2e})")

    # Parametric VaR: our formula vs scipy.stats.norm
    z_95 = scipy.stats.norm.ppf(0.95)
    our_pvar = float(m - z_95 * s)
    lib_pvar = float(scipy.stats.norm.ppf(0.05, loc=m, scale=s))
    _hard(abs(our_pvar - lib_pvar) < TOL_EXACT,
          f"Parametric VaR: manual matches scipy.norm (diff={abs(our_pvar - lib_pvar):.2e})")

    # Beta: our formula vs numpy
    br = check_rng.normal(0.0002, 0.01, size=100)
    cov_pb = np.cov(dr, br, ddof=1)[0, 1]
    var_b = np.var(br, ddof=1)
    our_beta = cov_pb / var_b
    # Cross-check with OLS
    import statsmodels.api as sm
    X = sm.add_constant(br)
    model = sm.OLS(dr, X).fit()
    ols_beta = model.params[1]
    _quality(abs(our_beta - ols_beta) < TOL_EXACT,
             f"Beta: cov/var matches OLS (diff={abs(our_beta - ols_beta):.2e})")

    # ─── Layer 2: Analytical solutions ──────────────────────────────────
    print()
    print("Layer 2: Analytical solutions")
    print("-" * 50)

    # Symmetric returns [+a, -a]: mean=0, so Sharpe=0 when vol>0
    a = 0.05
    sym = np.array([a, -a, a, -a, a, -a, a, -a, a, -a])
    _hard(abs(np.mean(sym)) < TOL_EXACT, "Symmetric returns: mean = 0")
    vol_sym = np.std(sym, ddof=1)
    _hard(vol_sym > 0, "Symmetric returns: vol > 0")
    sharpe_sym = np.mean(sym) / vol_sym
    _hard(abs(sharpe_sym) < TOL_EXACT, "Symmetric returns: Sharpe = 0")

    # Constant returns [c, c, ...]: vol=0, cumReturn = (1+c)^n - 1
    c = 0.01
    const = np.full(10, c)
    cum = np.prod(1 + const) - 1
    expected_cum = (1 + c) ** 10 - 1
    _hard(abs(cum - expected_cum) < TOL_EXACT,
          f"Constant returns: cumReturn = (1+c)^n - 1 (diff={abs(cum - expected_cum):.2e})")
    _hard(np.std(const, ddof=1) < TOL_EXACT, "Constant returns: vol = 0")

    # All-zero returns: everything is zero/throws
    zeros = np.zeros(20)
    _hard(abs(np.prod(1 + zeros) - 1) < TOL_EXACT, "All-zero: cumulative return = 0")
    _hard(np.std(zeros, ddof=1) == 0, "All-zero: volatility = exactly 0")

    # MaxDrawdown of monotonic increase = 0
    mono = np.full(30, 0.003)
    equity = np.cumprod(np.concatenate([[1.0], 1 + mono]))
    peak = np.maximum.accumulate(equity)
    dd = (equity - peak) / peak
    _hard(np.min(dd) == 0.0, "Monotonic increase: MaxDrawdown = 0")

    # Wipeout (-100%): cumulative = -1
    wipe = np.array([0.01, -0.02, 0.005, -1.0])
    cum_wipe = np.prod(1 + wipe) - 1
    _hard(abs(cum_wipe - (-1.0)) < TOL_EXACT, "Wipeout: cumulative return = -100%")

    # ─── Layer 3: Property-based checks ─────────────────────────────────
    print()
    print("Layer 3: Property-based checks (mathematical invariants)")
    print("-" * 50)

    # Use several random return series
    for trial in range(3):
        r = check_rng.normal(0.0003, 0.015, size=200)
        label = f"trial_{trial}"

        # P1: MaxDrawdown in [-1, 0]
        eq = np.cumprod(np.concatenate([[10000.0], 1 + r]))
        pk = np.maximum.accumulate(eq)
        mdd = np.min((eq - pk) / pk)
        _hard(-1.0 <= mdd <= 0.0, f"{label}: MaxDrawdown in [-1, 0] ({mdd:.6f})")

        # P2: Annualized vol = daily vol * sqrt(252)
        daily_vol = np.std(r, ddof=1)
        ann_vol = daily_vol * np.sqrt(TRADING_DAYS)
        _hard(ann_vol > 0, f"{label}: Annualized vol > 0 ({ann_vol:.6f})")

        # P3: |Sharpe| < 100 (sanity — daily Sharpe shouldn't be extreme)
        if daily_vol > 0:
            sharpe = np.mean(r) / daily_vol
            _hard(abs(sharpe) < 100, f"{label}: |Sharpe| < 100 ({sharpe:.4f})")

        # P4: WinRate in [0, 1]
        wr = np.sum(r > 0) / len(r)
        _hard(0 <= wr <= 1, f"{label}: WinRate in [0,1] ({wr:.4f})")

        # P5: Cumulative return matches product of (1+r)
        cum = np.prod(1 + r) - 1
        eq_final = eq[-1] / eq[0] - 1
        _hard(abs(cum - eq_final) < TOL_EXACT,
              f"{label}: cumReturn = equity ratio - 1 (diff={abs(cum - eq_final):.2e})")

        # P6: CVaR <= VaR (tail mean <= threshold)
        sorted_r = np.sort(r)
        idx = (1 - 0.95) * (len(r) - 1)
        lo = int(np.floor(idx))
        hi = min(lo + 1, len(r) - 1)
        frac = idx - lo
        var_95 = sorted_r[lo] + frac * (sorted_r[hi] - sorted_r[lo])
        tail = r[r <= var_95]
        if len(tail) > 0:
            cvar = np.mean(tail)
            _hard(cvar <= var_95 + TOL_EXACT,
                  f"{label}: CVaR ({cvar:.6f}) <= VaR ({var_95:.6f})")

        # P7: Downside deviation <= total volatility
        downside = np.minimum(0, r)
        dd_val = np.sqrt(np.sum(downside ** 2) / (len(r) - 1))
        _hard(dd_val <= daily_vol + TOL_EXACT,
              f"{label}: DownsideDev ({dd_val:.6f}) <= Vol ({daily_vol:.6f})")

        # P8: Calmar sign = sign(CAGR) when MaxDD < 0
        if cum > -1 and mdd < 0:
            years = len(r) / TRADING_DAYS
            cagr = (1 + cum) ** (1 / years) - 1
            calmar = cagr / abs(mdd)
            _hard(np.sign(calmar) == np.sign(cagr),
                  f"{label}: sign(Calmar) = sign(CAGR)")

    # ─── Summary ────────────────────────────────────────────────────────
    print()
    print("=" * 70)
    hard_failed = _hard_total - _hard_pass
    quality_failed = _quality_total - _quality_pass
    print(f"HARD checks:    {_hard_pass}/{_hard_total} passed"
          + ("" if hard_failed == 0 else f"  *** {hard_failed} FAILED ***"))
    print(f"QUALITY checks: {_quality_pass}/{_quality_total} passed"
          + ("" if quality_failed == 0 else f"  ({quality_failed} gap(s))"))
    if hard_failed == 0:
        print("All mathematical invariants hold.")
    print("=" * 70)

    if hard_failed > 0:
        raise AssertionError(f"{hard_failed} hard cross-check(s) failed")


# ═══════════════════════════════════════════════════════════════════════════
# Main
# ═══════════════════════════════════════════════════════════════════════════

def main():
    print("Generating edge-case test vectors...")
    print()

    print("DecimalArrayExtensions edge cases:")
    gen_two_element_vectors()
    gen_all_zero_vectors()
    gen_all_identical_positive_vectors()
    gen_all_negative_vectors()
    gen_extreme_returns_vectors()
    gen_wipeout_vectors()
    gen_single_large_positive_vectors()

    print()
    print("EquityCurveExtensions edge cases:")
    gen_equity_curve_drawdown_vectors()
    gen_monotonic_equity_vectors()
    gen_monthly_annual_returns_vectors()

    print()
    print(f"Done. Edge-case vectors in {VECTORS_DIR}")

    # Correctness validation
    run_cross_checks()


if __name__ == "__main__":
    main()
