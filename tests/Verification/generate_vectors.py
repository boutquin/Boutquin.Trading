#!/usr/bin/env python3
"""
Generate golden test vectors for Boutquin.Trading verification.

Produces JSON files in vectors/ that are consumed by both:
  - Python pytest tests (self-consistency check)
  - C# xUnit tests (cross-language verification)

Each vector file contains:
  - inputs: the raw data fed to the calculation
  - expected: the result computed by the trusted Python library
  - library: name + version of the library used
  - notes: any convention details (sample vs population, annualisation, etc.)
"""

import json
import sys
from pathlib import Path

import numpy as np
import scipy.stats
import statsmodels.api as sm
from sklearn.covariance import LedoitWolf

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

TRADING_DAYS = 252
RNG = np.random.default_rng(seed=42)


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


def generate_daily_returns():
    """Core dataset: 252 days of realistic returns."""
    returns = RNG.normal(loc=0.0004, scale=0.012, size=TRADING_DAYS)
    save("daily_returns", {"values": returns})
    return returns


def generate_benchmark_returns():
    """Benchmark returns for paired calculations."""
    returns = RNG.normal(loc=0.0003, scale=0.011, size=TRADING_DAYS)
    save("benchmark_returns", {"values": returns})
    return returns


def generate_negative_returns():
    """Negative-drift returns for drawdown tests."""
    returns = RNG.normal(loc=-0.001, scale=0.015, size=TRADING_DAYS)
    save("negative_returns", {"values": returns})
    return returns


def generate_multi_asset_returns():
    """3-asset returns for covariance/correlation tests."""
    raw = RNG.normal(loc=0.0003, scale=0.012, size=(3, TRADING_DAYS))
    # Inject correlation between assets 0 and 1
    raw[1] = 0.6 * raw[0] + 0.4 * raw[1]
    save("multi_asset_returns", {"values": raw})
    return raw


def generate_factor_returns():
    """3 Fama-French-style factor series."""
    mkt = RNG.normal(loc=0.0004, scale=0.01, size=TRADING_DAYS)
    smb = RNG.normal(loc=0.0001, scale=0.005, size=TRADING_DAYS)
    hml = RNG.normal(loc=0.00015, scale=0.004, size=TRADING_DAYS)
    data = {"Mkt-RF": mkt, "SMB": smb, "HML": hml}
    save("factor_returns", {k: v for k, v in data.items()})
    return data


# ─── Return metrics ───────────────────────────────────────────────────────

def gen_return_vectors(dr):
    """AnnualizedReturn, CAGR, DailyReturns, EquityCurve."""
    # Cumulative return
    cumulative = np.prod(1 + dr) - 1
    # Annualized return: (1 + cumulative)^(252/N) - 1
    annualized = (1 + cumulative) ** (TRADING_DAYS / len(dr)) - 1

    # CAGR: same formula but denominator is years = N/252
    years = len(dr) / TRADING_DAYS
    cagr = np.prod(1 + dr) ** (1 / years) - 1

    # Equity curve from returns
    equity = np.empty(len(dr) + 1)
    equity[0] = 10000.0
    for i, r in enumerate(dr):
        equity[i + 1] = equity[i] * (1 + r)

    # Daily returns from equity curve (inverse)
    daily_from_equity = np.diff(equity) / equity[:-1]

    save("returns", {
        "inputs": {"daily_returns": dr, "trading_days_per_year": TRADING_DAYS},
        "expected": {
            "cumulative_return": float(cumulative),
            "annualized_return": float(annualized),
            "cagr": float(cagr),
            "equity_curve": equity,
            "daily_returns_from_equity": daily_from_equity,
        },
        "library": f"numpy {np.__version__}",
        "notes": "Annualized = (1+cumRet)^(252/N)-1. CAGR uses years=N/252.",
    })


# ─── Volatility ───────────────────────────────────────────────────────────

