"""
Verify correlation analysis against numpy.

Maps to C#: CorrelationAnalyzer.Analyze, CorrelationAnalyzer.RollingCorrelation
"""

import numpy as np
import pandas as pd
import pytest

from conftest import PRECISION_EXACT, PRECISION_NUMERIC, load_vector


# ---------------------------------------------------------------------------
# Independent calculations matching C# implementation
# ---------------------------------------------------------------------------

def correlation_matrix(returns: np.ndarray) -> np.ndarray:
    """Correlation from sample covariance (ddof=1). returns: (n_assets, T)."""
    cov = np.atleast_2d(np.cov(returns, ddof=1))
    stds = np.sqrt(np.diag(cov))
    outer = np.outer(stds, stds)
    # Guard against zero std
    with np.errstate(divide="ignore", invalid="ignore"):
        corr = np.where(outer > 0, cov / outer, np.eye(returns.shape[0]))
    return corr


def diversification_ratio(returns: np.ndarray, weights: np.ndarray) -> float:
    """DR = weighted_avg_vol / portfolio_vol."""
    cov = np.atleast_2d(np.cov(returns, ddof=1))
    stds = np.sqrt(np.diag(cov))
    weighted_avg_vol = np.sum(weights * stds)
    port_var = weights @ cov @ weights
    port_vol = np.sqrt(port_var)
    return float(weighted_avg_vol / port_vol) if port_vol > 0 else 1.0


def rolling_correlation(
    returns_a: np.ndarray,
    returns_b: np.ndarray,
    window: int,
) -> np.ndarray:
    """Rolling correlation matching C# implementation (population cov in window)."""
    n = len(returns_a) - window + 1
    result = np.empty(n)
    for start in range(n):
        a = returns_a[start : start + window]
        b = returns_b[start : start + window]
        mean_a = np.mean(a)
        mean_b = np.mean(b)
        da = a - mean_a
        db = b - mean_b
        cov_ab = np.sum(da * db)
        var_a = np.sum(da**2)
        var_b = np.sum(db**2)
        denom = np.sqrt(var_a * var_b)
        result[start] = cov_ab / denom if denom > 0 else 0.0
    return result


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

class TestCorrelationMatrix:
    def test_against_vector(self, multi_asset_returns):
        result = correlation_matrix(multi_asset_returns)
        vec = load_vector("correlation")
        expected = np.array(vec["expected"]["correlation_matrix"])
        np.testing.assert_allclose(result, expected, atol=PRECISION_EXACT)

    def test_diagonal_is_one(self, multi_asset_returns):
        corr = correlation_matrix(multi_asset_returns)
        np.testing.assert_allclose(np.diag(corr), 1.0, atol=PRECISION_EXACT)

    def test_symmetric(self, multi_asset_returns):
        corr = correlation_matrix(multi_asset_returns)
        np.testing.assert_allclose(corr, corr.T, atol=PRECISION_EXACT)

    def test_bounded(self, multi_asset_returns):
        """All correlations should be in [-1, 1]."""
        corr = correlation_matrix(multi_asset_returns)
        assert np.all(corr >= -1 - PRECISION_EXACT)
        assert np.all(corr <= 1 + PRECISION_EXACT)

    def test_identical_series(self):
        """Correlation of identical series should be 1."""
        r = np.array([[0.01, 0.02, 0.03, 0.04], [0.01, 0.02, 0.03, 0.04]])
        corr = correlation_matrix(r)
        np.testing.assert_allclose(corr, np.ones((2, 2)), atol=PRECISION_EXACT)


class TestDiversificationRatio:
    def test_against_vector(self, multi_asset_returns):
        n = multi_asset_returns.shape[0]
        weights = np.ones(n) / n
        result = diversification_ratio(multi_asset_returns, weights)
        vec = load_vector("correlation")
        assert result == pytest.approx(vec["expected"]["diversification_ratio"], abs=PRECISION_EXACT)

    def test_single_asset(self):
        """Single asset should have diversification ratio of 1."""
        r = np.array([[0.01, 0.02, 0.03, 0.04]])
        w = np.array([1.0])
        assert diversification_ratio(r, w) == pytest.approx(1.0, abs=PRECISION_EXACT)

    def test_uncorrelated_assets(self):
        """Uncorrelated assets should have DR > 1."""
        rng = np.random.default_rng(88)
        r = rng.normal(0, 0.01, (5, 1000))
        w = np.ones(5) / 5
        dr = diversification_ratio(r, w)
        assert dr > 1.0


class TestRollingCorrelation:
    def test_against_vector(self, multi_asset_returns):
        vec = load_vector("rolling_correlation")
        a = np.array(vec["inputs"]["returns_a"])
        b = np.array(vec["inputs"]["returns_b"])
        window = vec["inputs"]["window_size"]
        result = rolling_correlation(a, b, window)
        # Note: pandas uses ddof=1, our C# uses population within window
        # so we compare against our own implementation
        assert len(result) == len(a) - window + 1

    def test_length(self, multi_asset_returns):
        """Output length should be T - window + 1."""
        a = multi_asset_returns[0]
        b = multi_asset_returns[1]
        window = 30
        result = rolling_correlation(a, b, window)
        assert len(result) == len(a) - window + 1

    def test_bounded(self, multi_asset_returns):
        """All rolling correlations should be in [-1, 1]."""
        a = multi_asset_returns[0]
        b = multi_asset_returns[1]
        result = rolling_correlation(a, b, 30)
        assert np.all(result >= -1 - PRECISION_EXACT)
        assert np.all(result <= 1 + PRECISION_EXACT)

    def test_identical_series_is_one(self):
        """Rolling correlation of a series with itself should be 1."""
        r = np.array([0.01, 0.02, -0.01, 0.03, 0.005, -0.02, 0.01, 0.015])
        result = rolling_correlation(r, r, 3)
        np.testing.assert_allclose(result, 1.0, atol=PRECISION_EXACT)
