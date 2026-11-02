"""Regeneration must retain the independently qualified Numerics 2.0 contracts."""
import json
from pathlib import Path

import numpy as np
import pytest

from detoning_reference import detoned_reference
from generate_covariance_edge_vectors import ledoit_wolf_cov

VECTORS = Path(__file__).parent / "vectors"


@pytest.mark.parametrize("name", ["covariance_edge_correlated", "covariance_ledoit_wolf_own"])
def test_identity_target_regenerates_qualified_matrix(name):
    data = json.loads((VECTORS / f"{name}.json").read_text())
    covariance, intensity = ledoit_wolf_cov(np.array(data["inputs"]["returns"]))
    np.testing.assert_allclose(covariance, data["expected"]["ledoit_wolf_covariance"], rtol=0, atol=1e-15)
    assert abs(intensity - data["expected"]["ledoit_wolf_shrinkage"]) < 1e-14


@pytest.mark.parametrize("key,alpha", [("detoned_alpha_1", 1.0), ("detoned_alpha_05", .5)])
def test_detoning_regenerates_qualified_matrix(key, alpha):
    case = json.loads((VECTORS / "covariance_detoned.json").read_text())["cases"]["three_asset"]
    np.testing.assert_allclose(detoned_reference(np.array(case["returns"]), alpha), case[key], rtol=0, atol=1e-15)