def gen_volatility_vectors(dr):
    """Volatility, AnnualizedVolatility."""
    vol = np.std(dr, ddof=1)  # sample std dev
    ann_vol = vol * np.sqrt(TRADING_DAYS)

    save("volatility", {
        "inputs": {"daily_returns": dr, "trading_days_per_year": TRADING_DAYS},
        "expected": {
            "daily_volatility": float(vol),
            "annualized_volatility": float(ann_vol),
        },
        "library": f"numpy {np.__version__}",
        "notes": "Sample std dev (ddof=1). Annualized = daily * sqrt(252).",
    })


# ─── Downside deviation ──────────────────────────────────────────────────

def gen_downside_deviation_vectors(dr):
    """DownsideDeviation with rf=0."""
    rf = 0.0
    downside = np.minimum(0, dr - rf)
    # C# uses N-1 divisor over ALL returns (full-sample semi-deviation)
    dd = np.sqrt(np.sum(downside**2) / (len(dr) - 1))

    save("downside_deviation", {
        "inputs": {"daily_returns": dr, "risk_free_rate": rf},
        "expected": {"downside_deviation": float(dd)},
        "library": f"numpy {np.__version__}",
        "notes": "Full-sample semi-deviation: min(0, r-rf) squared, sum/(N-1), sqrt. N-1 divisor.",
    })


# ─── Sharpe / Sortino / Information Ratio ────────────────────────────────

def gen_ratio_vectors(dr, br):
    """Sharpe, Sortino, InformationRatio, Alpha, Beta."""
    rf = 0.0

    # Sharpe (daily)
    sharpe = (np.mean(dr) - rf) / np.std(dr, ddof=1)
    ann_sharpe = sharpe * np.sqrt(TRADING_DAYS)

    # Sortino (daily)
    downside = np.minimum(0, dr - rf)
    dd = np.sqrt(np.sum(downside**2) / (len(dr) - 1))
    sortino = (np.mean(dr) - rf) / dd
    ann_sortino = sortino * np.sqrt(TRADING_DAYS)

    # Beta
    cov_pb = np.sum((dr - np.mean(dr)) * (br - np.mean(br))) / (len(dr) - 1)
    var_b = np.sum((br - np.mean(br))**2) / (len(br) - 1)
    beta = cov_pb / var_b

    # Alpha (daily)
    alpha = np.mean(dr) - rf - beta * (np.mean(br) - rf)

    # Information Ratio
    active = dr - br
    ir = np.mean(active) / np.std(active, ddof=1)

    save("ratios", {
        "inputs": {
            "daily_returns": dr,
            "benchmark_returns": br,
            "risk_free_rate": rf,
            "trading_days_per_year": TRADING_DAYS,
        },
        "expected": {
            "sharpe_ratio": float(sharpe),
            "annualized_sharpe_ratio": float(ann_sharpe),
            "sortino_ratio": float(sortino),
            "annualized_sortino_ratio": float(ann_sortino),
            "beta": float(beta),
            "alpha": float(alpha),
            "information_ratio": float(ir),
        },
        "library": f"numpy {np.__version__}",
        "notes": "All use sample std dev (ddof=1). Downside deviation uses N-1 divisor. Annualized = daily * sqrt(252).",
    })


# ─── Calmar, Omega, WinRate, ProfitFactor, RecoveryFactor ────────────────

