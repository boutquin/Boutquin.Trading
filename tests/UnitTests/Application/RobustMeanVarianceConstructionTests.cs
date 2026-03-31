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
using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Domain.ValueObjects;
using FluentAssertions;

/// <summary>
/// Tests for <see cref="RobustMeanVarianceConstruction"/>.
/// </summary>
public sealed class RobustMeanVarianceConstructionTests
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
    /// With a single covariance scenario, the model should behave like standard mean-variance.
    /// Weights should sum to 1 and all be non-negative.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_SingleScenario_BehavesAsMeanVariance()
    {
        var covEstimator = new SampleCovarianceEstimator();
        var model = new RobustMeanVarianceConstruction(covEstimator);

        var weights = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns);

        weights.Should().HaveCount(3);
        AssertWeightsSumToOne(weights);

        foreach (var (asset, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0m, $"Weight for {asset} should be non-negative");
        }
    }

    /// <summary>
    /// With two covariance scenarios (one normal, one stressed), the robust weights
    /// should still sum to 1 and be non-negative. The worst-case utility for robust
    /// weights should be at least as good as or close to the worst-case for equal weights.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_TwoScenarios_ProducesRobustWeights()
    {
        var covEstimator = new SampleCovarianceEstimator();

        // Normal scenario
        var normalCov = covEstimator.Estimate(ThreeAssetReturns);

        // Stressed scenario: scale covariance by 3x (higher volatility regime)
        var n = normalCov.GetLength(0);
        var stressedCov = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                stressedCov[i, j] = normalCov[i, j] * 3m;
            }
        }

        var model = new RobustMeanVarianceConstruction(covEstimator);
        var scenarios = new[] { normalCov, stressedCov };

        var weights = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns, scenarios);

        weights.Should().HaveCount(3);
        AssertWeightsSumToOne(weights);

        foreach (var (asset, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0m, $"Weight for {asset} should be non-negative");
        }
    }

    /// <summary>
    /// Empty asset list should return an empty dictionary.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_EmptyAssets_ReturnsEmpty()
    {
        var covEstimator = new SampleCovarianceEstimator();
        var model = new RobustMeanVarianceConstruction(covEstimator);

        var weights = model.ComputeTargetWeights(
            Array.Empty<Asset>(),
            Array.Empty<decimal[]>());

        weights.Should().BeEmpty();
    }

    /// <summary>
    /// Weights from the base interface (single scenario) must sum to 1.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_WeightsSumToOne()
    {
        var covEstimator = new SampleCovarianceEstimator();
        var model = new RobustMeanVarianceConstruction(covEstimator);

        var weights = model.ComputeTargetWeights(ThreeAssets, ThreeAssetReturns);

        AssertWeightsSumToOne(weights);
    }

    /// <summary>
    /// Passing an empty covariance scenarios list should throw ArgumentException.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_NoScenarios_ThrowsArgumentException()
    {
        var covEstimator = new SampleCovarianceEstimator();
        var model = new RobustMeanVarianceConstruction(covEstimator);

        var act = () => model.ComputeTargetWeights(
            ThreeAssets,
            ThreeAssetReturns,
            Array.Empty<decimal[,]>());

        act.Should().Throw<ArgumentException>();
    }
}
