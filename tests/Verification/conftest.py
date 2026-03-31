"""
Shared fixtures and constants for Boutquin.Trading verification tests.

These tests validate C# financial calculations against independent Python
implementations using well-known third-party libraries (numpy, scipy,
statsmodels, scikit-learn, PyPortfolioOpt).

IMPORTANT: All fixtures load data from the generated vector files to ensure
consistency between fixtures and test vectors. Run generate_vectors.py first.
"""

import json
from pathlib import Path

import numpy as np
import pytest

# ---------------------------------------------------------------------------
# Precision tiers — match the tolerance to the calculation family
# ---------------------------------------------------------------------------
# Exact arithmetic (returns, simple ratios)
PRECISION_EXACT = 1e-10
# Iterative / floating-point heavy (optimisers, matrix ops)
PRECISION_NUMERIC = 1e-6
# Statistical sampling (Monte Carlo)
PRECISION_STATISTICAL = 1e-4

TRADING_DAYS_PER_YEAR = 252

VECTORS_DIR = Path(__file__).parent / "vectors"


# ---------------------------------------------------------------------------
# Vector I/O helpers
# ---------------------------------------------------------------------------
def save_vector(name: str, data: dict) -> None:
    """Save a test vector as JSON to the vectors/ directory."""
    VECTORS_DIR.mkdir(exist_ok=True)
    path = VECTORS_DIR / f"{name}.json"

    def convert(obj):
        if isinstance(obj, np.ndarray):
            return obj.tolist()
        if isinstance(obj, (np.float64, np.float32)):
            return float(obj)
        if isinstance(obj, (np.int64, np.int32)):
            return int(obj)
        raise TypeError(f"Cannot serialize {type(obj)}")

    with open(path, "w") as f:
        json.dump(data, f, indent=2, default=convert)


def load_vector(name: str) -> dict:
    """Load a test vector from the vectors/ directory."""
    path = VECTORS_DIR / f"{name}.json"
    with open(path) as f:
        return json.load(f)


# ---------------------------------------------------------------------------
# Fixtures — loaded from generated vector files for consistency
# ---------------------------------------------------------------------------
@pytest.fixture(scope="session")
def daily_returns():
    """252 days of daily returns — loaded from vectors."""
    vec = load_vector("daily_returns")
    return np.array(vec["values"])


@pytest.fixture(scope="session")
def daily_returns_negative():
    """252 days with negative drift — loaded from vectors."""
    vec = load_vector("negative_returns")
    return np.array(vec["values"])


@pytest.fixture(scope="session")
def benchmark_returns():
    """252 days of benchmark returns — loaded from vectors."""
    vec = load_vector("benchmark_returns")
    return np.array(vec["values"])


@pytest.fixture(scope="session")
def equity_curve(daily_returns):
    """Equity curve built from daily_returns, starting at 10000."""
    curve = np.empty(len(daily_returns) + 1)
    curve[0] = 10000.0
    for i, r in enumerate(daily_returns):
        curve[i + 1] = curve[i] * (1 + r)
    return curve


@pytest.fixture(scope="session")
def multi_asset_returns():
    """3-asset return matrix (3 × 252) — loaded from vectors."""
    vec = load_vector("multi_asset_returns")
    return np.array(vec["values"])


@pytest.fixture(scope="session")
def factor_returns():
    """3 factor return series — loaded from vectors."""
    vec = load_vector("factor_returns")
    return {k: np.array(v) for k, v in vec.items()}