def gen_derived_ratio_vectors(dr):
    """Ratios derived from returns: Calmar, Omega, WinRate, ProfitFactor, RecoveryFactor."""
    # Max drawdown from equity curve
    equity = np.empty(len(dr) + 1)
    equity[0] = 10000.0
    for i, r in enumerate(dr):
        equity[i + 1] = equity[i] * (1 + r)
    peak = np.maximum.accumulate(equity)
    drawdowns = (equity - peak) / peak
    max_dd = np.min(drawdowns)

    # CAGR
    cumulative = np.prod(1 + dr)
    years = len(dr) / TRADING_DAYS
    cagr = cumulative ** (1 / years) - 1

    # Calmar
    calmar = cagr / abs(max_dd) if max_dd != 0 else float("inf")

    # Omega (threshold=0)
    gains = np.sum(np.maximum(dr, 0))
    losses = np.sum(np.maximum(-dr, 0))
    omega = gains / losses if losses > 0 else float("inf")

    # Win rate
    win_rate = np.sum(dr > 0) / len(dr)

    # Profit factor
    gross_profit = np.sum(dr[dr > 0])
    gross_loss = abs(np.sum(dr[dr < 0]))
    profit_factor = gross_profit / gross_loss if gross_loss > 0 else float("inf")

    # Recovery factor
    cumulative_ret = cumulative - 1
    recovery = cumulative_ret / abs(max_dd) if max_dd != 0 else float("inf")

    save("derived_ratios", {
        "inputs": {"daily_returns": dr, "trading_days_per_year": TRADING_DAYS},
        "expected": {
            "max_drawdown": float(max_dd),
            "calmar_ratio": float(calmar),
            "omega_ratio": float(omega),
            "win_rate": float(win_rate),
            "profit_factor": float(profit_factor),
            "recovery_factor": float(recovery),
        },
        "library": f"numpy {np.__version__}",
        "notes": "MaxDD from equity curve (peak-trough/peak). Omega threshold=0.",
    })


# ─── VaR / CVaR ──────────────────────────────────────────────────────────

def gen_var_vectors(dr):
    """Historical VaR, Parametric VaR, CVaR."""
    confidence = 0.95

    # Historical VaR — linear interpolation at (1-conf) percentile
    # C# uses: index = (1-conf)*(N-1), then linear interp
    sorted_r = np.sort(dr)
    index = (1 - confidence) * (len(sorted_r) - 1)
    lower = int(np.floor(index))
    upper = min(lower + 1, len(sorted_r) - 1)
    fraction = index - lower
    hist_var = sorted_r[lower] + fraction * (sorted_r[upper] - sorted_r[lower])

    # Parametric VaR
    mean = np.mean(dr)
    std = np.std(dr, ddof=1)
    z = scipy.stats.norm.ppf(confidence)
    param_var = mean - z * std

    # CVaR (Expected Shortfall)
    tail = dr[dr <= hist_var]
    cvar = np.mean(tail)

    save("var", {
        "inputs": {
            "daily_returns": dr,
            "confidence_level": confidence,
        },
        "expected": {
            "historical_var": float(hist_var),
            "parametric_var": float(param_var),
            "conditional_var": float(cvar),
        },
        "library": f"numpy {np.__version__}, scipy {scipy.__version__}",
        "notes": "HistVaR: linear interp at (1-conf)*(N-1). ParamVaR: mean - z*std (ddof=1). CVaR: mean of tail <= VaR.",
    })


# ─── Skewness / Kurtosis ─────────────────────────────────────────────────

def gen_statistics_vectors(dr):
    """Adjusted Fisher-Pearson skewness and sample excess kurtosis."""
    n = len(dr)
    mean = np.mean(dr)
    std = np.std(dr, ddof=1)

    # Adjusted Fisher-Pearson skewness: n/((n-1)(n-2)) * sum((x-mean)/std)^3
    m3 = np.sum(((dr - mean) / std) ** 3)
    skew = (n / ((n - 1) * (n - 2))) * m3

    # Sample excess kurtosis (Fisher):
    # [n(n+1)/((n-1)(n-2)(n-3))] * sum((x-mean)/std)^4 - 3(n-1)^2/((n-2)(n-3))
    m4 = np.sum(((dr - mean) / std) ** 4)
    kurt_term1 = (n * (n + 1)) / ((n - 1) * (n - 2) * (n - 3)) * m4
    kurt_term2 = 3 * (n - 1) ** 2 / ((n - 2) * (n - 3))
    kurtosis = kurt_term1 - kurt_term2

    # Cross-check with scipy (should match)
    scipy_skew = scipy.stats.skew(dr, bias=False)
    scipy_kurt = scipy.stats.kurtosis(dr, bias=False, fisher=True)

    save("statistics", {
        "inputs": {"daily_returns": dr},
        "expected": {
            "skewness": float(skew),
            "kurtosis": float(kurtosis),
            "scipy_skewness": float(scipy_skew),
            "scipy_kurtosis": float(scipy_kurt),
        },
        "library": f"numpy {np.__version__}, scipy {scipy.__version__}",
        "notes": "Adjusted Fisher-Pearson skewness. Sample excess kurtosis (Fisher). scipy cross-check included.",
    })


