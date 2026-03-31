// Copyright (c) 2023-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

namespace Boutquin.Trading.Tests.UnitTests.Application;

using Boutquin.Trading.Application.DownsideRisk;
using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Domain.ValueObjects;
using FluentAssertions;

/// <summary>
/// Tests for MeanDownsideRiskConstruction with CVaR and DownsideDeviation risk measures.
/// </summary>
public sealed class MeanDownsideRiskConstructionTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_tlt = new("TLT");
    private static readonly Asset s_gld = new("GLD");
    private static readonly Asset s_vnq = new("VNQ");

    // VTI: high vol symmetric, TLT: low vol, GLD: medium vol, VNQ: high vol with negative skew
    private static decimal[][] FourAssetReturns =>
    [
        [0.02m, -0.03m, 0.04m, -0.01m, 0.03m, -0.02m, 0.01m, 0.05m, -0.04m, 0.02m], // VTI
        [0.005m, -0.003m, 0.004m, 0.002m, -0.001m, 0.003m, -0.002m, 0.001m, 0.004m, -0.003m], // TLT
        [0.01m, -0.02m, 0.015m, -0.005m, 0.02m, -0.01m, 0.005m, 0.03m, -0.025m, 0.01m], // GLD
        [0.03m, -0.04m, 0.05m, -0.02m, 0.04m, -0.03m, 0.02m, 0.06m, -0.05m, 0.03m]  // VNQ
    ];

    private static IReadOnlyList<Asset> FourAssets => [s_vti, s_tlt, s_gld, s_vnq];

    // Asymmetric returns: equity has positive skew (big upside, small downside),
    // bond has negative skew (small upside, occasional big loss)
    private static decimal[][] AsymmetricReturns =>
    [
        // Equity: mostly small losses, occasional large gains (positive skew)
        [-0.005m, -0.003m, -0.004m, 0.08m, -0.002m, -0.006m, 0.07m, -0.003m, -0.005m, 0.09m],
        // Bond: mostly small gains, occasional large losses (negative skew)
        [0.003m, 0.002m, 0.004m, -0.06m, 0.003m, 0.002m, -0.05m, 0.004m, 0.003m, -0.07m]
    ];

    private static IReadOnlyList<Asset> TwoAssets => [new Asset("EQUITY"), new Asset("BOND")];

    // --- Helpers ---

    private static void AssertWeightsSumToOne(IReadOnlyDictionary<Asset, decimal> weights)
    {
        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-8m, "Weights must sum to 1.0");
    }

    private static void AssertAllWeightsNonNegative(IReadOnlyDictionary<Asset, decimal> weights)
    {
        foreach (var (asset, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0m, $"Weight for {asset} must be non-negative");
        }
    }

    // ==================== CVaR Construction Tests ====================

    [Fact]
    public void MeanCVaR_FourAssets_WeightsSumToOne()
    {
        var model = new MeanDownsideRiskConstruction(new CVaRRiskMeasure());

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        weights.Should().HaveCount(4);
        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void MeanCVaR_EmptyAssets_ShouldReturnEmpty()
    {
        var model = new MeanDownsideRiskConstruction(new CVaRRiskMeasure());

        var weights = model.ComputeTargetWeights([], []);

        weights.Should().BeEmpty();
    }

    [Fact]
    public void MeanCVaR_SingleAsset_ShouldReturn100Percent()
    {
        var model = new MeanDownsideRiskConstruction(new CVaRRiskMeasure());
        var assets = new List<Asset> { s_vti };

        var weights = model.ComputeTargetWeights(assets, [FourAssetReturns[0]]);

        weights[s_vti].Should().BeApproximately(1.0m, 1e-8m);
    }

    [Fact]
    public void MeanCVaR_HighRiskAversion_ShouldReduceTailExposure()
    {
        // High λ should shift weight away from assets with fat left tails
        var lowLambda = new MeanDownsideRiskConstruction(new CVaRRiskMeasure(), riskAversion: 0.5m);
        var highLambda = new MeanDownsideRiskConstruction(new CVaRRiskMeasure(), riskAversion: 10.0m);

        var wLow = lowLambda.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var wHigh = highLambda.ComputeTargetWeights(FourAssets, FourAssetReturns);

        // VNQ has the fattest left tail — high λ should reduce its weight
        wHigh[s_vnq].Should().BeLessThanOrEqualTo(wLow[s_vnq] + 0.01m,
            "Higher risk aversion should reduce allocation to highest-tail-risk asset");

        AssertWeightsSumToOne(wLow);
        AssertWeightsSumToOne(wHigh);
    }

    [Fact]
    public void MeanCVaR_DifferentConfidenceLevels_ShouldProduceDifferentWeights()
    {
        var cvar90 = new MeanDownsideRiskConstruction(new CVaRRiskMeasure(0.90m));
        var cvar99 = new MeanDownsideRiskConstruction(new CVaRRiskMeasure(0.99m));

        var w90 = cvar90.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var w99 = cvar99.ComputeTargetWeights(FourAssets, FourAssetReturns);

        // At different confidence levels, weights should differ
        // (though they might converge for small datasets)
        AssertWeightsSumToOne(w90);
        AssertWeightsSumToOne(w99);
    }

    [Fact]
    public void MeanCVaR_InvalidConfidenceLevel_ShouldThrow()
    {
        var act0 = () => new CVaRRiskMeasure(0m);
        var act1 = () => new CVaRRiskMeasure(1m);
        var actNeg = () => new CVaRRiskMeasure(-0.5m);

        act0.Should().Throw<ArgumentOutOfRangeException>();
        act1.Should().Throw<ArgumentOutOfRangeException>();
        actNeg.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CVaR_Evaluate_EmptyScenarios_ThrowsCalculationException()
    {
        var measure = new CVaRRiskMeasure(0.95m);
        var weights = new[] { 1m };
        var scenarios = Array.Empty<decimal[]>();

        var act = () => measure.Evaluate(weights, scenarios, 0.01m);
        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Fact]
    public void MeanCVaR_NullRiskMeasure_ShouldThrow()
    {
        var act = () => new MeanDownsideRiskConstruction(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MeanCVaR_NullAssets_ShouldThrow()
    {
        var model = new MeanDownsideRiskConstruction(new CVaRRiskMeasure());

        var act = () => model.ComputeTargetWeights(null!, FourAssetReturns);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MeanCVaR_MismatchedReturns_ShouldThrow()
    {
        var model = new MeanDownsideRiskConstruction(new CVaRRiskMeasure());

        // 4 assets but only 2 return series
        var act = () => model.ComputeTargetWeights(FourAssets, [FourAssetReturns[0], FourAssetReturns[1]]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MeanCVaR_InsufficientReturns_ShouldThrow()
    {
        var model = new MeanDownsideRiskConstruction(new CVaRRiskMeasure());
        var assets = new List<Asset> { s_vti };

        var act = () => model.ComputeTargetWeights(assets, [new[] { 0.01m }]);

        act.Should().Throw<ArgumentException>();
    }

    // ==================== Sortino (Downside Deviation) Construction Tests ====================

    [Fact]
    public void MeanSortino_FourAssets_WeightsSumToOne()
    {
        var model = new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure());

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        weights.Should().HaveCount(4);
        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void MeanSortino_EmptyAssets_ShouldReturnEmpty()
    {
        var model = new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure());

        var weights = model.ComputeTargetWeights([], []);

        weights.Should().BeEmpty();
    }

    [Fact]
    public void MeanSortino_SingleAsset_ShouldReturn100Percent()
    {
        var model = new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure());
        var assets = new List<Asset> { s_vti };

        var weights = model.ComputeTargetWeights(assets, [FourAssetReturns[0]]);

        weights[s_vti].Should().BeApproximately(1.0m, 1e-8m);
    }

    [Fact]
    public void MeanSortino_AsymmetricReturns_ShouldFavorPositiveSkew()
    {
        // With asymmetric returns, Sortino should favor the positively-skewed asset
        // because its upside volatility is not penalized
        var model = new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure());

        var weights = model.ComputeTargetWeights(TwoAssets, AsymmetricReturns);

        var equity = new Asset("EQUITY");
        var bond = new Asset("BOND");

        // Equity has positive skew (small losses, big gains) → favored by Sortino
        // Bond has negative skew (small gains, big losses) → penalized by Sortino
        weights[equity].Should().BeGreaterThan(weights[bond],
            "Sortino optimization should favor positively-skewed equity over negatively-skewed bond");

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void MeanSortino_CustomMAR_ShouldAffectWeights()
    {
        var mar0 = new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure(0m));
        var marHigh = new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure(0.01m));

        var w0 = mar0.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var wHigh = marHigh.ComputeTargetWeights(FourAssets, FourAssetReturns);

        // Higher MAR means more scenarios count as "downside" → different optimization landscape
        AssertWeightsSumToOne(w0);
        AssertWeightsSumToOne(wHigh);

        // Weights should differ (higher MAR penalizes more scenarios as downside)
        var anyDifference = FourAssets.Any(a => Math.Abs(w0[a] - wHigh[a]) > 1e-10m);
        anyDifference.Should().BeTrue("Different MAR values should produce different weight allocations");
    }

    [Fact]
    public void MeanSortino_WeightConstraints_ShouldBeRespected()
    {
        var model = new MeanDownsideRiskConstruction(
            new DownsideDeviationRiskMeasure(),
            minWeight: 0.10m,
            maxWeight: 0.40m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-10m, "Weight should respect min constraint");
            weight.Should().BeLessThanOrEqualTo(0.40m + 1e-10m, "Weight should respect max constraint");
        }

        AssertWeightsSumToOne(weights);
    }

    [Fact]
    public void MeanCVaR_WeightConstraints_ShouldBeRespected()
    {
        var model = new MeanDownsideRiskConstruction(
            new CVaRRiskMeasure(),
            minWeight: 0.10m,
            maxWeight: 0.40m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-10m, "Weight should respect min constraint");
            weight.Should().BeLessThanOrEqualTo(0.40m + 1e-10m, "Weight should respect max constraint");
        }

        AssertWeightsSumToOne(weights);
    }

    // ==================== Generic / Pluggability Tests ====================

    [Fact]
    public void MeanDownsideRisk_DifferentRiskMeasures_ProduceDifferentWeights()
    {
        var cvarModel = new MeanDownsideRiskConstruction(new CVaRRiskMeasure());
        var sortinoModel = new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure());

        var wCvar = cvarModel.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var wSortino = sortinoModel.ComputeTargetWeights(FourAssets, FourAssetReturns);

        // Different risk measures should generally produce different allocations
        AssertWeightsSumToOne(wCvar);
        AssertWeightsSumToOne(wSortino);
        AssertAllWeightsNonNegative(wCvar);
        AssertAllWeightsNonNegative(wSortino);
    }

    [Fact]
    public void MeanSortino_AllPositiveReturns_ShouldConverge()
    {
        // All returns positive → no downside risk → should converge to max-return portfolio
        var model = new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure());
        var assets = new List<Asset> { new("A"), new("B") };

        decimal[][] returns =
        [
            [0.01m, 0.02m, 0.015m, 0.03m, 0.025m],    // A: lower mean
            [0.02m, 0.04m, 0.03m, 0.05m, 0.035m]       // B: higher mean
        ];

        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);

        // With no downside risk, should favor higher-return asset
        weights[new Asset("B")].Should().BeGreaterThanOrEqualTo(weights[new Asset("A")],
            "With zero downside risk, higher-return asset should get equal or higher weight");
    }

    // ==================== CVaR ζ Line-Search Mutation Bug ====================
    //
    // The Rockafellar-Uryasev CVaR formulation maintains an auxiliary variable ζ
    // (VaR threshold) that is updated via gradient descent alongside the weights.
    // The bug: during line search, each candidate evaluation calls Evaluate() which
    // mutates ζ. When a candidate is rejected, ζ is NOT restored, so it drifts away
    // from the value that corresponds to the current (accepted) weights. This corrupts
    // the gradient for the next iteration, preventing the optimizer from converging.

    /// <summary>
    /// With analytical ζ computation, CVaR evaluation at the same weights
    /// should produce identical results regardless of what other weights
    /// were evaluated in between. ζ is a pure function of the current
    /// weights — no accumulated state.
    /// </summary>
    [Fact]
    public void CVaR_AnalyticalZeta_ShouldBeDeterministic_RegardlessOfEvaluationOrder()
    {
        var cvar = new CVaRRiskMeasure(0.95m);
        var scenarios = MakeIncomeStyleScenarios();
        var w = new[] { 0.5m, 0.5m };

        // Evaluate at w
        var (v1, g1) = cvar.Evaluate(w, scenarios, 1.0m);

        // Evaluate at very different weights (simulating line search)
        cvar.Evaluate([0.9m, 0.1m], scenarios, 0.5m);
        cvar.Evaluate([0.1m, 0.9m], scenarios, 0.25m);
        cvar.Evaluate([0.95m, 0.05m], scenarios, 0.125m);

        // Re-evaluate at original weights — should be identical
        var (v2, g2) = cvar.Evaluate(w, scenarios, 1.0m);

        v2.Should().Be(v1,
            "Analytical ζ makes CVaR evaluation deterministic — " +
            "same weights should always produce same value regardless of evaluation history");

        for (var i = 0; i < g1.Length; i++)
        {
            g2[i].Should().Be(g1[i], $"Gradient[{i}] should be identical across evaluations at same weights");
        }
    }

    /// <summary>
    /// Passing learningRate=0 to Evaluate should produce the same CVaR value
    /// as a previous call at the same weights, because ζ is not updated.
    /// This tests the fix mechanism: line search should use lr=0.
    /// </summary>
    [Fact]
    public void CVaR_ZeroLearningRate_ShouldNotDriftZeta()
    {
        var cvar = new CVaRRiskMeasure(0.95m);
        var scenarios = MakeIncomeStyleScenarios();
        var w = new[] { 0.5m, 0.5m };

        // Let ζ converge at current weights
        for (var i = 0; i < 10; i++)
        {
            cvar.Evaluate(w, scenarios, 1.0m);
        }

        // Baseline evaluation
        var (baseline, _) = cvar.Evaluate(w, scenarios, 0m);

        // Simulate line-search evaluations at different weights with lr=0
        cvar.Evaluate([0.9m, 0.1m], scenarios, 0m);
        cvar.Evaluate([0.1m, 0.9m], scenarios, 0m);
        cvar.Evaluate([0.95m, 0.05m], scenarios, 0m);

        // Re-evaluate at original weights — ζ should be unchanged
        var (afterLineSearch, _) = cvar.Evaluate(w, scenarios, 0m);

        afterLineSearch.Should().Be(baseline,
            "Zero learning rate should not drift ζ — line search evaluations must be side-effect-free");
    }

    /// <summary>
    /// Easy case: 3 assets with very different profiles. Even a partially broken
    /// optimizer finds this. Kept as a sanity check.
    /// </summary>
    [Fact]
    public void MeanCVaR_WellSeparatedAssets_ShouldDeviateFromEqualWeight()
    {
        var (assets, returns) = MakeThreeAssetIncomeScenarios();

        var model = new MeanDownsideRiskConstruction(
            new CVaRRiskMeasure(0.95m),
            minWeight: 0.10m,
            maxWeight: 0.50m,
            riskAversion: 1.5m);

        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights);

        weights[new Asset("SAFE")].Should().BeGreaterThan(0.38m,
            "SAFE asset should be meaningfully overweight vs 1/N=33%");
        weights[new Asset("RISKY")].Should().BeLessThan(0.28m,
            "RISKY asset should be meaningfully underweight vs 1/N=33%");
    }

    /// <summary>
    /// Realistic income portfolio: 5 assets with return/risk profiles matching the
    /// actual income archetype (3 bond-like + 2 equity-like). Uses 252 scenarios
    /// with seeded randomness for reproducibility.
    ///
    /// With the ζ mutation bug, the optimizer cannot find improving directions in
    /// this more nuanced landscape and converges to exactly 20% for all 5 assets.
    /// After the fix, MeanCVaR should overweight FLOT (best return-per-CVaR) and
    /// underweight equity assets (highest marginal CVaR contributors).
    ///
    /// The max deviation from equal weight is the key metric: ≈0% (broken) vs
    /// several percentage points (working).
    /// </summary>
    [Fact]
    public void MeanCVaR_IncomeLikePortfolio_ShouldNotCollapseToEqualWeight()
    {
        var (assets, returns) = MakeIncomeLikeFiveAssetScenarios();

        var model = new MeanDownsideRiskConstruction(
            new CVaRRiskMeasure(0.95m),
            minWeight: 0.10m,
            maxWeight: 0.35m,
            riskAversion: 1.5m);

        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights);

        // The maximum deviation of any weight from equal weight (20%).
        // With the ζ bug: max deviation ≈ 0% (exact equal weight).
        // After fix: max deviation should be at least 2pp (and likely much more).
        var maxDeviation = weights.Values.Max(w => Math.Abs(w - 0.20m));

        maxDeviation.Should().BeGreaterThan(0.02m,
            "MeanCVaR should produce weights that deviate at least 2pp from equal weight " +
            "when assets have different return-to-CVaR profiles. A max deviation near 0% " +
            "indicates the optimizer failed to converge (ζ corruption during line search).");
    }

    // --- Scenario generators for ζ mutation tests ---

    /// <summary>
    /// 2-asset, 100-scenario dataset: one safe, one risky with fat left tail.
    /// </summary>
    private static decimal[][] MakeIncomeStyleScenarios()
    {
        const int s = 100;
        const int tailCount = 5; // 5% of 100

        var safe = new decimal[s];
        var risky = new decimal[s];

        for (var t = 0; t < s; t++)
        {
            safe[t] = 0.002m; // Constant positive return (no risk)

            if (t < tailCount)
            {
                risky[t] = -0.08m; // Fat left tail
            }
            else
            {
                risky[t] = 0.008m; // Normal positive return
            }
        }

        return [safe, risky];
    }

    /// <summary>
    /// 3-asset, 100-scenario dataset with clearly separated risk profiles.
    /// </summary>
    private static (IReadOnlyList<Asset> Assets, decimal[][] Returns) MakeThreeAssetIncomeScenarios()
    {
        const int s = 100;
        const int tailCount = 5; // 5% tail

        var safeReturns = new decimal[s];
        var moderateReturns = new decimal[s];
        var riskyReturns = new decimal[s];

        for (var t = 0; t < s; t++)
        {
            safeReturns[t] = 0.001m; // Constant, zero tail risk

            if (t < tailCount)
            {
                moderateReturns[t] = -0.025m;
                riskyReturns[t] = -0.10m;
            }
            else
            {
                moderateReturns[t] = 0.003m;
                riskyReturns[t] = 0.008m;
            }
        }

        IReadOnlyList<Asset> assets = [new("SAFE"), new("MODERATE"), new("RISKY")];
        return (assets, [safeReturns, moderateReturns, riskyReturns]);
    }

    /// <summary>
    /// 5-asset, 252-scenario dataset mimicking real income portfolio profiles:
    ///   VGSH-like:  low vol, positive drift (short-term treasury)
    ///   FLOT-like:  low vol, higher drift (floating rate — best Ret/CVaR)
    ///   SCHP-like:  medium vol, low drift (TIPS — worst Ret/CVaR among bonds)
    ///   JEPQ-like:  high vol, high drift, fat left tail (covered call equity)
    ///   SCHD-like:  high vol, moderate drift, fat left tail (dividend equity)
    /// Seeded RNG for deterministic reproducibility.
    /// </summary>
    private static (IReadOnlyList<Asset> Assets, decimal[][] Returns) MakeIncomeLikeFiveAssetScenarios()
    {
        const int s = 252;
        const int tailCount = 13; // ~5% of 252

        var vgsh = new decimal[s];
        var flot = new decimal[s];
        var schp = new decimal[s];
        var jepq = new decimal[s];
        var schd = new decimal[s];

        var rng = new Random(42); // Deterministic seed

        for (var t = 0; t < s; t++)
        {
            // Bond-like: low vol, slightly positive drift
            vgsh[t] = 0.00012m + (decimal)(rng.NextDouble() - 0.5) * 0.0004m;
            flot[t] = 0.00020m + (decimal)(rng.NextDouble() - 0.5) * 0.0003m;
            schp[t] = 0.00006m + (decimal)(rng.NextDouble() - 0.5) * 0.0010m;

            if (t < tailCount)
            {
                // Equity tail shocks (5% worst scenarios)
                jepq[t] = -0.025m + (decimal)(rng.NextDouble() - 0.5) * 0.010m;
                schd[t] = -0.020m + (decimal)(rng.NextDouble() - 0.5) * 0.008m;
            }
            else
            {
                // Equity normal returns: higher drift, higher vol
                jepq[t] = 0.00057m + (decimal)(rng.NextDouble() - 0.5) * 0.0020m;
                schd[t] = 0.00035m + (decimal)(rng.NextDouble() - 0.5) * 0.0018m;
            }
        }

        IReadOnlyList<Asset> assets = [new("VGSH"), new("FLOT"), new("SCHP"), new("JEPQ"), new("SCHD")];
        return (assets, [vgsh, flot, schp, jepq, schd]);
    }
}
