"""
Verify OLS factor regression against statsmodels.

Maps to C#: FactorRegressor.Regress
"""

import numpy as np
import statsmodels.api as sm
import pytest

from conftest import PRECISION_EXACT, PRECISION_NUMERIC, load_vector


# ---------------------------------------------------------------------------
# Independent calculation using statsmodels
# ---------------------------------------------------------------------------

def ols_regression(
    portfolio_returns: np.ndarray,
    factor_returns: list[np.ndarray],
) -> dict:
    """OLS regression: R_p = alpha + sum(beta_i * F_i) + epsilon."""
    X = np.column_stack(factor_returns)
    X = sm.add_constant(X)
    model = sm.OLS(portfolio_returns, X).fit()

    return {
        "alpha": float(model.params[0]),
        "betas": [float(b) for b in model.params[1:]],
        "r_squared": float(model.rsquared),
        "residual_std_error": float(np.sqrt(model.mse_resid)),
    }


# Manual OLS via normal equations (mirrors C# Gaussian elimination)
def ols_manual(
    portfolio_returns: np.ndarray,
    factor_returns: list[np.ndarray],
) -> dict:
    """OLS via (X'X)^{-1} X'y using numpy linalg."""
    t = len(portfolio_returns)
    k = len(factor_returns)
    X = np.ones((t, k + 1))
    for j, fr in enumerate(factor_returns):
        X[:, j + 1] = fr

    y = portfolio_returns
    beta = np.linalg.solve(X.T @ X, X.T @ y)

    predicted = X @ beta
    residuals = y - predicted
    ss_res = np.sum(residuals**2)
    ss_tot = np.sum((y - np.mean(y))**2)
    r2 = max(0.0, min(1.0, 1.0 - ss_res / ss_tot)) if ss_tot > 0 else 0.0
    dof = t - (k + 1)
    rse = np.sqrt(ss_res / dof) if dof > 0 else 0.0

    return {
        "alpha": float(beta[0]),
        "betas": [float(b) for b in beta[1:]],
        "r_squared": float(r2),
        "residual_std_error": float(rse),
    }


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

class TestFactorRegression:
    def test_against_vector(self):
        vec = load_vector("regression")
        portfolio = np.array(vec["inputs"]["portfolio_returns"])
        factors = [np.array(f) for f in vec["inputs"]["factor_returns"]]

        result = ols_regression(portfolio, factors)
        expected = vec["expected"]

        assert result["alpha"] == pytest.approx(expected["alpha"], abs=PRECISION_NUMERIC)
        for name, beta_val in expected["betas"].items():
            idx = vec["inputs"]["factor_names"].index(name)
            assert result["betas"][idx] == pytest.approx(beta_val, abs=PRECISION_NUMERIC)
        assert result["r_squared"] == pytest.approx(expected["r_squared"], abs=PRECISION_NUMERIC)
        assert result["residual_std_error"] == pytest.approx(
            expected["residual_std_error"], abs=PRECISION_NUMERIC
        )

    def test_manual_matches_statsmodels(self):
        """Our manual normal equations should match statsmodels."""
        vec = load_vector("regression")
        portfolio = np.array(vec["inputs"]["portfolio_returns"])
        factors = [np.array(f) for f in vec["inputs"]["factor_returns"]]

        sm_result = ols_regression(portfolio, factors)
        manual_result = ols_manual(portfolio, factors)

        assert manual_result["alpha"] == pytest.approx(sm_result["alpha"], abs=PRECISION_NUMERIC)
        for i in range(len(factors)):
            assert manual_result["betas"][i] == pytest.approx(
                sm_result["betas"][i], abs=PRECISION_NUMERIC
            )
        assert manual_result["r_squared"] == pytest.approx(
            sm_result["r_squared"], abs=PRECISION_NUMERIC
        )

    def test_single_factor(self):
        """Single-factor regression should give beta = slope of OLS line."""
        rng = np.random.default_rng(55)
        x = rng.normal(0, 0.01, 100)
        y = 0.001 + 1.5 * x + rng.normal(0, 0.002, 100)
        result = ols_regression(y, [x])
        assert result["betas"][0] == pytest.approx(1.5, abs=0.1)
        assert result["r_squared"] > 0.8

    def test_r_squared_bounds(self):
        """R² should always be in [0, 1]."""
        vec = load_vector("regression")
        portfolio = np.array(vec["inputs"]["portfolio_returns"])
        factors = [np.array(f) for f in vec["inputs"]["factor_returns"]]
        result = ols_regression(portfolio, factors)
        assert 0.0 <= result["r_squared"] <= 1.0