# ─── Indicators ───────────────────────────────────────────────────────────

def gen_indicator_vectors(dr):
    """SMA, EMA, RealizedVolatility, MomentumScore."""
    import pandas as pd

    prices = np.cumsum(dr) + 100  # synthetic price series
    period = 20

    # SMA: average of last 20 values
    sma = np.mean(prices[-period:])

    # EMA: seed with SMA of first `period` values, then apply multiplier
    multiplier = 2.0 / (period + 1)
    ema = np.mean(prices[:period])
    for v in prices[period:]:
        ema = (v - ema) * multiplier + ema

    # Realized Volatility: sample std of last `window` returns, annualized
    window = 20
    window_returns = dr[-window:]
    rv = np.std(window_returns, ddof=1) * np.sqrt(TRADING_DAYS)

    # Momentum Score: 12-1 (cumulative return from 12mo ago to 1mo ago)
    total_months = 12
    skip_months = 1
    days_per_month = 21
    required = total_months * days_per_month
    if len(dr) >= required:
        skip_days = skip_months * days_per_month
        start_idx = len(dr) - required
        end_idx = len(dr) - skip_days
        momentum = np.prod(1 + dr[start_idx:end_idx]) - 1
    else:
        momentum = None

    save("indicators", {
        "inputs": {
            "values": prices,
            "daily_returns": dr,
            "period": period,
            "vol_window": window,
            "momentum_total_months": total_months,
            "momentum_skip_months": skip_months,
            "trading_days_per_month": days_per_month,
        },
        "expected": {
            "sma": float(sma),
            "ema": float(ema),
            "realized_volatility": float(rv),
            "momentum_score": float(momentum) if momentum is not None else None,
        },
        "library": f"numpy {np.__version__}, pandas {pd.__version__}",
        "notes": "SMA=mean(last N). EMA: seed=SMA(first N), mult=2/(N+1). RealVol: std(ddof=1)*sqrt(252). Momentum: 12-1 cumulative.",
    })


# ─── Covariance estimators ───────────────────────────────────────────────

def gen_covariance_vectors(mar):
    """Sample, EWMA, and Ledoit-Wolf covariance matrices."""
    # Sample covariance (N-1 divisor)
    sample_cov = np.cov(mar, ddof=1)  # mar is 3×252

    # EWMA covariance (lambda=0.94)
    lam = 0.94
    n_assets, t = mar.shape
    means = np.mean(mar, axis=1)
    demeaned = mar - means[:, np.newaxis]
    weights = np.array([lam ** (t - 1 - k) for k in range(t)])
    weights /= weights.sum()
    ewma_cov = np.zeros((n_assets, n_assets))
    for i in range(n_assets):
        for j in range(i, n_assets):
            val = np.sum(weights * demeaned[i] * demeaned[j])
            ewma_cov[i, j] = val
            ewma_cov[j, i] = val

    # Ledoit-Wolf (scikit-learn)
    # sklearn expects observations as rows, features as columns
    lw = LedoitWolf().fit(mar.T)
    lw_cov = lw.covariance_
    lw_shrinkage = lw.shrinkage_

    save("covariance", {
        "inputs": {
            "returns": mar,
            "ewma_lambda": lam,
        },
        "expected": {
            "sample_covariance": sample_cov,
            "ewma_covariance": ewma_cov,
            "ledoit_wolf_covariance": lw_cov,
            "ledoit_wolf_shrinkage": float(lw_shrinkage),
        },
        "library": f"numpy {np.__version__}, scikit-learn {__import__('sklearn').__version__}",
        "notes": "Sample: np.cov(ddof=1). EWMA: lambda=0.94 normalized weights. LW: sklearn LedoitWolf (OAS variant may differ from Boutquin implementation).",
    })


