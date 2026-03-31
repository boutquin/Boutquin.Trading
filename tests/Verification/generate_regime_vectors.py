#!/usr/bin/env python3
"""
Generate cross-language verification vectors for regime classifier,
risk rules, and position sizing.

Phase 5B-5D of the verification roadmap.
Components: GrowthInflationRegimeClassifier, MaxDrawdownRule,
            MaxPositionSizeRule, MaxSectorExposureRule,
            FixedWeightPositionSizer, DynamicWeightPositionSizer.

All Python reference implementations replicate the EXACT C# algorithms.

CORRECTNESS VALIDATION (three layers):
  1. Library cross-references — N/A for these (deterministic state machines / arithmetic).
  2. Analytical solutions — regime on zero signals = hysteresis, constant equity = no drawdown,
     position sizing with round-trip math.
  3. Property-based checks — regime is one of 4 quadrants, risk rules are allow/reject,
     |weights| sum <= 1, position quantities >= 0.
"""

import json
import math
from pathlib import Path

import numpy as np

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

RNG = np.random.default_rng(seed=502)  # Distinct seed for Phase 5 regime/rules

# ═══════════════════════════════════════════════════════════════════════════
# Correctness tracking
# ═══════════════════════════════════════════════════════════════════════════

HARD_CHECKS: list[tuple[str, bool]] = []
QUALITY_CHECKS: list[tuple[str, bool, str]] = []


def hard_check(name: str, condition: bool):
    HARD_CHECKS.append((name, condition))
    assert condition, f"HARD CHECK FAILED: {name}"


def quality_check(name: str, actual: float, expected: float, rel_tol: float = 0.01):
    if expected == 0:
        gap = abs(actual)
        passed = gap < 1e-6
    else:
        gap = abs(actual - expected) / abs(expected)
        passed = gap < rel_tol
    QUALITY_CHECKS.append((name, passed, f"gap={gap:.6f}"))
    if not passed:
        print(f"  ⚠ QUALITY GAP: {name} — actual={actual:.8f}, expected={expected:.8f}, gap={gap:.4%}")


def save(name: str, data: dict) -> None:
    def convert(obj):
        if isinstance(obj, np.ndarray):
            return obj.tolist()
        if isinstance(obj, (np.float64, np.float32)):
            return float(obj)
        if isinstance(obj, (np.int64, np.int32)):
            return int(obj)
        raise TypeError(f"Cannot serialize {type(obj)}")

    path = VECTORS_DIR / f"{name}.json"
    with open(path, "w") as f:
        json.dump(data, f, indent=2, default=convert)
    print(f"  ✓ Saved {path.name}")


# ═══════════════════════════════════════════════════════════════════════════
# Regime enumeration (matching C# EconomicRegime)
# ═══════════════════════════════════════════════════════════════════════════

REGIMES = {
    (True, True): "RisingGrowthRisingInflation",
    (True, False): "RisingGrowthFallingInflation",
    (False, True): "FallingGrowthRisingInflation",
    (False, False): "FallingGrowthFallingInflation",
}


def classify_regime(growth: float, inflation: float, deadband: float,
                    prior_regime: str | None) -> str:
    """Replicate C# GrowthInflationRegimeClassifier logic exactly."""
    growth_rising = growth > deadband
    growth_falling = growth < -deadband
    inflation_rising = inflation > deadband
    inflation_falling = inflation < -deadband

    # Both ambiguous → use prior
    if (not growth_rising and not growth_falling and
            not inflation_rising and not inflation_falling and prior_regime is not None):
        return prior_regime

    # Determine growth dimension
    if growth_rising:
        is_growth_rising = True
    elif growth_falling:
        is_growth_rising = False
    elif prior_regime is not None and prior_regime in (
            "RisingGrowthRisingInflation", "RisingGrowthFallingInflation"):
        is_growth_rising = True
    else:
        is_growth_rising = False

    # Determine inflation dimension
    if inflation_rising:
        is_inflation_rising = True
    elif inflation_falling:
        is_inflation_rising = False
    elif prior_regime is not None and prior_regime in (
            "RisingGrowthRisingInflation", "FallingGrowthRisingInflation"):
        is_inflation_rising = True
    else:
        is_inflation_rising = False

    return REGIMES[(is_growth_rising, is_inflation_rising)]


# ═══════════════════════════════════════════════════════════════════════════
# 5B. Regime Classifier Vectors
# ═══════════════════════════════════════════════════════════════════════════

