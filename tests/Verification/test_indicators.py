"""
Verify indicator calculations against numpy / pandas.

Maps to C#: SimpleMovingAverage, ExponentialMovingAverage,
            RealizedVolatility, MomentumScore
"""

import numpy as np
import pandas as pd
import pytest

from conftest import PRECISION_EXACT, PRECISION_NUMERIC, TRADING_DAYS_PER_YEAR, load_vector


# ---------------------------------------------------------------------------
# Independent calculations matching C# implementations
# ---------------------------------------------------------------------------

def sma(values: np.ndarray, period: int) -> float:
    """Average of last `period` values."""
    return float(np.mean(values[-period:]))


def ema(values: np.ndarray, period: int) -> float:
    """EMA with SMA seed, multiplier = 2/(period+1)."""
    multiplier = 2.0 / (period + 1)
    result = np.mean(values[:period])  # SMA seed
    for v in values[period:]:
        result = (v - result) * multiplier + result
    return float(result)


def realized_volatility(returns: np.ndarray, window: int, td: int = 252) -> float:
    """Sample std of last `window` returns, annualized."""
    window_returns = returns[-window:]
    std = np.std(window_returns, ddof=1)
    return float(std * np.sqrt(td))


def momentum_score(
    daily_returns: np.ndarray,
    total_months: int = 12,
    skip_months: int = 1,
    days_per_month: int = 21,
) -> float:
    """Cumulative return from (end - total*dpm) to (end - skip*dpm)."""
    required = total_months * days_per_month
    skip_days = skip_months * days_per_month
    start = len(daily_returns) - required
    end = len(daily_returns) - skip_days
    return float(np.prod(1 + daily_returns[start:end]) - 1)


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

class TestSMA:
    def test_against_vector(self, daily_returns):
        vec = load_vector("indicators")
        values = np.array(vec["inputs"]["values"])
        period = vec["inputs"]["period"]
        result = sma(values, period)
        assert result == pytest.approx(vec["expected"]["sma"], abs=PRECISION_EXACT)

    def test_against_pandas(self, daily_returns):
        """Cross-check: pandas rolling mean."""
        prices = np.cumsum(daily_returns) + 100
        period = 20
        pd_sma = pd.Series(prices).rolling(period).mean().iloc[-1]
        result = sma(prices, period)
        assert result == pytest.approx(float(pd_sma), abs=PRECISION_EXACT)

    def test_full_window(self):
        """When data == period, SMA is just the mean."""
        data = np.array([1.0, 2.0, 3.0, 4.0, 5.0])
        assert sma(data, 5) == pytest.approx(3.0, abs=PRECISION_EXACT)


class TestEMA:
    def test_against_vector(self, daily_returns):
        vec = load_vector("indicators")
        values = np.array(vec["inputs"]["values"])
        period = vec["inputs"]["period"]
        result = ema(values, period)
        assert result == pytest.approx(vec["expected"]["ema"], abs=PRECISION_EXACT)

    def test_against_pandas(self, daily_returns):
        """Cross-check: pandas EWM with adjust=False and SMA seed."""
        prices = np.cumsum(daily_returns) + 100
        period = 20
        # Pandas ewm with adjust=False is equivalent to C# EMA
        pd_ema = pd.Series(prices).ewm(span=period, adjust=False).mean().iloc[-1]
        result = ema(prices, period)
        # Note: pandas uses different seeding, so may differ slightly
        # Our C# seeds with SMA(first N), pandas seeds differently
        # Accept looser tolerance
        assert abs(result - float(pd_ema)) < 0.5  # sanity check, not exact match


class TestRealizedVolatility:
    def test_against_vector(self, daily_returns):
        vec = load_vector("indicators")
        window = vec["inputs"]["vol_window"]
        result = realized_volatility(daily_returns, window)
        assert result == pytest.approx(vec["expected"]["realized_volatility"], abs=PRECISION_EXACT)

    def test_scales_with_annualization(self, daily_returns):
        """252-day annualization should produce larger value than daily."""
        window = 20
        daily_std = float(np.std(daily_returns[-window:], ddof=1))
        ann = realized_volatility(daily_returns, window)
        assert ann == pytest.approx(daily_std * np.sqrt(252), abs=PRECISION_EXACT)


class TestMomentumScore:
    def test_against_vector(self, daily_returns):
        vec = load_vector("indicators")
        result = momentum_score(daily_returns)
        expected = vec["expected"]["momentum_score"]
        if expected is not None:
            assert result == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_positive_returns_give_positive_momentum(self):
        """All positive returns should give positive momentum."""
        r = np.full(252, 0.001)
        mom = momentum_score(r)
        assert mom > 0

    def test_skip_excludes_recent(self):
        """Skipping 1 month should exclude last 21 days."""
        rng = np.random.default_rng(77)
        r = rng.normal(0.001, 0.01, 252)
        # Set last 21 days to extreme negative
        r[-21:] = -0.05
        mom = momentum_score(r, skip_months=1)
        # Momentum should not reflect the recent crash
        # (it looks at days [0..231] in a 252-day series)
        mom_no_skip = momentum_score(r, total_months=12, skip_months=0, days_per_month=21)
        assert mom > mom_no_skip  # skipping avoids the recent crash
