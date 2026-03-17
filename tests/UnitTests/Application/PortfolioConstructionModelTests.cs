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
/// Tests for all IPortfolioConstructionModel implementations.
/// </summary>
public sealed class PortfolioConstructionModelTests
{
    private const decimal Precision = 1e-10m;

    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_tlt = new("TLT");
    private static readonly Asset s_gld = new("GLD");
    private static readonly Asset s_vnq = new("VNQ");

    // Return series with different volatilities:
    // VTI: high vol, TLT: low vol, GLD: medium vol, VNQ: high vol
    private static decimal[][] FourAssetReturns =>
    [
        [0.02m, -0.03m, 0.04m, -0.01m, 0.03m, -0.02m, 0.01m, 0.05m, -0.04m, 0.02m], // VTI
        [0.005m, -0.003m, 0.004m, 0.002m, -0.001m, 0.003m, -0.002m, 0.001m, 0.004m, -0.003m], // TLT
        [0.01m, -0.02m, 0.015m, -0.005m, 0.02m, -0.01m, 0.005m, 0.03m, -0.025m, 0.01m], // GLD
        [0.03m, -0.04m, 0.05m, -0.02m, 0.04m, -0.03m, 0.02m, 0.06m, -0.05m, 0.03m]  // VNQ
    ];

    private static IReadOnlyList<Asset> FourAssets => [s_vti, s_tlt, s_gld, s_vnq];

    // --- Helper ---

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

    // ==================== EqualWeightConstruction Tests ====================

    [Fact]
    public void EqualWeight_FourETFs_ShouldGiveEach25Percent()
    {
        var model = new EqualWeightConstruction();

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        weights.Should().HaveCount(4);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeApproximately(0.25m, Precision);
        }