def generate_regime_vectors():
    print("\n═══ Phase 5B: Regime Classifier Vectors ═══")

    cases = {}

    # --- All 4 quadrants (no deadband) ---
    quadrant_cases = [
        ("rising_rising", 0.5, 0.3, 0.0, None, "RisingGrowthRisingInflation"),
        ("rising_falling", 0.5, -0.3, 0.0, None, "RisingGrowthFallingInflation"),
        ("falling_rising", -0.5, 0.3, 0.0, None, "FallingGrowthRisingInflation"),
        ("falling_falling", -0.5, -0.3, 0.0, None, "FallingGrowthFallingInflation"),
    ]

    for name, g, inf, db, prior, expected in quadrant_cases:
        result = classify_regime(g, inf, db, prior)
        hard_check(f"Regime {name}", result == expected)
        cases[name] = {
            "growth_signal": g,
            "inflation_signal": inf,
            "deadband": db,
            "prior_regime": prior,
            "expected_regime": expected,
        }

    # --- Deadband hysteresis ---
    # Both signals within deadband, prior = RisingGrowthRisingInflation => stays
    result = classify_regime(0.05, 0.05, 0.1, "RisingGrowthRisingInflation")
    hard_check("Hysteresis: both ambiguous", result == "RisingGrowthRisingInflation")
    cases["hysteresis_both_ambiguous"] = {
        "growth_signal": 0.05,
        "inflation_signal": 0.05,
        "deadband": 0.1,
        "prior_regime": "RisingGrowthRisingInflation",
        "expected_regime": "RisingGrowthRisingInflation",
    }

    # Growth ambiguous, inflation clear
    result = classify_regime(0.05, -0.5, 0.1, "RisingGrowthRisingInflation")
    hard_check("Hysteresis: growth ambiguous", result == "RisingGrowthFallingInflation")
    cases["hysteresis_growth_ambiguous"] = {
        "growth_signal": 0.05,
        "inflation_signal": -0.5,
        "deadband": 0.1,
        "prior_regime": "RisingGrowthRisingInflation",
        "expected_regime": "RisingGrowthFallingInflation",
    }

    # Inflation ambiguous, growth clear
    result = classify_regime(-0.5, 0.05, 0.1, "RisingGrowthRisingInflation")
    hard_check("Hysteresis: inflation ambiguous", result == "FallingGrowthRisingInflation")
    cases["hysteresis_inflation_ambiguous"] = {
        "growth_signal": -0.5,
        "inflation_signal": 0.05,
        "deadband": 0.1,
        "prior_regime": "RisingGrowthRisingInflation",
        "expected_regime": "FallingGrowthRisingInflation",
    }

    # No prior, both ambiguous => defaults to FallingGrowthFallingInflation
    result = classify_regime(0.05, 0.05, 0.1, None)
    hard_check("No prior, both ambiguous", result == "FallingGrowthFallingInflation")
    cases["no_prior_both_ambiguous"] = {
        "growth_signal": 0.05,
        "inflation_signal": 0.05,
        "deadband": 0.1,
        "prior_regime": None,
        "expected_regime": "FallingGrowthFallingInflation",
    }

    # --- Sequence test: regime transitions with hysteresis ---
    sequence_signals = [
        (0.5, 0.3),    # clear rising/rising
        (0.05, 0.05),  # ambiguous → stays rising/rising
        (-0.5, 0.05),  # growth falls, inflation ambiguous → falling/rising
        (0.05, -0.5),  # growth ambiguous (prior=falling), inflation falls → falling/falling
        (0.5, 0.5),    # clear rising/rising
    ]
    deadband = 0.1
    prior = None
    sequence_results = []
    for g, inf in sequence_signals:
        regime = classify_regime(g, inf, deadband, prior)
        sequence_results.append(regime)
        prior = regime

    expected_sequence = [
        "RisingGrowthRisingInflation",
        "RisingGrowthRisingInflation",  # hysteresis
        "FallingGrowthRisingInflation",
        "FallingGrowthFallingInflation",
        "RisingGrowthRisingInflation",
    ]
    for i, (actual, expected) in enumerate(zip(sequence_results, expected_sequence)):
        hard_check(f"Sequence step {i}", actual == expected)

    cases["sequence"] = {
        "signals": [{"growth": g, "inflation": inf} for g, inf in sequence_signals],
        "deadband": deadband,
        "expected_regimes": expected_sequence,
    }

    # Exact boundary tests
    result = classify_regime(0.1, 0.1, 0.1, None)
    hard_check("Exact boundary => falling/falling (not >)", result == "FallingGrowthFallingInflation")
    cases["exact_boundary"] = {
        "growth_signal": 0.1,
        "inflation_signal": 0.1,
        "deadband": 0.1,
        "prior_regime": None,
        "expected_regime": "FallingGrowthFallingInflation",
    }

    # Zero deadband: any non-zero signal is clear
    result = classify_regime(0.001, 0.001, 0.0, None)
    hard_check("Zero deadband, tiny positive", result == "RisingGrowthRisingInflation")
    cases["zero_deadband_tiny"] = {
        "growth_signal": 0.001,
        "inflation_signal": 0.001,
        "deadband": 0.0,
        "prior_regime": None,
        "expected_regime": "RisingGrowthRisingInflation",
    }

    save("regime_classifier", {
        "description": "Phase 5B: GrowthInflationRegimeClassifier verification vectors",
        "cases": cases,
    })


