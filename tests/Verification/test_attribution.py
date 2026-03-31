"""
Verify Brinson-Fachler performance attribution.

Maps to C#: BrinsonFachlerAttributor.Attribute
"""

import numpy as np
import pytest

from conftest import PRECISION_EXACT, load_vector


# ---------------------------------------------------------------------------
# Independent calculation
# ---------------------------------------------------------------------------

def brinson_fachler(
    assets: list[str],
    portfolio_weights: dict[str, float],
    benchmark_weights: dict[str, float],
    portfolio_returns: dict[str, float],
    benchmark_returns: dict[str, float],
) -> dict:
    """Brinson-Fachler single-period attribution."""
    # Total benchmark return
    rb_total = sum(benchmark_weights[a] * benchmark_returns[a] for a in assets)

    allocation = {}
    selection = {}
    interaction = {}

    for a in assets:
        wp = portfolio_weights[a]
        wb = benchmark_weights[a]
        rp = portfolio_returns[a]
        rb = benchmark_returns[a]

        allocation[a] = (wp - wb) * (rb - rb_total)
        selection[a] = wb * (rp - rb)
        interaction[a] = (wp - wb) * (rp - rb)

    total_alloc = sum(allocation.values())
    total_sel = sum(selection.values())
    total_inter = sum(interaction.values())

    return {
        "benchmark_total_return": rb_total,
        "allocation_effects": allocation,
        "selection_effects": selection,
        "interaction_effects": interaction,
        "total_allocation": total_alloc,
        "total_selection": total_sel,
        "total_interaction": total_inter,
        "total_active_return": total_alloc + total_sel + total_inter,
    }


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

class TestBrinsonFachler:
    def test_against_vector(self):
        vec = load_vector("attribution")
        inp = vec["inputs"]
        result = brinson_fachler(
            inp["assets"],
            inp["portfolio_weights"],
            inp["benchmark_weights"],
            inp["portfolio_returns"],
            inp["benchmark_returns"],
        )
        expected = vec["expected"]

        assert result["benchmark_total_return"] == pytest.approx(
            expected["benchmark_total_return"], abs=PRECISION_EXACT
        )
        assert result["total_allocation"] == pytest.approx(
            expected["total_allocation"], abs=PRECISION_EXACT
        )
        assert result["total_selection"] == pytest.approx(
            expected["total_selection"], abs=PRECISION_EXACT
        )
        assert result["total_interaction"] == pytest.approx(
            expected["total_interaction"], abs=PRECISION_EXACT
        )
        assert result["total_active_return"] == pytest.approx(
            expected["total_active_return"], abs=PRECISION_EXACT
        )

        # Per-asset effects
        for asset in inp["assets"]:
            assert result["allocation_effects"][asset] == pytest.approx(
                expected["allocation_effects"][asset], abs=PRECISION_EXACT
            )
            assert result["selection_effects"][asset] == pytest.approx(
                expected["selection_effects"][asset], abs=PRECISION_EXACT
            )
            assert result["interaction_effects"][asset] == pytest.approx(
                expected["interaction_effects"][asset], abs=PRECISION_EXACT
            )

    def test_effects_sum_to_active_return(self):
        """Allocation + Selection + Interaction = Total Active Return."""
        vec = load_vector("attribution")
        expected = vec["expected"]
        summed = (
            expected["total_allocation"]
            + expected["total_selection"]
            + expected["total_interaction"]
        )
        assert summed == pytest.approx(expected["total_active_return"], abs=PRECISION_EXACT)

    def test_zero_active_weight(self):
        """When portfolio == benchmark weights, allocation and interaction are 0."""
        assets = ["A", "B"]
        w = {"A": 0.6, "B": 0.4}
        pr = {"A": 0.05, "B": 0.03}
        br = {"A": 0.04, "B": 0.02}
        result = brinson_fachler(assets, w, w, pr, br)
        assert result["total_allocation"] == pytest.approx(0.0, abs=PRECISION_EXACT)
        assert result["total_interaction"] == pytest.approx(0.0, abs=PRECISION_EXACT)

    def test_zero_active_returns(self):
        """When portfolio returns == benchmark returns, selection and interaction are 0."""
        assets = ["A", "B"]
        pw = {"A": 0.7, "B": 0.3}
        bw = {"A": 0.5, "B": 0.5}
        r = {"A": 0.04, "B": 0.02}
        result = brinson_fachler(assets, pw, bw, r, r)
        assert result["total_selection"] == pytest.approx(0.0, abs=PRECISION_EXACT)
        assert result["total_interaction"] == pytest.approx(0.0, abs=PRECISION_EXACT)

    def test_active_return_matches_direct(self):
        """Active return should equal portfolio return - benchmark return."""
        vec = load_vector("attribution")
        inp = vec["inputs"]
        port_ret = sum(
            inp["portfolio_weights"][a] * inp["portfolio_returns"][a]
            for a in inp["assets"]
        )
        bench_ret = sum(
            inp["benchmark_weights"][a] * inp["benchmark_returns"][a]
            for a in inp["assets"]
        )
        direct_active = port_ret - bench_ret
        result = brinson_fachler(
            inp["assets"],
            inp["portfolio_weights"],
            inp["benchmark_weights"],
            inp["portfolio_returns"],
            inp["benchmark_returns"],
        )
        assert result["total_active_return"] == pytest.approx(
            direct_active, abs=PRECISION_EXACT
        )
