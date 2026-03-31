"""
Verify return calculations against numpy.

Maps to C#: DecimalArrayExtensions.AnnualizedReturn, CompoundAnnualGrowthRate,
            DailyReturns, EquityCurve
"""

import numpy as np
import pytest

from conftest import PRECISION_EXACT, TRADING_DAYS_PER_YEAR, load_vector, save_vector


# ---------------------------------------------------------------------------
# Independent calculations (no library dependency beyond numpy)
# ---------------------------------------------------------------------------

def cumulative_return(daily_returns: np.ndarray) -> float:
    return float(np.prod(1 + daily_returns) - 1)


def annualized_return(daily_returns: np.ndarray, trading_days: int = 252) -> float:
    cum = np.prod(1 + daily_returns)
    return float(cum ** (trading_days / len(daily_returns)) - 1)


def cagr(daily_returns: np.ndarray, trading_days: int = 252) -> float:
    cum = np.prod(1 + daily_returns)
    years = len(daily_returns) / trading_days
    return float(cum ** (1 / years) - 1)


def equity_curve(daily_returns: np.ndarray, initial: float = 10000.0) -> np.ndarray:
    curve = np.empty(len(daily_returns) + 1)
    curve[0] = initial
    for i, r in enumerate(daily_returns):
        curve[i + 1] = curve[i] * (1 + r)
    return curve


def daily_returns_from_equity(eq: np.ndarray) -> np.ndarray:
    return np.diff(eq) / eq[:-1]


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

class TestCumulativeReturn:
    def test_basic(self, daily_returns):
        result = cumulative_return(daily_returns)
        expected = float(np.prod(1 + daily_returns) - 1)
        assert abs(result - expected) < PRECISION_EXACT

    def test_zero_returns(self):
        assert cumulative_return(np.zeros(10)) == pytest.approx(0.0, abs=PRECISION_EXACT)

    def test_single_large_loss(self):
        """A -100% return wipes out everything."""
        r = np.array([0.1, 0.2, -1.0])
        assert cumulative_return(r) == pytest.approx(-1.0, abs=PRECISION_EXACT)


class TestAnnualizedReturn:
    def test_against_vector(self, daily_returns):
        result = annualized_return(daily_returns)
        vec = load_vector("returns")
        expected = vec["expected"]["annualized_return"]
        assert result == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_one_year_equals_cumulative(self):
        """With exactly 252 days, annualized return == cumulative return."""
        r = np.full(252, 0.001)
        ann = annualized_return(r, 252)
        cum = cumulative_return(r)
        assert ann == pytest.approx(cum, abs=PRECISION_EXACT)


class TestCAGR:
    def test_against_vector(self, daily_returns):
        result = cagr(daily_returns)
        vec = load_vector("returns")
        expected = vec["expected"]["cagr"]
        assert result == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_matches_annualized_for_one_year(self):
        """For exactly 1 year of data, CAGR == annualized return."""
        r = np.full(252, 0.0005)
        assert cagr(r) == pytest.approx(annualized_return(r), abs=PRECISION_EXACT)


class TestEquityCurve:
    def test_roundtrip(self, daily_returns):
        """equity_curve → daily_returns_from_equity should recover original."""
        ec = equity_curve(daily_returns)
        recovered = daily_returns_from_equity(ec)
        np.testing.assert_allclose(recovered, daily_returns, atol=PRECISION_EXACT)

    def test_against_vector(self, daily_returns):
        result = equity_curve(daily_returns)
        vec = load_vector("returns")
        expected = np.array(vec["expected"]["equity_curve"])
        np.testing.assert_allclose(result, expected, atol=PRECISION_EXACT)

    def test_initial_value(self, daily_returns):
        ec = equity_curve(daily_returns, initial=5000.0)
        assert ec[0] == 5000.0


class TestDailyReturns:
    def test_against_vector(self, daily_returns):
        ec = equity_curve(daily_returns)
        result = daily_returns_from_equity(ec)
        vec = load_vector("returns")
        expected = np.array(vec["expected"]["daily_returns_from_equity"])
        np.testing.assert_allclose(result, expected, atol=PRECISION_EXACT)