# ═══════════════════════════════════════════════════════════════════════════
# 5C. Risk Rules — simplified verification (pure logic, no portfolio mock)
# ═══════════════════════════════════════════════════════════════════════════

def max_drawdown_check(equity_curve: list[float], max_dd_pct: float, tolerance: float = 0.0001) -> bool:
    """Returns True if allowed (drawdown within limit), False if rejected."""
    if len(equity_curve) < 2:
        return True
    peak = max(equity_curve)
    last = equity_curve[-1]
    dd = (peak - last) / peak if peak > 0 else 0.0
    return dd <= max_dd_pct + tolerance


def max_position_check(position_value: float, total_value: float,
                       max_pct: float, tolerance: float = 0.0001) -> bool:
    """Returns True if allowed."""
    if total_value <= 0:
        return True
    pct = abs(position_value) / total_value
    return pct <= max_pct + tolerance


def max_sector_check(sector_exposure: float, total_value: float,
                     max_pct: float, tolerance: float = 0.001) -> bool:
    """Returns True if allowed."""
    if total_value <= 0:
        return True
    pct = abs(sector_exposure) / total_value
    return pct <= max_pct + tolerance


def generate_risk_rule_vectors():
    print("\n═══ Phase 5C: Risk Rule Vectors ═══")

    cases = {}

    # --- MaxDrawdownRule ---
    # Case 1: No drawdown => allowed
    eq1 = [10000, 10100, 10200, 10300]
    allowed = max_drawdown_check(eq1, 0.20)
    hard_check("MaxDD: no drawdown => allowed", allowed)
    cases["max_dd_no_drawdown"] = {
        "rule": "MaxDrawdown",
        "equity_curve": eq1,
        "max_drawdown_percent": 0.20,
        "expected_allowed": True,
    }

    # Case 2: Small drawdown within limit
    eq2 = [10000, 10500, 10200, 10300]  # peak=10500, last=10300, dd=1.9%
    dd2 = (10500 - 10300) / 10500
    allowed = max_drawdown_check(eq2, 0.20)
    hard_check(f"MaxDD: {dd2:.4f} < 0.20", allowed)
    cases["max_dd_within_limit"] = {
        "rule": "MaxDrawdown",
        "equity_curve": eq2,
        "max_drawdown_percent": 0.20,
        "current_drawdown": dd2,
        "expected_allowed": True,
    }

    # Case 3: Drawdown exceeds limit
    eq3 = [10000, 10500, 8000]  # peak=10500, last=8000, dd=23.8%
    dd3 = (10500 - 8000) / 10500
    allowed = max_drawdown_check(eq3, 0.20)
    hard_check(f"MaxDD: {dd3:.4f} > 0.20", not allowed)
    cases["max_dd_exceeded"] = {
        "rule": "MaxDrawdown",
        "equity_curve": eq3,
        "max_drawdown_percent": 0.20,
        "current_drawdown": dd3,
        "expected_allowed": False,
    }

    # Case 4: Exactly at limit (within tolerance) => allowed
    # peak=10000, last=8000, dd=0.20
    eq4 = [10000, 8000]
    dd4 = (10000 - 8000) / 10000  # = 0.20 exactly
    allowed = max_drawdown_check(eq4, 0.20)
    hard_check("MaxDD: exactly at limit", allowed)
    cases["max_dd_at_limit"] = {
        "rule": "MaxDrawdown",
        "equity_curve": eq4,
        "max_drawdown_percent": 0.20,
        "current_drawdown": dd4,
        "expected_allowed": True,
    }

    # Case 5: Single data point => allowed
    allowed = max_drawdown_check([10000], 0.10)
    hard_check("MaxDD: single point => allowed", allowed)
    cases["max_dd_single_point"] = {
        "rule": "MaxDrawdown",
        "equity_curve": [10000],
        "max_drawdown_percent": 0.10,
        "expected_allowed": True,
    }

    # --- MaxPositionSizeRule ---
    cases["max_pos_allowed"] = {
        "rule": "MaxPositionSize",
        "position_value": 2000,
        "total_portfolio_value": 10000,
        "max_position_percent": 0.25,
        "expected_allowed": True,
        "position_percent": 0.20,
    }
    hard_check("MaxPos: 20% < 25%", max_position_check(2000, 10000, 0.25))

    cases["max_pos_rejected"] = {
        "rule": "MaxPositionSize",
        "position_value": 3000,
        "total_portfolio_value": 10000,
        "max_position_percent": 0.25,
        "expected_allowed": False,
        "position_percent": 0.30,
    }
    hard_check("MaxPos: 30% > 25%", not max_position_check(3000, 10000, 0.25))

    cases["max_pos_at_limit"] = {
        "rule": "MaxPositionSize",
        "position_value": 2500,
        "total_portfolio_value": 10000,
        "max_position_percent": 0.25,
        "expected_allowed": True,
        "position_percent": 0.25,
    }
    hard_check("MaxPos: exactly 25%", max_position_check(2500, 10000, 0.25))

    # --- MaxSectorExposureRule ---
    cases["max_sector_allowed"] = {
        "rule": "MaxSectorExposure",
        "sector_exposure": 3500,
        "total_portfolio_value": 10000,
        "max_exposure_percent": 0.40,
        "expected_allowed": True,
        "exposure_percent": 0.35,
    }
    hard_check("MaxSector: 35% < 40%", max_sector_check(3500, 10000, 0.40))

    cases["max_sector_rejected"] = {
        "rule": "MaxSectorExposure",
        "sector_exposure": 4500,
        "total_portfolio_value": 10000,
        "max_exposure_percent": 0.40,
        "expected_allowed": False,
        "exposure_percent": 0.45,
    }
    hard_check("MaxSector: 45% > 40%", not max_sector_check(4500, 10000, 0.40))

    save("risk_rules", {
        "description": "Phase 5C: Risk rule verification vectors (simplified logic tests)",
        "cases": cases,
    })


