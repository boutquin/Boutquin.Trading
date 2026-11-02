"""Independent NumPy/SciPy reference for López de Prado (2020), §§2.4–2.6.

Fits the MP noise variance against a Gaussian KDE, denoises and rescales,
then removes the market eigenpair and rescales again. Inputs are assets × time.
"""
import numpy as np
from scipy.optimize import differential_evolution


def correlation(matrix):
    scales = np.sqrt(np.diag(matrix))
    return np.clip(matrix / np.outer(scales, scales), -1, 1)


def detoned_reference(returns, alpha=1.0):
    n, t = returns.shape
    sample = np.atleast_2d(np.cov(returns, ddof=1))
    if n < 3:
        return sample
    scales = np.sqrt(np.diag(sample))
    values, vectors = np.linalg.eigh(correlation(sample))
    q = t / n
    upper = (1 + np.sqrt(1 / q)) ** 2
    threshold = upper
    if q != 1:
        grid = np.linspace((1 - np.sqrt(1 / q)) ** 2, upper, 1000)

        def objective(parameter):
            variance = float(parameter[0])
            x = variance * grid
            density = q / (2 * np.pi * variance * x) * np.sqrt(
                np.maximum(0, (x[-1] - x) * (x - x[0])))
            kde = np.exp(-0.5 * ((x[:, None] - values) / .01) ** 2).mean(axis=1) / (.01 * np.sqrt(2 * np.pi))
            return float(np.sum((density - kde) ** 2))

        fit = differential_evolution(objective, [(1e-5, 1 - 1e-5)], seed=731, tol=1e-10, popsize=30)
        candidates = [1e-5, 1 - 1e-5, float(fit.x[0])]
        variance = min(candidates, key=lambda x: objective([x])) if fit.success else 1.0
        threshold = variance * upper
    noise = values <= threshold
    if noise.any():
        values[noise] = values[noise].mean()
    cleaned = correlation(vectors @ np.diag(values) @ vectors.T)
    if alpha and not noise.all():
        values, vectors = np.linalg.eigh(cleaned)
        cleaned = correlation(cleaned - alpha * values[-1] * np.outer(vectors[:, -1], vectors[:, -1]))
    return cleaned * np.outer(scales, scales)
