"""
Verify covariance estimators against numpy / scikit-learn.

Maps to C#: SampleCovarianceEstimator, ExponentiallyWeightedCovarianceEstimator,
            LedoitWolfShrinkageEstimator
"""

import numpy as np
import pytest
from sklearn.covariance import LedoitWolf

from conftest import PRECISION_EXACT, PRECISION_NUMERIC, load_vector


# ---------------------------------------------------------------------------
# Independent calculations matching C# implementations
# ---------------------------------------------------------------------------

def sample_covariance(returns: np.ndarray) -> np.ndarray:
    """Sample covariance matrix (N-1 divisor). returns: shape (n_assets, T)."""
    return np.cov(returns, ddof=1)


def ewma_covariance(returns: np.ndarray, lam: float = 0.94) -> np.ndarray:
    """EWMA covariance matrix matching C# implementation.
    Weights: w[k] = lambda^(T-1-k), normalized. Uses demeaned returns."""
    n_assets, t = returns.shape
    means = np.mean(returns, axis=1)
    demeaned = returns - means[:, np.newaxis]

    weights = np.array([lam ** (t - 1 - k) for k in range(t)])
    weights /= weights.sum()

    cov = np.zeros((n_assets, n_assets))
    for i in range(n_assets):
        for j in range(i, n_assets):
            val = np.sum(weights * demeaned[i] * demeaned[j])
            cov[i, j] = val
            cov[j, i] = val
    return cov


def ledoit_wolf_covariance(returns: np.ndarray) -> tuple[np.ndarray, float]:
    """Ledoit-Wolf via scikit-learn. returns: shape (n_assets, T)."""
    lw = LedoitWolf().fit(returns.T)  # sklearn wants (T, n_assets)
    return lw.covariance_, lw.shrinkage_


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

class TestSampleCovariance:
    def test_against_vector(self, multi_asset_returns):
        result = sample_covariance(multi_asset_returns)
        vec = load_vector("covariance")
        expected = np.array(vec["expected"]["sample_covariance"])
        np.testing.assert_allclose(result, expected, atol=PRECISION_EXACT)

    def test_against_numpy(self, multi_asset_returns):
        """Directly verify against np.cov."""
        result = sample_covariance(multi_asset_returns)
        expected = np.cov(multi_asset_returns, ddof=1)
        np.testing.assert_allclose(result, expected, atol=PRECISION_EXACT)

    def test_symmetric(self, multi_asset_returns):
        cov = sample_covariance(multi_asset_returns)
        np.testing.assert_allclose(cov, cov.T, atol=PRECISION_EXACT)

    def test_diagonal_positive(self, multi_asset_returns):
        cov = sample_covariance(multi_asset_returns)
        assert np.all(np.diag(cov) > 0)

    def test_two_assets(self):
        """2-asset case should produce 2x2 matrix."""
        r = np.array([[0.01, 0.02, 0.03], [0.005, 0.015, 0.025]])
        cov = sample_covariance(r)
        assert cov.shape == (2, 2)
        assert cov[0, 1] == cov[1, 0]


class TestEWMACovariance:
    def test_against_vector(self, multi_asset_returns):
        result = ewma_covariance(multi_asset_returns, lam=0.94)
        vec = load_vector("covariance")
        expected = np.array(vec["expected"]["ewma_covariance"])
        np.testing.assert_allclose(result, expected, atol=PRECISION_EXACT)

    def test_symmetric(self, multi_asset_returns):
        cov = ewma_covariance(multi_asset_returns)
        np.testing.assert_allclose(cov, cov.T, atol=PRECISION_EXACT)

    def test_lambda_1_approaches_equal_weight(self, multi_asset_returns):
        """As lambda → 1, EWMA approaches equal-weighted (but not identical due to normalization)."""
        ewma_99 = ewma_covariance(multi_asset_returns, lam=0.9999)
        sample = sample_covariance(multi_asset_returns)
        # Should be in the same ballpark
        np.testing.assert_allclose(ewma_99, sample, rtol=0.1)


class TestLedoitWolfCovariance:
    def test_against_vector(self, multi_asset_returns):
        result, shrinkage = ledoit_wolf_covariance(multi_asset_returns)
        vec = load_vector("covariance")
        expected = np.array(vec["expected"]["ledoit_wolf_covariance"])
        # sklearn's LW and C# implementation may differ slightly due to
        # different shrinkage formula details (OAS vs standard LW)
        np.testing.assert_allclose(result, expected, atol=PRECISION_NUMERIC)

    def test_shrinkage_between_0_and_1(self, multi_asset_returns):
        _, shrinkage = ledoit_wolf_covariance(multi_asset_returns)
        assert 0.0 <= shrinkage <= 1.0

    def test_symmetric(self, multi_asset_returns):
        cov, _ = ledoit_wolf_covariance(multi_asset_returns)
        np.testing.assert_allclose(cov, cov.T, atol=PRECISION_EXACT)

    def test_between_sample_and_identity(self, multi_asset_returns):
        """LW covariance should be between sample cov and scaled identity."""
        cov_lw, shrinkage = ledoit_wolf_covariance(multi_asset_returns)
        cov_sample = sample_covariance(multi_asset_returns)
        mu = np.mean(np.diag(cov_sample))
        target = mu * np.eye(multi_asset_returns.shape[0])

        # LW = shrinkage * target + (1 - shrinkage) * sample (approximately)
        expected_mix = shrinkage * target + (1 - shrinkage) * cov_sample
        np.testing.assert_allclose(cov_lw, expected_mix, atol=PRECISION_NUMERIC)