# ─── OLS factor regression ───────────────────────────────────────────────

def gen_regression_vectors(dr, factors):
    """Multi-factor OLS regression using statsmodels."""
    # Build portfolio returns as a linear combination + noise
    portfolio = (
        0.02  # daily alpha
        + 1.1 * factors["Mkt-RF"]
        + 0.3 * factors["SMB"]
        - 0.2 * factors["HML"]
        + RNG.normal(0, 0.003, size=len(dr))
    )

    # Statsmodels OLS
    X = np.column_stack([factors["Mkt-RF"], factors["SMB"], factors["HML"]])
    X = sm.add_constant(X)
    model = sm.OLS(portfolio, X).fit()

    save("regression", {
        "inputs": {
            "portfolio_returns": portfolio,
            "factor_names": ["Mkt-RF", "SMB", "HML"],
            "factor_returns": [factors["Mkt-RF"], factors["SMB"], factors["HML"]],
        },
        "expected": {
            "alpha": float(model.params[0]),
            "betas": {
                "Mkt-RF": float(model.params[1]),
                "SMB": float(model.params[2]),
                "HML": float(model.params[3]),
            },
            "r_squared": float(model.rsquared),
            "residual_std_error": float(np.sqrt(model.mse_resid)),
        },
        "library": f"statsmodels {sm.__version__}",
        "notes": "OLS with constant. R² = 1 - SSres/SStot. Residual std error = sqrt(SSres / (T - K - 1)).",
    })
    return portfolio


# ─── Correlation matrix ──────────────────────────────────────────────────

def gen_correlation_vectors(mar):
    """Correlation matrix and diversification ratio."""
    n_assets = mar.shape[0]

    # Correlation matrix via numpy
    corr = np.corrcoef(mar)  # uses N divisor by default

    # But C# uses N-1 covariance then divides by std devs
    cov = np.cov(mar, ddof=1)
    stds = np.sqrt(np.diag(cov))
    corr_sample = cov / np.outer(stds, stds)

    # Diversification ratio with equal weights
    weights = np.ones(n_assets) / n_assets
    weighted_avg_vol = np.sum(weights * stds)
    port_var = weights @ cov @ weights
    port_vol = np.sqrt(port_var)
    div_ratio = weighted_avg_vol / port_vol if port_vol > 0 else 1.0

    save("correlation", {
        "inputs": {
            "returns": mar,
            "weights": weights,
        },
        "expected": {
            "correlation_matrix": corr_sample,
            "diversification_ratio": float(div_ratio),
        },
        "library": f"numpy {np.__version__}",
        "notes": "Correlation from sample covariance (ddof=1). DivRatio = weighted_avg_vol / portfolio_vol.",
    })


# ─── Brinson-Fachler attribution ─────────────────────────────────────────

