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

using Boutquin.Trading.Application.CovarianceEstimators;
using Boutquin.Trading.Application.DownsideRisk;
using Boutquin.Trading.Application.PortfolioConstruction;
using FluentAssertions;

/// <summary>
/// Input-validation and contract guards for the portfolio-construction optimizers,
/// surfaced by code-arena run 2026-06-20-0712-5ce02a (cluster constr-opt).
/// </summary>
public sealed class ConstructionOptimizerGuardTests
{
    private static readonly Symbol s_a = new("A");
    private static readonly Symbol s_b = new("B");
    private static readonly Symbol s_c = new("C");

    private static IReadOnlyList<Symbol> TwoAssets => [s_a, s_b];

    private static IReadOnlyList<Symbol> ThreeAssets => [s_a, s_b, s_c];

    private static decimal[][] TwoAssetReturns =>
    [
        [0.02m, -0.01m, 0.03m, -0.02m, 0.01m],
        [0.005m, -0.003m, 0.004m, 0.002m, -0.001m]
    ];

    // ===== F-EW-NULL =====

    [Fact]
    public void EqualWeight_NullReturns_ShouldThrowArgumentNullException()
    {
        var model = new EqualWeightConstruction();

        var act = () => model.ComputeTargetWeights(TwoAssets, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ===== F-BL-NORM =====

    [Fact]
    public void BlackLitterman_NoViews_EquilibriumNotSummingToOne_ResultSumsToOne()
    {
        // Equilibrium weights sum to 0.6, not 1.0. The no-views path must still honor
        // the interface sum-to-1.0 guarantee.
        var model = new BlackLittermanConstruction(equilibriumWeights: [0.4m, 0.2m]);

        var weights = model.ComputeTargetWeights(TwoAssets, TwoAssetReturns);

        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-12m,
            "no-views Black-Litterman must return weights summing to 1.0 even when equilibrium weights do not");
    }

    // ===== F-BL-RA0 =====

    [Fact]
    public void BlackLitterman_ZeroRiskAversion_ShouldThrowArgumentOutOfRange()
    {
        var act = () => new BlackLittermanConstruction(
            equilibriumWeights: [0.5m, 0.5m],
            riskAversionCoefficient: 0m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BlackLitterman_NegativeRiskAversion_ShouldThrowArgumentOutOfRange()
    {
        var act = () => new BlackLittermanConstruction(
            equilibriumWeights: [0.5m, 0.5m],
            riskAversionCoefficient: -2.5m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DynamicBlackLitterman_ZeroRiskAversion_ShouldThrowArgumentOutOfRange()
    {
        var act = () => new DynamicBlackLittermanConstruction(
            viewSpecs: [],
            riskAversionCoefficient: 0m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ===== F-RMV-DIM =====

    [Fact]
    public void RobustMeanVariance_ScenarioWrongDimensions_ShouldThrowArgumentException()
    {
        var model = new RobustMeanVarianceConstruction(new SampleCovarianceEstimator());

        // Two scenarios so the multi-scenario indexing path is exercised; the second is 1x1
        // against a 2-asset universe — a dimension mismatch that would otherwise throw
        // IndexOutOfRangeException deep in the optimizer.
        var good = new decimal[2, 2] { { 0.01m, 0.0m }, { 0.0m, 0.02m } };
        var bad = new decimal[1, 1] { { 0.01m } };

        var act = () => model.ComputeTargetWeights(TwoAssets, TwoAssetReturns, [good, bad]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RobustMeanVariance_RaggedReturns_ShouldThrowArgumentException()
    {
        var model = new RobustMeanVarianceConstruction(new SampleCovarianceEstimator());

        var good = new decimal[2, 2] { { 0.01m, 0.0m }, { 0.0m, 0.02m } };
        decimal[][] ragged = [[0.02m, -0.01m, 0.03m], [0.005m]];

        var act = () => model.ComputeTargetWeights(TwoAssets, ragged, [good]);

        act.Should().Throw<ArgumentException>();
    }

    // ===== M-MDR =====

    [Fact]
    public void MeanDownsideRisk_RaggedReturns_ShouldThrowArgumentException()
    {
        var model = new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure());

        // Series 1 is shorter than series 0 → transpose would throw IndexOutOfRangeException.
        decimal[][] ragged = [[0.02m, -0.01m, 0.03m], [0.005m, -0.003m]];

        var act = () => model.ComputeTargetWeights(TwoAssets, ragged);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MeanDownsideRisk_OverLengthSeries_ShouldThrowArgumentException()
    {
        var model = new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure());

        // Series 1 is longer than series 0 → trailing observations would be silently truncated.
        decimal[][] ragged = [[0.02m, -0.01m], [0.005m, -0.003m, 0.004m]];

        var act = () => model.ComputeTargetWeights(TwoAssets, ragged);

        act.Should().Throw<ArgumentException>();
    }

    // ===== F-MAXDIV-OBJ =====

    // Three assets with materially different volatilities and low cross-correlation,
    // so the maximum-diversification portfolio (DR maximization) and the minimum-variance
    // portfolio are distinct optima. A.vol ≈ 1%, B.vol ≈ 2%, C.vol ≈ 4%.
    private static decimal[][] UnequalVolReturns =>
    [
        [0.010m, -0.010m, 0.011m, -0.009m, 0.010m, -0.011m, 0.009m, -0.010m, 0.011m, -0.010m],
        [0.021m, 0.019m, -0.020m, -0.021m, 0.020m, 0.019m, -0.020m, -0.019m, 0.021m, -0.020m],
        [-0.040m, 0.041m, 0.039m, -0.041m, -0.039m, 0.040m, 0.041m, -0.040m, 0.039m, -0.041m]
    ];

    [Fact]
    public void MaxDiversification_BindingCap_OptimizesDiversificationNotMinVariance()
    {
        // With a binding cap that forces the constrained branch, the maximum-diversification
        // model must still optimize the diversification objective — NOT silently solve the
        // bounded minimum-variance problem. The two are different optima for unequal vols.
        const decimal cap = 0.5m;

        var maxDiv = new MaximumDiversificationConstruction(maxWeight: cap);
        var minVar = new MinimumVarianceConstruction(maxWeight: cap);

        var maxDivW = maxDiv.ComputeTargetWeights(ThreeAssets, UnequalVolReturns);
        var minVarW = minVar.ComputeTargetWeights(ThreeAssets, UnequalVolReturns);

        // Sanity: the cap is actually binding (some unconstrained weight would exceed it).
        maxDivW.Values.Sum().Should().BeApproximately(1.0m, 1e-8m);
        maxDivW.Values.Max().Should().BeLessThanOrEqualTo(cap + 1e-8m, "the cap must bind");

        var totalDiff = ThreeAssets.Sum(a => Math.Abs(maxDivW[a] - minVarW[a]));
        totalDiff.Should().BeGreaterThan(1e-4m,
            "constrained maximum-diversification weights must differ from constrained minimum-variance weights");
    }

    // ===== F-VA-001 =====

    // Two assets with very different volatilities so the unconstrained maximum-diversification
    // weight is heavily skewed toward the low-vol asset, breaching the box and forcing the
    // constrained waterfilling projector. A.vol ≈ 0.4%, B.vol ≈ 4% → unconstrained weight on A
    // far exceeds maxWeight, so on the first waterfilling round one coordinate saturates to the
    // cap while the other drops to the floor — the case where the residual mass was dropped.
    private static decimal[][] SkewedVolReturns =>
    [
        [0.004m, -0.004m, 0.004m, -0.004m, 0.004m, -0.004m, 0.004m, -0.004m, 0.004m, -0.004m],
        [0.040m, 0.038m, -0.041m, -0.039m, 0.040m, 0.039m, -0.040m, -0.038m, 0.041m, -0.040m]
    ];

    [Fact]
    public void MaxDiversification_BoxSaturatesBothBoundsInOneRound_WeightsStillSumToOne()
    {
        // Box [0.183, 0.595] for two assets: maxWeight < 1 forces the constrained branch, and
        // the skewed un-normalized weight saturates one coordinate to 0.595 (max) and the other
        // to 0.183 (min) in a single waterfilling round. The projector previously broke out with
        // freeCount==0, leaving 1 − (0.595 + 0.183) = 0.222 of capital unallocated.
        const decimal min = 0.183m;
        const decimal max = 0.595m;

        var model = new MaximumDiversificationConstruction(minWeight: min, maxWeight: max);

        var weights = model.ComputeTargetWeights(TwoAssets, SkewedVolReturns);

        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-8m,
            "the constrained waterfilling projector must allocate all capital even when one " +
            "coordinate saturates to max and another to min in the same round");

        // Bounds must still be respected (no over-allocation past max, no under-allocation below min).
        foreach (var w in weights.Values)
        {
            w.Should().BeGreaterThanOrEqualTo(min - 1e-8m, "the floor must be respected");
            w.Should().BeLessThanOrEqualTo(max + 1e-8m, "the cap must be respected");
        }
    }

    // ===== F-VB-01 =====

    [Fact]
    public void MeanDownsideRisk_FirstSeriesNull_ShouldThrowArgumentException()
    {
        var model = new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure());

        // returns[0] is null but the array length matches assets.Count, so the count guard
        // passes. The per-series null guard previously started at i=1, so returns[0].Length
        // was dereferenced first → raw NullReferenceException instead of the documented
        // ArgumentException for degenerate input.
        decimal[][] returns = [null!, [0.02m, -0.01m, 0.03m], [0.005m, -0.003m, 0.004m]];

        var act = () => model.ComputeTargetWeights(ThreeAssets, returns);

        act.Should().Throw<ArgumentException>();
    }
}