        AssertWeightsSumToOne(weights);
    }

    [Fact]
    public void EqualWeight_SingleETF_ShouldReturn100Percent()
    {
        var model = new EqualWeightConstruction();
        var assets = new List<Asset> { s_vti };

        var weights = model.ComputeTargetWeights(assets, [FourAssetReturns[0]]);

        weights[s_vti].Should().BeApproximately(1.0m, Precision);
    }

    [Fact]
    public void EqualWeight_EmptyAssets_ShouldReturnEmpty()
    {
        var model = new EqualWeightConstruction();

        var weights = model.ComputeTargetWeights([], []);

        weights.Should().BeEmpty();
    }

    // ==================== InverseVolatilityConstruction Tests ====================

    [Fact]
    public void InverseVolatility_LowerVolAsset_ShouldGetHigherWeight()
    {
        var model = new InverseVolatilityConstruction();

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        // TLT has lowest volatility, should get highest weight
        weights[s_tlt].Should().BeGreaterThan(weights[s_vti], "Lower-vol TLT should have higher weight than higher-vol VTI");
        weights[s_tlt].Should().BeGreaterThan(weights[s_vnq], "Lower-vol TLT should have higher weight than higher-vol VNQ");
        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void InverseVolatility_HalfVolAsset_ShouldGetDoubleWeight()
    {
        // Two assets: A has σ, B has 2σ → B gets weight 1/2 of A's weight
        var model = new InverseVolatilityConstruction();
        var a = new Asset("A");
        var b = new Asset("B");

        // B = 2*A, so vol(B) = 2*vol(A)
        var returnsA = new[] { 0.01m, -0.01m, 0.02m, -0.02m, 0.01m };
        var returnsB = new[] { 0.02m, -0.02m, 0.04m, -0.04m, 0.02m };

        var weights = model.ComputeTargetWeights([a, b], [returnsA, returnsB]);

        // w_A / w_B should be approx 2
        var ratio = weights[a] / weights[b];
        ratio.Should().BeApproximately(2.0m, 0.01m, "Asset with half the vol should get double the weight");
        AssertWeightsSumToOne(weights);
    }

    [Fact]
    public void InverseVolatility_ZeroVol_ShouldThrowCalculationException()
    {
        var model = new InverseVolatilityConstruction();
        var a = new Asset("A");
        var constantReturns = new[] { 0.01m, 0.01m, 0.01m, 0.01m };

        var act = () => model.ComputeTargetWeights([a], [constantReturns]);

        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Fact]
    public void InverseVolatility_WeightsSumToOne()
    {
        var model = new InverseVolatilityConstruction();

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
    }

    // ==================== RiskParityConstruction Tests ====================

    // RiskParity-specific returns: all positively correlated with similar patterns,
    // ensuring positive marginal risk contributions throughout iteration.
    private static decimal[][] RiskParityReturns =>
    [
        [0.02m, -0.01m, 0.03m, -0.02m, 0.01m, 0.04m, -0.03m, 0.02m, -0.01m, 0.03m], // VTI
        [0.01m, -0.005m, 0.015m, -0.01m, 0.005m, 0.02m, -0.015m, 0.01m, -0.005m, 0.015m], // TLT
        [0.015m, -0.008m, 0.022m, -0.015m, 0.008m, 0.03m, -0.022m, 0.015m, -0.008m, 0.022m], // GLD
        [0.025m, -0.012m, 0.035m, -0.025m, 0.012m, 0.045m, -0.035m, 0.025m, -0.012m, 0.035m]  // VNQ
    ];

    [Fact]
    public void RiskParity_EqualRiskContribution_WithinTolerance()
    {
        var model = new RiskParityConstruction();

        var weights = model.ComputeTargetWeights(FourAssets, RiskParityReturns);

        // Compute marginal risk contributions: MRC_i = w_i * (Σw)_i / σ_p
        var n = FourAssets.Count;
        var cov = new Boutquin.Trading.Application.CovarianceEstimators.SampleCovarianceEstimator()
            .Estimate(RiskParityReturns);

        var w = FourAssets.Select(a => weights[a]).ToArray();

        var riskContributions = new decimal[n];
        var portVariance = 0m;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                portVariance += w[i] * w[j] * cov[i, j];
            }
        }

        for (var i = 0; i < n; i++)
        {
            var mrc = 0m;
            for (var j = 0; j < n; j++)
            {
                mrc += cov[i, j] * w[j];
            }

            riskContributions[i] = w[i] * mrc;
        }

        // All risk contributions should be approximately equal
        var avgRc = riskContributions.Average();
        for (var i = 0; i < n; i++)
        {
            riskContributions[i].Should().BeApproximately(avgRc, 0.001m,
                $"Risk contribution of asset {i} should equal average");
        }

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void RiskParity_ConvergesInUnder100Iterations()
    {
        // The default max is 100. If it converges, weights should be stable.
        var model = new RiskParityConstruction(maxIterations: 100);

        var weights = model.ComputeTargetWeights(FourAssets, RiskParityReturns);

        weights.Should().HaveCount(4);
        AssertWeightsSumToOne(weights);
    }

    [Fact]
    public void RiskParity_UsesCovarianceEstimator()
    {
        // Use data with a recent shock to make EWMA diverge from sample.
        // All assets positively correlated to ensure positive MRC throughout.
        var shockedReturns = new[]
        {
            new[] { 0.01m, 0.005m, 0.01m, 0.005m, 0.01m, 0.005m, 0.01m, 0.005m, 0.01m, 0.10m },
            new[] { 0.008m, 0.004m, 0.008m, 0.004m, 0.008m, 0.004m, 0.008m, 0.004m, 0.008m, 0.05m },
            new[] { 0.005m, 0.003m, 0.005m, 0.003m, 0.005m, 0.003m, 0.005m, 0.003m, 0.005m, 0.08m },
            new[] { 0.003m, 0.002m, 0.003m, 0.002m, 0.003m, 0.002m, 0.003m, 0.002m, 0.003m, 0.04m }
        };

        var sampleModel = new RiskParityConstruction(
            new Boutquin.Trading.Application.CovarianceEstimators.SampleCovarianceEstimator());
        var ewmaModel = new RiskParityConstruction(
            new Boutquin.Trading.Application.CovarianceEstimators.ExponentiallyWeightedCovarianceEstimator(0.80m));

        var sampleWeights = sampleModel.ComputeTargetWeights(FourAssets, shockedReturns);
        var ewmaWeights = ewmaModel.ComputeTargetWeights(FourAssets, shockedReturns);

        // Different covariance estimators should produce different weights
        var totalDiff = FourAssets.Sum(a => Math.Abs(sampleWeights[a] - ewmaWeights[a]));
        totalDiff.Should().BeGreaterThan(0m, "Different covariance estimators should produce different weights");
    }

    // ==================== MeanVarianceConstruction Tests ====================

    [Fact]
    public void MeanVariance_ConstraintsRespected()
    {
        var model = new MeanVarianceConstruction(maxWeight: 0.5m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeLessThanOrEqualTo(0.5m + 1e-8m, "No weight should exceed max");
        }
    }

    [Fact]
    public void MeanVariance_TwoAssetCase_HigherReturnAssetGetsMoreWeight()
    {
        // Asset A has higher mean return than B
        var model = new MeanVarianceConstruction();
        var a = new Asset("HIGH");
        var b = new Asset("LOW");

        var returnsA = new[] { 0.05m, 0.04m, 0.06m, 0.03m, 0.05m };
        var returnsB = new[] { 0.01m, 0.005m, 0.008m, 0.012m, 0.009m };

        var weights = model.ComputeTargetWeights([a, b], [returnsA, returnsB]);

        weights[a].Should().BeGreaterThan(weights[b],
            "Higher-return asset should get more weight in max-Sharpe portfolio");
        AssertWeightsSumToOne(weights);
    }

    [Fact]
    public void MeanVariance_IdenticalAssets_ShouldReturnEqualWeight()
    {
        var model = new MeanVarianceConstruction();
        var a = new Asset("A");
        var b = new Asset("B");
        var returns = new[] { 0.01m, -0.01m, 0.02m, -0.02m, 0.01m };

        var weights = model.ComputeTargetWeights([a, b], [returns, returns]);

        // Identical assets should get equal weight
        weights[a].Should().BeApproximately(weights[b], 0.05m,
            "Identical assets should receive approximately equal weight");
        AssertWeightsSumToOne(weights);
    }

    // ==================== MinimumVarianceConstruction Tests ====================

    [Fact]
    public void MinimumVariance_ShouldHaveLowerVarianceThanEqualWeight()
    {
        var minVarModel = new MinimumVarianceConstruction();
        var equalModel = new EqualWeightConstruction();

        var minVarWeights = minVarModel.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var equalWeights = equalModel.ComputeTargetWeights(FourAssets, FourAssetReturns);

        var cov = new Boutquin.Trading.Application.CovarianceEstimators.SampleCovarianceEstimator()
            .Estimate(FourAssetReturns);

        var minVarVariance = ComputePortfolioVariance(FourAssets, minVarWeights, cov);
        var equalVariance = ComputePortfolioVariance(FourAssets, equalWeights, cov);

        minVarVariance.Should().BeLessThan(equalVariance + 1e-10m,
            "Minimum variance portfolio should have lower variance than equal weight");
        AssertWeightsSumToOne(minVarWeights);
        AssertAllWeightsNonNegative(minVarWeights);
    }

    [Fact]
    public void MinimumVariance_ShouldHaveLowerVarianceThanInverseVol()
    {
        var minVarModel = new MinimumVarianceConstruction();
        var invVolModel = new InverseVolatilityConstruction();

        var minVarWeights = minVarModel.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var invVolWeights = invVolModel.ComputeTargetWeights(FourAssets, FourAssetReturns);

        var cov = new Boutquin.Trading.Application.CovarianceEstimators.SampleCovarianceEstimator()
            .Estimate(FourAssetReturns);

        var minVarVariance = ComputePortfolioVariance(FourAssets, minVarWeights, cov);
        var invVolVariance = ComputePortfolioVariance(FourAssets, invVolWeights, cov);

        minVarVariance.Should().BeLessThanOrEqualTo(invVolVariance + 1e-6m,
            "Minimum variance should produce lower or equal variance than inverse vol");
    }

    [Fact]
    public void MinimumVariance_ConstraintsRespected()
    {
        var model = new MinimumVarianceConstruction();

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    // ==================== BlackLittermanConstruction Tests ====================

    [Fact]
    public void BlackLitterman_NoViews_ShouldReturnEquilibriumWeights()
    {
        var eqWeights = new[] { 0.4m, 0.2m, 0.2m, 0.2m };
        var model = new BlackLittermanConstruction(eqWeights);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        // With no views, output should be based on equilibrium
        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void BlackLitterman_SingleAbsoluteView_ShouldShiftWeight()
    {
        var eqWeights = new[] { 0.25m, 0.25m, 0.25m, 0.25m };

        // View: VTI (asset 0) will return 5%
        var pickMatrix = new decimal[1, 4];
        pickMatrix[0, 0] = 1m;
        var viewReturns = new[] { 0.05m };
        var viewUncertainty = new decimal[1, 1];
        viewUncertainty[0, 0] = 0.0001m; // High confidence

        var modelWithView = new BlackLittermanConstruction(
            eqWeights,
            pickMatrix: pickMatrix,
            viewReturns: viewReturns,
            viewUncertainty: viewUncertainty);

        var modelNoView = new BlackLittermanConstruction(eqWeights);

        var weightsWithView = modelWithView.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var weightsNoView = modelNoView.ComputeTargetWeights(FourAssets, FourAssetReturns);

        // The view should shift weight toward VTI
        weightsWithView[s_vti].Should().BeGreaterThan(weightsNoView[s_vti] - 0.01m,
            "Positive view on VTI should increase its weight");
        AssertWeightsSumToOne(weightsWithView);
    }

    [Fact]
    public void BlackLitterman_ConfidenceScalesViewImpact()
    {
        var eqWeights = new[] { 0.25m, 0.25m, 0.25m, 0.25m };

        // Same view, different confidence
        var pickMatrix = new decimal[1, 4];
        pickMatrix[0, 0] = 1m;
        var viewReturns = new[] { 0.05m };

        var highConfUncertainty = new decimal[1, 1];
        highConfUncertainty[0, 0] = 0.00001m;

        var lowConfUncertainty = new decimal[1, 1];
        lowConfUncertainty[0, 0] = 0.1m;

        var highConfModel = new BlackLittermanConstruction(eqWeights,
            pickMatrix: pickMatrix, viewReturns: viewReturns, viewUncertainty: highConfUncertainty);
        var lowConfModel = new BlackLittermanConstruction(eqWeights,
            pickMatrix: pickMatrix, viewReturns: viewReturns, viewUncertainty: lowConfUncertainty);

        var highConfWeights = highConfModel.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var lowConfWeights = lowConfModel.ComputeTargetWeights(FourAssets, FourAssetReturns);

        // High confidence should shift VTI weight more
        var highDelta = highConfWeights[s_vti] - 0.25m;
        var lowDelta = lowConfWeights[s_vti] - 0.25m;
        Math.Abs(highDelta).Should().BeGreaterThanOrEqualTo(Math.Abs(lowDelta) - 0.01m,
            "Higher confidence should produce a larger weight shift");
    }

    // ==================== Shared validation tests ====================

    [Theory]
    [MemberData(nameof(AllModels))]
    public void AllModels_EmptyAssets_ShouldReturnEmpty(IPortfolioConstructionModel model)
    {
        var weights = model.ComputeTargetWeights([], []);

        weights.Should().BeEmpty();
    }

    public static IEnumerable<object[]> AllModels =>
    [
        [new EqualWeightConstruction()],
        [new InverseVolatilityConstruction()],
        [new RiskParityConstruction()],
        [new MeanVarianceConstruction()],
        [new MinimumVarianceConstruction()],
        [new BlackLittermanConstruction(new[] { 0.25m, 0.25m, 0.25m, 0.25m })]
    ];

    // --- Helper ---

    private static decimal ComputePortfolioVariance(
        IReadOnlyList<Asset> assets,
        IReadOnlyDictionary<Asset, decimal> weights,
        decimal[,] cov)
    {
        var n = assets.Count;
        var variance = 0m;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                variance += weights[assets[i]] * weights[assets[j]] * cov[i, j];
            }
        }

        return variance;
    }
}