def gen_attribution_vectors():
    """Brinson-Fachler single-period attribution."""
    assets = ["Equities", "Bonds", "Commodities"]
    pw = {"Equities": 0.6, "Bonds": 0.3, "Commodities": 0.1}
    bw = {"Equities": 0.5, "Bonds": 0.4, "Commodities": 0.1}
    pr = {"Equities": 0.08, "Bonds": 0.03, "Commodities": -0.02}
    br = {"Equities": 0.06, "Bonds": 0.04, "Commodities": -0.01}

    # Total benchmark return
    rb_total = sum(bw[a] * br[a] for a in assets)

    allocation = {}
    selection = {}
    interaction = {}
    for a in assets:
        allocation[a] = (pw[a] - bw[a]) * (br[a] - rb_total)
        selection[a] = bw[a] * (pr[a] - br[a])
        interaction[a] = (pw[a] - bw[a]) * (pr[a] - br[a])

    total_alloc = sum(allocation.values())
    total_sel = sum(selection.values())
    total_inter = sum(interaction.values())
    total_active = total_alloc + total_sel + total_inter

    save("attribution", {
        "inputs": {
            "assets": assets,
            "portfolio_weights": pw,
            "benchmark_weights": bw,
            "portfolio_returns": pr,
            "benchmark_returns": br,
        },
        "expected": {
            "benchmark_total_return": rb_total,
            "allocation_effects": allocation,
            "selection_effects": selection,
            "interaction_effects": interaction,
            "total_allocation": total_alloc,
            "total_selection": total_sel,
            "total_interaction": total_inter,
            "total_active_return": total_active,
        },
        "library": "manual calculation (Brinson-Fachler formula)",
        "notes": "Alloc=(Wp-Wb)(Rb_sector-Rb_total), Sel=Wb(Rp-Rb), Inter=(Wp-Wb)(Rp-Rb).",
    })


# ─── Rolling correlation ─────────────────────────────────────────────────

def gen_rolling_correlation_vectors(mar):
    """Rolling pairwise correlation."""
    import pandas as pd

    window = 60
    a = pd.Series(mar[0])
    b = pd.Series(mar[1])
    rolling_corr = a.rolling(window).corr(b).dropna().values

    save("rolling_correlation", {
        "inputs": {
            "returns_a": mar[0],
            "returns_b": mar[1],
            "window_size": window,
        },
        "expected": {
            "rolling_correlation": rolling_corr,
        },
        "library": f"pandas {pd.__version__}",
        "notes": "pandas rolling correlation uses N-1 divisor internally.",
    })


# ═══════════════════════════════════════════════════════════════════════════
# Main
# ═══════════════════════════════════════════════════════════════════════════

def main():
    print("Generating golden test vectors...")
    print()

    # Generate input datasets
    dr = generate_daily_returns()
    br = generate_benchmark_returns()
    neg = generate_negative_returns()
    mar = generate_multi_asset_returns()
    factors = generate_factor_returns()

    # Generate calculation vectors
    print("Return metrics:")
    gen_return_vectors(dr)

    print("Volatility:")
    gen_volatility_vectors(dr)

    print("Downside deviation:")
    gen_downside_deviation_vectors(dr)

    print("Ratios (Sharpe, Sortino, Beta, Alpha, IR):")
    gen_ratio_vectors(dr, br)

    print("Derived ratios (Calmar, Omega, WinRate, ProfitFactor, Recovery):")
    gen_derived_ratio_vectors(dr)

    print("VaR / CVaR:")
    gen_var_vectors(dr)

    print("Statistics (skewness, kurtosis):")
    gen_statistics_vectors(dr)

    print("Indicators (SMA, EMA, RealizedVol, Momentum):")
    gen_indicator_vectors(dr)

    print("Covariance estimators:")
    gen_covariance_vectors(mar)

    print("Factor regression:")
    gen_regression_vectors(dr, factors)

    print("Correlation analysis:")
    gen_correlation_vectors(mar)

    print("Brinson-Fachler attribution:")
    gen_attribution_vectors()

    print("Rolling correlation:")
    gen_rolling_correlation_vectors(mar)

    print()
    print(f"Done. {len(list(VECTORS_DIR.glob('*.json')))} vector files in {VECTORS_DIR}")


if __name__ == "__main__":
    main()