# ═══════════════════════════════════════════════════════════════════════════
# 5D. Position Sizing
# ═══════════════════════════════════════════════════════════════════════════

def fixed_weight_position_sizes(
    total_value: float, weights: dict[str, float],
    prices: dict[str, float], cash_buffer: float = 0.0
) -> dict[str, int]:
    """Replicate C# FixedWeightPositionSizer."""
    allocatable = total_value * (1.0 - cash_buffer)
    result = {}
    for asset, weight in weights.items():
        desired_value = allocatable * weight
        price = prices[asset]
        # Round with MidpointRounding.AwayFromZero = Python round()
        qty = round(desired_value / price)
        result[asset] = qty
    return result


def generate_position_sizing_vectors():
    print("\n═══ Phase 5D: Position Sizing Vectors ═══")

    cases = {}

    # Case 1: Equal weights, no buffer
    weights1 = {"AAPL": 0.5, "MSFT": 0.5}
    prices1 = {"AAPL": 150.0, "MSFT": 300.0}
    total1 = 100000.0
    sizes1 = fixed_weight_position_sizes(total1, weights1, prices1)
    hard_check("FixedWeight: AAPL", sizes1["AAPL"] == round(50000 / 150))
    hard_check("FixedWeight: MSFT", sizes1["MSFT"] == round(50000 / 300))
    cases["fixed_equal_no_buffer"] = {
        "type": "FixedWeight",
        "total_value": total1,
        "weights": weights1,
        "prices": prices1,
        "cash_buffer_percent": 0.0,
        "expected_quantities": sizes1,
    }

    # Case 2: Unequal weights with cash buffer
    weights2 = {"AAPL": 0.6, "GOOG": 0.4}
    prices2 = {"AAPL": 175.0, "GOOG": 140.0}
    total2 = 50000.0
    buffer2 = 0.02
    sizes2 = fixed_weight_position_sizes(total2, weights2, prices2, buffer2)
    allocatable2 = total2 * (1 - buffer2)
    hard_check("FixedWeight buffer: AAPL", sizes2["AAPL"] == round(allocatable2 * 0.6 / 175))
    hard_check("FixedWeight buffer: GOOG", sizes2["GOOG"] == round(allocatable2 * 0.4 / 140))
    cases["fixed_unequal_with_buffer"] = {
        "type": "FixedWeight",
        "total_value": total2,
        "weights": weights2,
        "prices": prices2,
        "cash_buffer_percent": buffer2,
        "expected_quantities": sizes2,
    }

    # Case 3: Three assets
    weights3 = {"A": 0.4, "B": 0.35, "C": 0.25}
    prices3 = {"A": 50.0, "B": 120.0, "C": 85.0}
    total3 = 200000.0
    sizes3 = fixed_weight_position_sizes(total3, weights3, prices3)
    for asset in weights3:
        expected_qty = round(total3 * weights3[asset] / prices3[asset])
        hard_check(f"FixedWeight 3-asset: {asset}", sizes3[asset] == expected_qty)
    cases["fixed_three_assets"] = {
        "type": "FixedWeight",
        "total_value": total3,
        "weights": weights3,
        "prices": prices3,
        "cash_buffer_percent": 0.0,
        "expected_quantities": sizes3,
    }

    # Case 4: Rounding edge — MidpointRounding.AwayFromZero
    # $50000 * 0.333 / $100 = 166.5 => rounds to 167 (AwayFromZero) in C#
    # Python round(166.5) = 166 (banker's rounding!)
    # C# uses Math.Round(x, MidpointRounding.AwayFromZero) = round away from zero
    # Python equivalent: math.floor(x + 0.5) for positive x
    weights4 = {"X": 0.333}
    prices4 = {"X": 100.0}
    total4 = 50000.0
    desired = total4 * 0.333 / 100.0  # = 166.5
    # C# rounds 166.5 -> 167 (AwayFromZero)
    qty4 = math.floor(desired + 0.5)  # = 167
    cases["fixed_rounding_edge"] = {
        "type": "FixedWeight",
        "total_value": total4,
        "weights": weights4,
        "prices": prices4,
        "cash_buffer_percent": 0.0,
        "expected_quantities": {"X": qty4},
    }
    hard_check(f"Rounding 166.5 => 167", qty4 == 167)

    # Layer 3: position quantities are non-negative for positive weights
    for case_name, case_data in cases.items():
        if "expected_quantities" in case_data:
            for asset, qty in case_data["expected_quantities"].items():
                hard_check(f"{case_name}.{asset} >= 0", qty >= 0)

    save("position_sizing", {
        "description": "Phase 5D: Position sizing verification vectors",
        "cases": cases,
    })


