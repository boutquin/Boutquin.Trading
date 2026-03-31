"""
Verify risk metrics against numpy / scipy.

Maps to C#: DecimalArrayExtensions.HistoricalVaR, ParametricVaR, ConditionalVaR
            EquityCurveExtensions.CalculateDrawdownsAndMaxDrawdownInfo
"""

import numpy as np
import scipy.stats
import pytest

from conftest import PRECISION_EXACT, PRECISION_NUMERIC, load_vector


# ---------------------------------------------------------------------------
# Independent calculations
# ---------------------------------------------------------------------------

def historical_var(daily_returns: np.ndarray, confidence: float = 0.95) -> float:
    """C#-matching: index = (1-conf)*(N-1), linear interp."""
    sorted_r = np.sort(daily_returns)
    index = (1 - confidence) * (len(sorted_r) - 1)
    lower = int(np.floor(index))
    upper = min(lower + 1, len(sorted_r) - 1)
    fraction = index - lower
    return float(sorted_r[lower] + fraction * (sorted_r[upper] - sorted_r[lower]))


def parametric_var(daily_returns: np.ndarray, confidence: float = 0.95) -> float:
    """mean - z_score * std (sample, ddof=1)."""
    mean = np.mean(daily_returns)
    std = np.std(daily_returns, ddof=1)
    z = scipy.stats.norm.ppf(confidence)
    return float(mean - z * std)


def conditional_var(daily_returns: np.ndarray, confidence: float = 0.95) -> float:
    """Average of returns <= historical VaR."""
    var = historical_var(daily_returns, confidence)
    tail = daily_returns[daily_returns <= var]
    return float(np.mean(tail))


def max_drawdown(daily_returns: np.ndarray) -> float:
    """Max drawdown from equity curve."""
    equity = np.empty(len(daily_returns) + 1)
    equity[0] = 10000.0
    for i, r in enumerate(daily_returns):
        equity[i + 1] = equity[i] * (1 + r)
    peak = np.maximum.accumulate(equity)
    dd = (equity - peak) / peak
    return float(np.min(dd))


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

class TestHistoricalVaR:
    def test_against_vector(self, daily_returns):
        result = historical_var(daily_returns)
        vec = load_vector("var")
        expected = vec["expected"]["historical_var"]
        assert result == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_against_numpy_percentile(self, daily_returns):
        """Cross-check: our interp vs numpy.percentile (should be close)."""
        result = historical_var(daily_returns, 0.95)
        np_result = np.percentile(daily_returns, 5)
        # numpy percentile uses slightly different interp, so looser tolerance
        assert abs(result - np_result) < 1e-4

    def test_all_positive_returns(self):
        """VaR should be positive when all returns are positive."""
        r = np.array([0.01, 0.02, 0.03, 0.04, 0.05])
        var = historical_var(r, 0.95)
        assert var > 0

    def test_confidence_95_vs_99(self, daily_returns):
        """99% VaR should be more extreme (more negative) than 95% VaR."""
        var_95 = historical_var(daily_returns, 0.95)
        var_99 = historical_var(daily_returns, 0.99)
        assert var_99 <= var_95


class TestParametricVaR:
    def test_against_vector(self, daily_returns):
        result = parametric_var(daily_returns)
        vec = load_vector("var")
        expected = vec["expected"]["parametric_var"]
        assert result == pytest.approx(expected, abs=PRECISION_NUMERIC)

    def test_against_scipy(self, daily_returns):
        """Cross-check against scipy.stats.norm."""
        mean = np.mean(daily_returns)
        std = np.std(daily_returns, ddof=1)
        scipy_var = scipy.stats.norm.ppf(0.05, loc=mean, scale=std)
        result = parametric_var(daily_returns, 0.95)
        assert result == pytest.approx(scipy_var, abs=PRECISION_NUMERIC)


class TestConditionalVaR:
    def test_against_vector(self, daily_returns):
        result = conditional_var(daily_returns)
        vec = load_vector("var")
        expected = vec["expected"]["conditional_var"]
        assert result == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_cvar_more_extreme_than_var(self, daily_returns):
        """CVaR should be <= VaR (more negative or equal)."""
        var = historical_var(daily_returns)
        cvar = conditional_var(daily_returns)
        assert cvar <= var + PRECISION_EXACT


class TestMaxDrawdown:
    def test_against_vector(self, daily_returns):
        result = max_drawdown(daily_returns)
        vec = load_vector("derived_ratios")
        expected = vec["expected"]["max_drawdown"]
        assert result == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_always_non_positive(self, daily_returns):
        assert max_drawdown(daily_returns) <= 0.0

    def test_monotonically_increasing(self):
        """No drawdown if equity only goes up."""
        r = np.full(100, 0.01)
        assert max_drawdown(r) == pytest.approx(0.0, abs=PRECISION_EXACT)

    def test_negative_drift_has_drawdown(self, daily_returns_negative):
        dd = max_drawdown(daily_returns_negative)
        assert dd < -0.01  # expect meaningful drawdown
