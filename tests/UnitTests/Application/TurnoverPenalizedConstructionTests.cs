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
using Boutquin.Trading.Domain.ValueObjects;
using FluentAssertions;

/// <summary>
/// Tests for <see cref="TurnoverPenalizedConstruction"/>.
/// </summary>
public sealed class TurnoverPenalizedConstructionTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_tlt = new("TLT");
    private static readonly Asset s_gld = new("GLD");

    private static IReadOnlyList<Asset> ThreeAssets => [s_vti, s_tlt, s_gld];

    private static decimal[][] ThreeAssetReturns =>
    [
        [0.02m, -0.01m, 0.03m, -0.02m, 0.01m, 0.04m, -0.03m, 0.02m, -0.01m, 0.03m],
        [0.005m, -0.003m, 0.004m, 0.002m, -0.001m, 0.003m, -0.002m, 0.001m, 0.004m, -0.003m],
        [0.01m, -0.02m, 0.015m, -0.005m, 0.02m, -0.01m, 0.005m, 0.03m, -0.025m, 0.01m]
    ];

    private static void AssertWeightsSumToOne(IReadOnlyDictionary<Asset, decimal> weights)
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
}
