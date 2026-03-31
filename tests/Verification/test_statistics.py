"""
Verify statistical calculations against scipy.stats.

Maps to C#: DecimalArrayExtensions.Skewness, Kurtosis, WinRate, ProfitFactor,
            RecoveryFactor, DownsideDeviation
"""

import numpy as np
import scipy.stats
import pytest

from conftest import PRECISION_EXACT, PRECISION_NUMERIC, load_vector


# ---------------------------------------------------------------------------
# Independent calculations matching C# formulas exactly
# ---------------------------------------------------------------------------

def adjusted_skewness(daily_returns: np.ndarray) -> float:
    """Adjusted Fisher-Pearson: n/((n-1)(n-2)) * sum((x-mean)/std)^3."""
    n = len(daily_returns)
    mean = np.mean(daily_returns)
    std = np.std(daily_returns, ddof=1)
    m3 = np.sum(((daily_returns - mean) / std) ** 3)
    return float(n / ((n - 1) * (n - 2)) * m3)


def excess_kurtosis(daily_returns: np.ndarray) -> float:
    """Sample excess kurtosis (Fisher) matching C# formula."""
    n = len(daily_returns)
    mean = np.mean(daily_returns)
    std = np.std(daily_returns, ddof=1)
    m4 = np.sum(((daily_returns - mean) / std) ** 4)
    term1 = (n * (n + 1)) / ((n - 1) * (n - 2) * (n - 3)) * m4
    term2 = 3 * (n - 1) ** 2 / ((n - 2) * (n - 3))
    return float(term1 - term2)


def win_rate(daily_returns: np.ndarray) -> float:
    return float(np.sum(daily_returns > 0) / len(daily_returns))


def profit_factor(daily_returns: np.ndarray) -> float:
    gross_profit = np.sum(daily_returns[daily_returns > 0])
    gross_loss = abs(np.sum(daily_returns[daily_returns < 0]))
    return float(gross_profit / gross_loss)


def recovery_factor(daily_returns: np.ndarray) -> float:
    cum = np.prod(1 + daily_returns) - 1
    equity = np.empty(len(daily_returns) + 1)
    equity[0] = 10000.0
    for i, r in enumerate(daily_returns):
        equity[i + 1] = equity[i] * (1 + r)
    peak = np.maximum.accumulate(equity)
    dd = (equity - peak) / peak
    max_dd = np.min(dd)
    return float(cum / abs(max_dd))


def downside_deviation(daily_returns: np.ndarray, rf: float = 0.0) -> float:
    downside = np.minimum(0, daily_returns - rf)
    return float(np.sqrt(np.sum(downside**2) / (len(daily_returns) - 1)))


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

class TestSkewness:
    def test_against_vector(self, daily_returns):
        result = adjusted_skewness(daily_returns)
        vec = load_vector("statistics")
        assert result == pytest.approx(vec["expected"]["skewness"], abs=PRECISION_EXACT)

    def test_matches_scipy(self, daily_returns):
        """Our manual formula should match scipy.stats.skew(bias=False)."""
        result = adjusted_skewness(daily_returns)
        expected = scipy.stats.skew(daily_returns, bias=False)
        assert result == pytest.approx(float(expected), abs=PRECISION_NUMERIC)

    def test_symmetric_distribution(self):
        """Symmetric data should have near-zero skewness."""
        rng = np.random.default_rng(123)
        # Large sample from symmetric distribution
        data = rng.standard_t(df=10, size=10000)
        skew = adjusted_skewness(data)
        assert abs(skew) < 0.1


class TestKurtosis:
    def test_against_vector(self, daily_returns):
        result = excess_kurtosis(daily_returns)
        vec = load_vector("statistics")
        assert result == pytest.approx(vec["expected"]["kurtosis"], abs=PRECISION_EXACT)

    def test_matches_scipy(self, daily_returns):
        """Our manual formula should match scipy.stats.kurtosis(bias=False, fisher=True)."""
        result = excess_kurtosis(daily_returns)
        expected = scipy.stats.kurtosis(daily_returns, bias=False, fisher=True)
        assert result == pytest.approx(float(expected), abs=PRECISION_NUMERIC)

    def test_normal_distribution_near_zero(self):
        """Normal data should have excess kurtosis near 0."""
        rng = np.random.default_rng(456)
        data = rng.normal(0, 1, 50000)
        kurt = excess_kurtosis(data)
        assert abs(kurt) < 0.1


class TestWinRate:
    def test_against_vector(self, daily_returns):
        result = win_rate(daily_returns)
        vec = load_vector("derived_ratios")
        assert result == pytest.approx(vec["expected"]["win_rate"], abs=PRECISION_EXACT)

    def test_all_positive(self):
        assert win_rate(np.array([0.01, 0.02, 0.03])) == pytest.approx(1.0)

    def test_all_negative(self):
        assert win_rate(np.array([-0.01, -0.02, -0.03])) == pytest.approx(0.0)

    def test_zero_excluded(self):
        """Zero returns are not wins (strictly > 0 in C#)."""
        assert win_rate(np.array([0.0, 0.0, 0.01])) == pytest.approx(1.0 / 3)


class TestProfitFactor:
    def test_against_vector(self, daily_returns):
        result = profit_factor(daily_returns)
        vec = load_vector("derived_ratios")
        assert result == pytest.approx(vec["expected"]["profit_factor"], abs=PRECISION_EXACT)


class TestRecoveryFactor:
    def test_against_vector(self, daily_returns):
        result = recovery_factor(daily_returns)
        vec = load_vector("derived_ratios")
        assert result == pytest.approx(vec["expected"]["recovery_factor"], abs=PRECISION_EXACT)


class TestDownsideDeviation:
    def test_against_vector(self, daily_returns):
        result = downside_deviation(daily_returns)
        vec = load_vector("downside_deviation")
        assert result == pytest.approx(vec["expected"]["downside_deviation"], abs=PRECISION_EXACT)

    def test_all_positive_returns_is_zero(self):
        """No downside when all returns exceed rf."""
        r = np.array([0.01, 0.02, 0.03, 0.04, 0.05])
        assert downside_deviation(r) == pytest.approx(0.0, abs=PRECISION_EXACT)

    def test_with_nonzero_rf(self):
        """Downside deviation increases as rf rises above returns."""
        r = np.array([0.01, 0.005, 0.015, -0.005, 0.008])
        dd_0 = downside_deviation(r, rf=0.0)
        dd_high = downside_deviation(r, rf=0.02)
        assert dd_high > dd_0
