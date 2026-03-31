"""
Verify ratio calculations against numpy.

Maps to C#: DecimalArrayExtensions.SharpeRatio, AnnualizedSharpeRatio,
            SortinoRatio, AnnualizedSortinoRatio, Beta, Alpha,
            InformationRatio, CalmarRatio, OmegaRatio
"""

import numpy as np
import pytest

from conftest import PRECISION_EXACT, TRADING_DAYS_PER_YEAR, load_vector


# ---------------------------------------------------------------------------
# Independent calculations matching C# conventions exactly
# ---------------------------------------------------------------------------

def sharpe_ratio(daily_returns: np.ndarray, rf: float = 0.0) -> float:
    return float((np.mean(daily_returns) - rf) / np.std(daily_returns, ddof=1))


def annualized_sharpe(daily_returns: np.ndarray, rf: float = 0.0, td: int = 252) -> float:
    return sharpe_ratio(daily_returns, rf) * np.sqrt(td)


def downside_deviation(daily_returns: np.ndarray, rf: float = 0.0) -> float:
    """Full-sample semi-deviation with N-1 divisor."""
    downside = np.minimum(0, daily_returns - rf)
    return float(np.sqrt(np.sum(downside**2) / (len(daily_returns) - 1)))


def sortino_ratio(daily_returns: np.ndarray, rf: float = 0.0) -> float:
    dd = downside_deviation(daily_returns, rf)
    return float((np.mean(daily_returns) - rf) / dd)


def annualized_sortino(daily_returns: np.ndarray, rf: float = 0.0, td: int = 252) -> float:
    return sortino_ratio(daily_returns, rf) * np.sqrt(td)


def beta(portfolio: np.ndarray, benchmark: np.ndarray) -> float:
    cov = np.sum((portfolio - np.mean(portfolio)) * (benchmark - np.mean(benchmark))) / (len(portfolio) - 1)
    var = np.sum((benchmark - np.mean(benchmark))**2) / (len(benchmark) - 1)
    return float(cov / var)


def alpha(portfolio: np.ndarray, benchmark: np.ndarray, rf: float = 0.0) -> float:
    b = beta(portfolio, benchmark)
    return float(np.mean(portfolio) - rf - b * (np.mean(benchmark) - rf))


def information_ratio(portfolio: np.ndarray, benchmark: np.ndarray) -> float:
    active = portfolio - benchmark
    return float(np.mean(active) / np.std(active, ddof=1))


def calmar_ratio(daily_returns: np.ndarray, td: int = 252) -> float:
    cum = np.prod(1 + daily_returns)
    years = len(daily_returns) / td
    cagr_val = cum ** (1 / years) - 1

    equity = np.empty(len(daily_returns) + 1)
    equity[0] = 10000.0
    for i, r in enumerate(daily_returns):
        equity[i + 1] = equity[i] * (1 + r)
    peak = np.maximum.accumulate(equity)
    dd = (equity - peak) / peak
    max_dd = np.min(dd)

    return float(cagr_val / abs(max_dd))


def omega_ratio(daily_returns: np.ndarray, threshold: float = 0.0) -> float:
    gains = np.sum(np.maximum(daily_returns - threshold, 0))
    losses = np.sum(np.maximum(threshold - daily_returns, 0))
    return float(gains / losses)


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

class TestSharpeRatio:
    def test_against_vector(self, daily_returns):
        result = sharpe_ratio(daily_returns)
        vec = load_vector("ratios")
        assert result == pytest.approx(vec["expected"]["sharpe_ratio"], abs=PRECISION_EXACT)

    def test_annualized_against_vector(self, daily_returns):
        result = annualized_sharpe(daily_returns)
        vec = load_vector("ratios")
        assert result == pytest.approx(vec["expected"]["annualized_sharpe_ratio"], abs=PRECISION_EXACT)

    def test_positive_for_positive_mean(self):
        r = np.full(100, 0.001)  # constant positive
        # Zero std dev → C# throws, but Sharpe conceptually infinite
        # We just test the convention is positive mean → positive ratio for varied returns
        r_varied = np.array([0.01, 0.005, 0.015, 0.008, 0.012])
        assert sharpe_ratio(r_varied) > 0


class TestSortinoRatio:
    def test_against_vector(self, daily_returns):
        result = sortino_ratio(daily_returns)
        vec = load_vector("ratios")
        assert result == pytest.approx(vec["expected"]["sortino_ratio"], abs=PRECISION_EXACT)

    def test_annualized_against_vector(self, daily_returns):
        result = annualized_sortino(daily_returns)
        vec = load_vector("ratios")
        assert result == pytest.approx(vec["expected"]["annualized_sortino_ratio"], abs=PRECISION_EXACT)

    def test_sortino_ge_sharpe_for_positive_skew(self):
        """For positively-skewed returns, Sortino >= Sharpe."""
        rng = np.random.default_rng(99)
        r = np.abs(rng.normal(0.001, 0.01, 200))  # all positive → low downside
        # Can't compute Sortino if all positive (dd=0), so add a few losses
        r[0] = -0.005
        r[1] = -0.003
        s = sharpe_ratio(r)
        so = sortino_ratio(r)
        assert so >= s


class TestBeta:
    def test_against_vector(self, daily_returns, benchmark_returns):
        result = beta(daily_returns, benchmark_returns)
        vec = load_vector("ratios")
        assert result == pytest.approx(vec["expected"]["beta"], abs=PRECISION_EXACT)

    def test_self_beta_is_one(self):
        """Beta of a series against itself should be 1.0."""
        r = np.array([0.01, -0.02, 0.03, -0.01, 0.005])
        assert beta(r, r) == pytest.approx(1.0, abs=PRECISION_EXACT)


class TestAlpha:
    def test_against_vector(self, daily_returns, benchmark_returns):
        result = alpha(daily_returns, benchmark_returns)
        vec = load_vector("ratios")
        assert result == pytest.approx(vec["expected"]["alpha"], abs=PRECISION_EXACT)


class TestInformationRatio:
    def test_against_vector(self, daily_returns, benchmark_returns):
        result = information_ratio(daily_returns, benchmark_returns)
        vec = load_vector("ratios")
        assert result == pytest.approx(vec["expected"]["information_ratio"], abs=PRECISION_EXACT)


class TestCalmarRatio:
    def test_against_vector(self, daily_returns):
        result = calmar_ratio(daily_returns)
        vec = load_vector("derived_ratios")
        assert result == pytest.approx(vec["expected"]["calmar_ratio"], abs=PRECISION_EXACT)


class TestOmegaRatio:
    def test_against_vector(self, daily_returns):
        result = omega_ratio(daily_returns)
        vec = load_vector("derived_ratios")
        assert result == pytest.approx(vec["expected"]["omega_ratio"], abs=PRECISION_EXACT)