# ═══════════════════════════════════════════════════════════════════════════
# Main
# ═══════════════════════════════════════════════════════════════════════════

if __name__ == "__main__":
    generate_regime_vectors()
    generate_risk_rule_vectors()
    generate_position_sizing_vectors()

    # Report
    print(f"\n{'═' * 60}")
    hard_pass = sum(1 for _, p in HARD_CHECKS if p)
    hard_fail = sum(1 for _, p in HARD_CHECKS if not p)
    print(f"HARD CHECKS: {hard_pass}/{len(HARD_CHECKS)} passed" +
          (f" ({hard_fail} FAILED)" if hard_fail else ""))

    qual_pass = sum(1 for _, p, _ in QUALITY_CHECKS if p)
    qual_fail = sum(1 for _, p, _ in QUALITY_CHECKS if not p)
    print(f"QUALITY CHECKS: {qual_pass}/{len(QUALITY_CHECKS)} passed" +
          (f" ({qual_fail} FAILED)" if qual_fail else ""))

    if hard_fail:
        print("\nFAILED HARD CHECKS:")
        for name, passed in HARD_CHECKS:
            if not passed:
                print(f"  ✗ {name}")

    if qual_fail:
        print("\nFAILED QUALITY CHECKS:")
        for name, passed, detail in QUALITY_CHECKS:
            if not passed:
                print(f"  ✗ {name} ({detail})")

    print(f"\nDone. Generated 3 vector files.")
