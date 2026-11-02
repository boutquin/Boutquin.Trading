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

using Boutquin.Trading.Application.PortfolioConstruction;
using FluentAssertions;

/// <summary>
/// Tests for <see cref="TurnoverPenalizedConstruction"/>.
/// </summary>
public sealed class TurnoverPenalizedConstructionTests
{
    private static readonly Symbol s_vti = new("VTI");
    private static readonly Symbol s_tlt = new("TLT");
    private static readonly Symbol s_gld = new("GLD");

    private static IReadOnlyList<Symbol> ThreeAssets => [s_vti, s_tlt, s_gld];

    private static decimal[][] ThreeAssetReturns =>
    [
        [0.02m, -0.01m, 0.03m, -0.02m, 0.01m, 0.04m, -0.03m, 0.02m, -0.01m, 0.03m],
        [0.005m, -0.003m, 0.004m, 0.002m, -0.001m, 0.003m, -0.002m, 0.001m, 0.004m, -0.003m],
        [0.01m, -0.02m, 0.015m, -0.005m, 0.02m, -0.01m, 0.005m, 0.03m, -0.025m, 0.01m]
    ];

    private static void AssertWeightsSumToOne(IReadOnlyDictionary<Symbol, decimal> weights)
    {
        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-8m, "Weights must sum to 1.0");
    }

    /// <summary>
    /// On the first call there are no previous weights, so the model should delegate
    /// directly to the inner model (EqualWeight = 1/N each).
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_FirstCall_DelegatesToInner()
    {
        var inner = new EqualWeightConstruction();
        var model = new TurnoverPenalizedConstruction(inner, lambda: 0.05m);

        var weights = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns);

        weights.Should().HaveCount(3);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeApproximately(1m / 3m, 1e-8m,
                "First call should match inner model (equal weight)");
        }

        AssertWeightsSumToOne(weights);
    }

    /// <summary>
    /// Second call should produce weights that are between the inner model target
    /// and the previous weights — i.e., turnover is reduced.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_SecondCall_ReducesTurnover()
    {
        var inner = new EqualWeightConstruction();
        var model = new TurnoverPenalizedConstruction(inner, lambda: 0.05m);

        // First call: sets previous weights to equal weight (1/3 each)
        _ = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns);

        // Second call: inner still returns 1/3 each, previous is 1/3 each,
        // so the result should still be 1/3 each (no deviation to penalize).
        var second = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns);

        // With identical inner target and previous, the result should match
        foreach (var (_, weight) in second)
        {
            weight.Should().BeApproximately(1m / 3m, 1e-6m,
                "Same target and previous should yield same weights");
        }

        AssertWeightsSumToOne(second);
    }

    /// <summary>
    /// When lambda = 0, the turnover penalty is disabled and the result should
    /// always match the inner model.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_LambdaZero_MatchesInner()
    {
        var inner = new EqualWeightConstruction();
        var model = new TurnoverPenalizedConstruction(inner, lambda: 0m);

        // First call
        _ = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns);

        // Second call — even with previous weights stored, lambda=0 means no penalty
        var second = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns);

        foreach (var (_, weight) in second)
        {
            weight.Should().BeApproximately(1m / 3m, 1e-8m,
                "Lambda=0 should always return inner model result");
        }

        AssertWeightsSumToOne(second);
    }

    /// <summary>
    /// Weights produced by the model must always sum to 1.0.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_WeightsSumToOne()
    {
        var inner = new EqualWeightConstruction();
        var model = new TurnoverPenalizedConstruction(inner, lambda: 0.10m);

        var weights = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns);

        AssertWeightsSumToOne(weights);
    }

    /// <summary>
    /// Negative lambda should throw ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Constructor_NegativeLambda_ThrowsArgumentOutOfRangeException()
    {
        var inner = new EqualWeightConstruction();

        var act = () => new TurnoverPenalizedConstruction(inner, lambda: -0.01m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ----------------------------------------------------------------------
    // Stateless explicit-currentWeights overload (as-of / per-cohort path).
    // ----------------------------------------------------------------------

    private static readonly IReadOnlyDictionary<Symbol, decimal> s_vtiHeavyPrev =
        new Dictionary<Symbol, decimal> { [s_vti] = 0.8m, [s_tlt] = 0.1m, [s_gld] = 0.1m };

    private static readonly IReadOnlyDictionary<Symbol, decimal> s_gldHeavyPrev =
        new Dictionary<Symbol, decimal> { [s_vti] = 0.1m, [s_tlt] = 0.1m, [s_gld] = 0.8m };

    /// <summary>
    /// Leaf 1: with lambda &gt; 0 and an explicit w_prev distinct from the inner target,
    /// the result is pulled toward w_prev relative to the no-penalty target (equal weight).
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_ExplicitCurrentWeights_PullsTowardPrevious()
    {
        var inner = new EqualWeightConstruction();
        var model = new TurnoverPenalizedConstruction(inner, lambda: 0.10m);

        var weights = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns, s_vtiHeavyPrev);

        weights[s_vti].Should().BeGreaterThan(1m / 3m,
            "the VTI-heavy previous weights should pull VTI above its equal-weight target");
        AssertWeightsSumToOne(weights);
    }

    /// <summary>
    /// Leaf 2: a null explicit w_prev short-circuits to the inner model's target unchanged.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_NullCurrentWeights_ReturnsInnerTarget()
    {
        var inner = new EqualWeightConstruction();
        var model = new TurnoverPenalizedConstruction(inner, lambda: 0.10m);

        var weights = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns, currentWeights: null);

        foreach (var (_, weight) in weights)
        {
            weight.Should().BeApproximately(1m / 3m, 1e-8m,
                "null current weights means no penalty — inner equal-weight target");
        }
    }

    /// <summary>
    /// Leaf 3 (load-bearing): the explicit overload does not mutate instance state.
    /// After a 3-arg call, a subsequent 2-arg call behaves as a <em>first</em> call
    /// (delegates to inner, no penalty), proving <c>_previousWeights</c> was left untouched.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_ExplicitOverload_DoesNotMutateTrackedState()
    {
        var inner = new EqualWeightConstruction();
        var model = new TurnoverPenalizedConstruction(inner, lambda: 0.10m);
        model.Reset();

        _ = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns, s_vtiHeavyPrev);

        // If the 3-arg call had seeded _previousWeights, this 2-arg call would be penalized.
        var stateful = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns);

        foreach (var (_, weight) in stateful)
        {
            weight.Should().BeApproximately(1m / 3m, 1e-8m,
                "the 2-arg call must behave as a first call — the 3-arg overload left no state");
        }
    }

    /// <summary>
    /// Leaf 4: two explicit calls with different w_prev each penalize against their own
    /// previous weights — one instance serves independent cohorts without cross-contamination.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_DifferentCurrentWeights_DoNotCrossContaminate()
    {
        var inner = new EqualWeightConstruction();
        var model = new TurnoverPenalizedConstruction(inner, lambda: 0.10m);

        var vtiCohort = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns, s_vtiHeavyPrev);
        var gldCohort = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns, s_gldHeavyPrev);

        vtiCohort[s_vti].Should().BeGreaterThan(gldCohort[s_vti],
            "the VTI-heavy cohort must tilt to VTI more than the GLD-heavy cohort");
        gldCohort[s_gld].Should().BeGreaterThan(vtiCohort[s_gld],
            "the GLD-heavy cohort must tilt to GLD more than the VTI-heavy cohort");
    }

    /// <summary>
    /// Leaf 5 (SPEC-REPRO): identical inputs yield byte-identical weights — no wall-clock,
    /// random, or iteration-order leakage.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_ExplicitOverload_IsDeterministic()
    {
        var inner = new EqualWeightConstruction();
        var model = new TurnoverPenalizedConstruction(inner, lambda: 0.07m);

        var first = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns, s_vtiHeavyPrev);
        var second = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns, s_vtiHeavyPrev);

        foreach (var symbol in ThreeAssets)
        {
            second[symbol].Should().Be(first[symbol],
                "same (assets, returns, currentWeights) must produce byte-identical weights");
        }
    }

    /// <summary>
    /// Leaf 6 (characterization): the stateful 2-arg path is byte-identical after the refactor —
    /// first call delegates to inner, a repeated call with the same target/previous holds steady.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_TwoArgPath_CharacterizationUnchanged()
    {
        var inner = new EqualWeightConstruction();
        var model = new TurnoverPenalizedConstruction(inner, lambda: 0.05m);

        var first = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns);
        var second = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns);

        foreach (var symbol in ThreeAssets)
        {
            first[symbol].Should().BeApproximately(1m / 3m, 1e-8m,
                "first 2-arg call delegates to the inner equal-weight model");
            second[symbol].Should().BeApproximately(1m / 3m, 1e-6m,
                "second 2-arg call with identical target and previous holds at equal weight");
        }
    }
}
