#!/usr/bin/env python3
"""
Generate Factor Graphical Lasso covariance estimator vectors for Boutquin.Trading.

Reference: Lee & Seregina (2020/2023), "Optimal Portfolio Using Factor Graphical
Lasso", arXiv 2011.00435.

Pipeline (matches the C# FactorGraphicalLassoEstimator exactly):

  1. Sample covariance  Sigma_hat (N-1 divisor, repo convention).
  2. PCA factor model    eigendecompose Sigma_hat; B_hat = U_K * sqrt(Lambda_K)
                         (top-K eigenpairs). Residual covariance W_eps = Sigma_hat - B B'.
  3. Weighted graphical lasso on W_eps with off-diagonal penalty weights
                         W[i,j] = sigma_eps_i * sigma_eps_j; BIC-selected lambda over a
                         10-point log grid in [0.001, 1.0]. Produces residual precision
                         Theta_eps.
  4. Sherman-Morrison-Woodbury reassembly (eq. 3.5):
                         Theta = Theta_eps - Theta_eps B (I_K + B' Theta_eps B)^-1 B' Theta_eps.
  5. Return Sigma = Theta^-1.

This generator is independent of the C# implementation: NumPy/LAPACK supplies PCA,
and scikit-learn solves ordinary graphical lasso on the residual correlation matrix.
Scaling its covariance back by the residual standard deviations is equivalent to the
weighted off-diagonal penalty in eq. 3.3. BIC uses the owned Numerics convention
-T*(logdet(Theta)-tr(S*Theta)) + k*log(T), counting both directed off-diagonal entries.
The full covariance is reconstructed directly as residual covariance + B B', an
independent algebraic route to the C# Sherman-Morrison-Woodbury construction.

Reference implementation:
https://scikit-learn.org/stable/modules/generated/sklearn.covariance.graphical_lasso.html
"""

import json
import warnings
from pathlib import Path

import numpy as np
import sklearn
from sklearn.covariance import graphical_lasso
from sklearn.exceptions import ConvergenceWarning

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

RNG = np.random.default_rng(seed=20110435)  # arXiv id flavor


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


def make_factor_panel(n_assets: int, t_obs: int, k: int) -> np.ndarray:
    """A K-factor panel: shared factor(s) + idiosyncratic noise. Returns N x T."""
    factors = RNG.standard_normal((k, t_obs)) * 0.012
    loadings = RNG.uniform(0.5, 1.5, size=(n_assets, k))
    idio = RNG.standard_normal((n_assets, t_obs)) * 0.006
    returns = loadings @ factors + idio
    # Round to 6 decimals so the JSON input is exactly reproducible double->decimal.
    return np.round(returns, 6)


def independent_reference(returns: np.ndarray, num_factors: int) -> dict:
    """Solve weighted residual glasso by correlation scaling, then add factor risk."""
    n_assets, n_observations = returns.shape
    sample = np.cov(returns, ddof=1)
    eigenvalues, eigenvectors = np.linalg.eigh(sample)
    selected = np.argsort(eigenvalues)[::-1][:num_factors]
    loadings = eigenvectors[:, selected] * np.sqrt(np.maximum(eigenvalues[selected], 0))
    factor_covariance = loadings @ loadings.T
    residual = sample - factor_covariance
    ridge = max(np.maximum(np.diag(residual), 0).mean() * 1e-6, 1e-12)
    for i in range(n_assets):
        residual[i, i] = ridge if residual[i, i] < ridge else residual[i, i] + ridge
    scale = np.sqrt(np.diag(residual))
    outer = np.outer(scale, scale)
    correlation = residual / outer
    candidates = []
    for penalty in np.logspace(-3, 0, 10):
        with warnings.catch_warnings():
            warnings.simplefilter("error", ConvergenceWarning)
            covariance, precision = graphical_lasso(
                correlation, float(penalty), tol=1e-7, enet_tol=1e-12, max_iter=10000
            )
        residual_precision = precision / outer
        fit = np.linalg.slogdet(residual_precision)[1] - np.trace(residual @ residual_precision)
        edges = np.count_nonzero(
            (np.abs(residual_precision) > 1e-10) & ~np.eye(n_assets, dtype=bool)
        )
        total = covariance * outer + factor_covariance
        candidates.append({
            "lambda": float(penalty),
            "bic": float(-n_observations * fit + edges * np.log(n_observations)),
            "legacy_bic_without_sample_size": float(-2 * fit + edges * np.log(n_observations)),
            "covariance": total.tolist(),
        })
    best = min(candidates, key=lambda row: row["bic"])
    legacy = min(candidates, key=lambda row: row["legacy_bic_without_sample_size"])
    # This fixture must distinguish the July 2026 BIC correction from the old package.
    assert best["lambda"] != legacy["lambda"]
    minimum_eigenvalue = float(np.linalg.eigvalsh(best["covariance"]).min())
    assert minimum_eigenvalue > 0
    return {
        "covariance": best["covariance"],
        "is_psd": minimum_eigenvalue > 0,
        "minimum_eigenvalue": minimum_eigenvalue,
        "selected_lambda": best["lambda"],
        "legacy_selected_lambda": legacy["lambda"],
        "reference_versions": {"numpy": np.__version__, "scikit_learn": sklearn.__version__},
    }


def main() -> None:
    print("Generating Factor Graphical Lasso vectors...")

    # 3-asset, T=30, K=1 (the spec's minimal cross-language example).
    returns = make_factor_panel(n_assets=3, t_obs=30, k=1)

    payload = {
        "description": (
            "Factor Graphical Lasso (Lee & Seregina 2020, arXiv 2011.00435): "
            "3-asset T=30 K=1 panel. NumPy PCA and scikit-learn residual graphical "
            "lasso independently compute the covariance, BIC choice and PSD ground truth."
        ),
        "reference": "Lee & Seregina (2020/2023), arXiv 2011.00435",
        "inputs": {
            "returns": returns.tolist(),  # N x T
            "num_factors": 1,
        },
        "expected": independent_reference(returns, num_factors=1),
    }

    save("covariance_factor_graphical_lasso", payload)
    print("Done.")


if __name__ == "__main__":
    main()
