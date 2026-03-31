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
using Boutquin.Trading.Domain.Enums;
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

    [Fact]
    public void RiskParity_MaxWeightConstraint_ShouldCapWeights()
    {
        var model = new RiskParityConstruction(maxWeight: 0.35m);

        var weights = model.ComputeTargetWeights(FourAssets, RiskParityReturns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeLessThanOrEqualTo(0.35m + 1e-8m, "No weight should exceed maxWeight");
        }
    }

    [Fact]
    public void RiskParity_MinWeightConstraint_ShouldFloorWeights()
    {
        var model = new RiskParityConstruction(minWeight: 0.10m);

        var weights = model.ComputeTargetWeights(FourAssets, RiskParityReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-8m, "No weight should be below minWeight");
        }
    }

    [Fact]
    public void RiskParity_MinAndMaxWeight_CombinedConstraints()
    {
        var model = new RiskParityConstruction(minWeight: 0.10m, maxWeight: 0.40m);

        var weights = model.ComputeTargetWeights(FourAssets, RiskParityReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-8m, "No weight should be below minWeight");
            weight.Should().BeLessThanOrEqualTo(0.40m + 1e-8m, "No weight should exceed maxWeight");
        }
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

    [Fact]
    public void MeanVariance_MinWeightConstraint_ShouldFloorWeights()
    {
        var model = new MeanVarianceConstruction(minWeight: 0.10m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-8m, "No weight should be below minWeight");
            weight.Should().BeLessThanOrEqualTo(1.0m + 1e-8m);
        }
    }

    [Fact]
    public void MeanVariance_MinAndMaxWeight_CombinedConstraints()
    {
        var model = new MeanVarianceConstruction(minWeight: 0.10m, maxWeight: 0.40m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-8m, "No weight should be below minWeight");
            weight.Should().BeLessThanOrEqualTo(0.40m + 1e-8m, "No weight should exceed maxWeight");
        }
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

    [Fact]
    public void MinimumVariance_MaxWeightConstraint_ShouldCapWeights()
    {
        var model = new MinimumVarianceConstruction(maxWeight: 0.40m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeLessThanOrEqualTo(0.40m + 1e-8m, "No weight should exceed maxWeight");
        }
    }

    [Fact]
    public void MinimumVariance_MinWeightConstraint_ShouldFloorWeights()
    {
        var model = new MinimumVarianceConstruction(minWeight: 0.10m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-8m, "No weight should be below minWeight");
        }
    }

    [Fact]
    public void MinimumVariance_MinAndMaxWeight_CombinedConstraints()
    {
        var model = new MinimumVarianceConstruction(minWeight: 0.10m, maxWeight: 0.40m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-8m, "No weight should be below minWeight");
            weight.Should().BeLessThanOrEqualTo(0.40m + 1e-8m, "No weight should exceed maxWeight");
        }
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

    [Fact]
    public void BlackLitterman_MinAndMaxWeight_CombinedConstraints()
    {
        var eqWeights = new[] { 0.25m, 0.25m, 0.25m, 0.25m };
        var model = new BlackLittermanConstruction(eqWeights, minWeight: 0.10m, maxWeight: 0.40m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-8m, "No weight should be below minWeight");
            weight.Should().BeLessThanOrEqualTo(0.40m + 1e-8m, "No weight should exceed maxWeight");
        }
    }

    [Fact]
    public void BlackLitterman_MaxWeightConstraint_ShouldCapWeights()
    {
        var eqWeights = new[] { 0.25m, 0.25m, 0.25m, 0.25m };

        // Strong view on VTI to push its weight high
        var pickMatrix = new decimal[1, 4];
        pickMatrix[0, 0] = 1m;
        var viewReturns = new[] { 0.10m };
        var viewUncertainty = new decimal[1, 1];
        viewUncertainty[0, 0] = 0.00001m; // Very high confidence

        var model = new BlackLittermanConstruction(eqWeights,
            pickMatrix: pickMatrix, viewReturns: viewReturns, viewUncertainty: viewUncertainty,
            maxWeight: 0.50m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeLessThanOrEqualTo(0.50m + 1e-8m, "No weight should exceed maxWeight");
        }
    }

    // ==================== MaximumDiversificationConstruction Tests ====================

    [Fact]
    public void MaxDiversification_ShouldMaximizeDiversificationRatio()
    {
        var model = new MaximumDiversificationConstruction();

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        // Compute diversification ratio: DR = Σ(w_i * σ_i) / σ_portfolio
        var cov = new Boutquin.Trading.Application.CovarianceEstimators.SampleCovarianceEstimator()
            .Estimate(FourAssetReturns);

        var mdpDr = ComputeDiversificationRatio(FourAssets, weights, cov);

        // Compare to equal weight
        var equalModel = new EqualWeightConstruction();
        var equalWeights = equalModel.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var equalDr = ComputeDiversificationRatio(FourAssets, equalWeights, cov);

        mdpDr.Should().BeGreaterThanOrEqualTo(equalDr - 1e-6m,
            "MDP should have higher or equal diversification ratio than equal weight");
        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void MaxDiversification_ConstraintsRespected()
    {
        var model = new MaximumDiversificationConstruction();

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
        weights.Should().HaveCount(4);
    }

    [Fact]
    public void MaxDiversification_MaxWeightConstraint_ShouldCapWeights()
    {
        var model = new MaximumDiversificationConstruction(maxWeight: 0.4m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeLessThanOrEqualTo(0.4m + 1e-8m, "No weight should exceed maxWeight");
        }
    }

    [Fact]
    public void MaxDiversification_MaxWeight25Pct_ShouldForceNearEqualWeight()
    {
        // With 4 assets and maxWeight=0.25, every asset must be ~25%
        var model = new MaximumDiversificationConstruction(maxWeight: 0.25m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeApproximately(0.25m, 0.01m,
                "With maxWeight=0.25 and 4 assets, each weight should be ~25%");
        }
    }

    [Fact]
    public void MaxDiversification_DefaultMaxWeight_ShouldNotConstrain()
    {
        // Default maxWeight=1.0 should produce same result as unconstrained
        var unconstrained = new MaximumDiversificationConstruction();
        var constrained = new MaximumDiversificationConstruction(maxWeight: 1.0m);

        var w1 = unconstrained.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var w2 = constrained.ComputeTargetWeights(FourAssets, FourAssetReturns);

        foreach (var asset in FourAssets)
        {
            w1[asset].Should().BeApproximately(w2[asset], 1e-10m,
                $"maxWeight=1.0 should produce identical weights for {asset}");
        }
    }

    [Fact]
    public void MaxDiversification_MinWeightConstraint_ShouldFloorWeights()
    {
        var model = new MaximumDiversificationConstruction(minWeight: 0.10m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-8m, "No weight should be below minWeight");
        }
    }

    [Fact]
    public void MaxDiversification_MinWeight25Pct_ShouldForceNearEqualWeight()
    {
        // With 4 assets and minWeight=0.25, every asset must be ~25%
        var model = new MaximumDiversificationConstruction(minWeight: 0.25m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeApproximately(0.25m, 0.01m,
                "With minWeight=0.25 and 4 assets, each weight should be ~25%");
        }
    }

    [Fact]
    public void MaxDiversification_MinAndMaxWeight_CombinedConstraints()
    {
        var model = new MaximumDiversificationConstruction(minWeight: 0.10m, maxWeight: 0.40m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-8m, "No weight should be below minWeight");
            weight.Should().BeLessThanOrEqualTo(0.40m + 1e-8m, "No weight should exceed maxWeight");
        }
    }

    [Fact]
    public void MaxDiversification_DefaultMinWeight_ShouldNotConstrain()
    {
        var unconstrained = new MaximumDiversificationConstruction();
        var constrained = new MaximumDiversificationConstruction(minWeight: 0m);

        var w1 = unconstrained.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var w2 = constrained.ComputeTargetWeights(FourAssets, FourAssetReturns);

        foreach (var asset in FourAssets)
        {
            w1[asset].Should().BeApproximately(w2[asset], 1e-10m,
                $"minWeight=0 should produce identical weights for {asset}");
        }
    }

    [Fact]
    public void MaxDiversification_HighDiversificationRatio_BetterThanInverseVol()
    {
        // MDP should achieve a diversification ratio >= InverseVol
        // because MDP explicitly maximizes DR, while InvVol only approximates it
        var mdpModel = new MaximumDiversificationConstruction();
        var invVolModel = new InverseVolatilityConstruction();

        var mdpWeights = mdpModel.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var invVolWeights = invVolModel.ComputeTargetWeights(FourAssets, FourAssetReturns);

        var cov = new Boutquin.Trading.Application.CovarianceEstimators.SampleCovarianceEstimator()
            .Estimate(FourAssetReturns);

        var mdpDr = ComputeDiversificationRatio(FourAssets, mdpWeights, cov);
        var invVolDr = ComputeDiversificationRatio(FourAssets, invVolWeights, cov);

        mdpDr.Should().BeGreaterThanOrEqualTo(invVolDr - 1e-6m,
            "MDP should achieve higher or equal diversification ratio than InverseVol");
    }

    [Fact]
    public void MaxDiversification_UsesCovarianceEstimator()
    {
        var shockedReturns = new[]
        {
            new[] { 0.01m, 0.005m, 0.01m, 0.005m, 0.01m, 0.005m, 0.01m, 0.005m, 0.01m, 0.10m },
            new[] { 0.008m, 0.004m, 0.008m, 0.004m, 0.008m, 0.004m, 0.008m, 0.004m, 0.008m, 0.05m },
            new[] { 0.005m, 0.003m, 0.005m, 0.003m, 0.005m, 0.003m, 0.005m, 0.003m, 0.005m, -0.03m },
            new[] { 0.003m, 0.002m, 0.003m, 0.002m, 0.003m, 0.002m, 0.003m, 0.002m, 0.003m, 0.04m }
        };

        var sampleModel = new MaximumDiversificationConstruction(
            new Boutquin.Trading.Application.CovarianceEstimators.SampleCovarianceEstimator());
        var ewmaModel = new MaximumDiversificationConstruction(
            new Boutquin.Trading.Application.CovarianceEstimators.ExponentiallyWeightedCovarianceEstimator(0.80m));

        var sampleWeights = sampleModel.ComputeTargetWeights(FourAssets, shockedReturns);
        var ewmaWeights = ewmaModel.ComputeTargetWeights(FourAssets, shockedReturns);

        var totalDiff = FourAssets.Sum(a => Math.Abs(sampleWeights[a] - ewmaWeights[a]));
        totalDiff.Should().BeGreaterThan(0m, "Different covariance estimators should produce different weights");
    }

    [Fact]
    public void MaxDiversification_SingleAsset_ShouldReturn100Percent()
    {
        var model = new MaximumDiversificationConstruction();
        var assets = new List<Asset> { s_vti };

        var weights = model.ComputeTargetWeights(assets, [FourAssetReturns[0]]);

        weights[s_vti].Should().BeApproximately(1.0m, Precision);
    }

    [Fact]
    public void MaxDiversification_LowCorrelationAsset_ShouldGetMeaningfulWeight()
    {
        // Asset C has low correlation to A and B but higher volatility
        // MDP should give C meaningful weight due to diversification benefit
        var a = new Asset("A");
        var b = new Asset("B");
        var c = new Asset("C");

        var returnsA = new[] { 0.01m, -0.01m, 0.02m, -0.02m, 0.015m, -0.005m, 0.01m, -0.01m, 0.005m, -0.015m };
        var returnsB = new[] { 0.012m, -0.008m, 0.018m, -0.022m, 0.013m, -0.007m, 0.011m, -0.009m, 0.006m, -0.014m };
        // C is uncorrelated: different pattern
        var returnsC = new[] { -0.02m, 0.03m, -0.01m, 0.04m, -0.03m, 0.02m, -0.04m, 0.01m, -0.02m, 0.05m };

        var mdpModel = new MaximumDiversificationConstruction();
        var invVolModel = new InverseVolatilityConstruction();

        var mdpWeights = mdpModel.ComputeTargetWeights([a, b, c], [returnsA, returnsB, returnsC]);
        var invVolWeights = invVolModel.ComputeTargetWeights([a, b, c], [returnsA, returnsB, returnsC]);

        // MDP should give C more weight than InvVol because of diversification benefit
        mdpWeights[c].Should().BeGreaterThan(invVolWeights[c] - 0.05m,
            "MDP should reward uncorrelated asset C more than inverse vol does");
        AssertWeightsSumToOne(mdpWeights);
        AssertAllWeightsNonNegative(mdpWeights);
    }

    // ==================== HierarchicalRiskParityConstruction Tests ====================

    [Fact]
    public void HRP_ConstraintsRespected()
    {
        var model = new HierarchicalRiskParityConstruction();

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
        weights.Should().HaveCount(4);
    }

    [Fact]
    public void HRP_LowerVolAsset_ShouldGetHigherWeight()
    {
        var model = new HierarchicalRiskParityConstruction();

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        // TLT (lowest vol) should generally get more weight than VNQ (highest vol)
        weights[s_tlt].Should().BeGreaterThan(weights[s_vnq],
            "Lower-vol TLT should get more weight than higher-vol VNQ in HRP");
    }

    [Fact]
    public void HRP_SingleAsset_ShouldReturn100Percent()
    {
        var model = new HierarchicalRiskParityConstruction();
        var assets = new List<Asset> { s_vti };

        var weights = model.ComputeTargetWeights(assets, [FourAssetReturns[0]]);

        weights[s_vti].Should().BeApproximately(1.0m, Precision);
    }

    [Fact]
    public void HRP_TwoAssets_ShouldSplitByInverseVariance()
    {
        // With only 2 assets, HRP reduces to inverse-variance allocation
        var model = new HierarchicalRiskParityConstruction();
        var a = new Asset("A");
        var b = new Asset("B");

        var returnsA = new[] { 0.01m, -0.01m, 0.02m, -0.02m, 0.01m };
        var returnsB = new[] { 0.02m, -0.02m, 0.04m, -0.04m, 0.02m }; // 2x vol

        var weights = model.ComputeTargetWeights([a, b], [returnsA, returnsB]);

        // With 2x vol, inverse-variance gives B = 1/4 of A's weight:
        // var(A) = σ², var(B) = 4σ² → w_A/(w_A+w_B) with 1/var weighting = 4/(4+1) = 0.8
        weights[a].Should().BeGreaterThan(weights[b],
            "Lower-variance asset should get higher weight");
        AssertWeightsSumToOne(weights);
    }

    [Fact]
    public void HRP_UsesCovarianceEstimator()
    {
        var shockedReturns = new[]
        {
            new[] { 0.01m, 0.005m, 0.01m, 0.005m, 0.01m, 0.005m, 0.01m, 0.005m, 0.01m, 0.10m },
            new[] { 0.008m, 0.004m, 0.008m, 0.004m, 0.008m, 0.004m, 0.008m, 0.004m, 0.008m, 0.05m },
            new[] { 0.005m, 0.003m, 0.005m, 0.003m, 0.005m, 0.003m, 0.005m, 0.003m, 0.005m, -0.03m },
            new[] { 0.003m, 0.002m, 0.003m, 0.002m, 0.003m, 0.002m, 0.003m, 0.002m, 0.003m, 0.04m }
        };

        var sampleModel = new HierarchicalRiskParityConstruction(
            new Boutquin.Trading.Application.CovarianceEstimators.SampleCovarianceEstimator());
        var ewmaModel = new HierarchicalRiskParityConstruction(
            new Boutquin.Trading.Application.CovarianceEstimators.ExponentiallyWeightedCovarianceEstimator(0.80m));

        var sampleWeights = sampleModel.ComputeTargetWeights(FourAssets, shockedReturns);
        var ewmaWeights = ewmaModel.ComputeTargetWeights(FourAssets, shockedReturns);

        var totalDiff = FourAssets.Sum(a => Math.Abs(sampleWeights[a] - ewmaWeights[a]));
        totalDiff.Should().BeGreaterThan(0m, "Different covariance estimators should produce different weights");
    }

    [Fact]
    public void HRP_ClusteredAssets_ShouldRespectClusterStructure()
    {
        // Create two clusters: (A, B) highly correlated, (C, D) highly correlated
        // but inter-cluster correlation low
        var a = new Asset("A");
        var b = new Asset("B");
        var c = new Asset("C");
        var d = new Asset("D");

        var baseAB = new[] { 0.01m, -0.01m, 0.02m, -0.02m, 0.015m, -0.005m, 0.01m, -0.01m, 0.005m, -0.015m };
        var returnsA = baseAB;
        var returnsB = baseAB.Select(r => r * 1.1m + 0.001m).ToArray(); // highly correlated with A

        var baseCD = new[] { -0.015m, 0.02m, -0.005m, 0.025m, -0.01m, 0.015m, -0.02m, 0.01m, -0.005m, 0.02m };
        var returnsC = baseCD;
        var returnsD = baseCD.Select(r => r * 0.9m - 0.001m).ToArray(); // highly correlated with C

        var model = new HierarchicalRiskParityConstruction();
        var weights = model.ComputeTargetWeights([a, b, c, d], [returnsA, returnsB, returnsC, returnsD]);

        // Each cluster should get roughly half the total weight
        var clusterAB = weights[a] + weights[b];
        var clusterCD = weights[c] + weights[d];

        // The split shouldn't be too extreme — both clusters should get meaningful weight
        clusterAB.Should().BeGreaterThan(0.15m, "Cluster AB should get meaningful weight");
        clusterCD.Should().BeGreaterThan(0.15m, "Cluster CD should get meaningful weight");

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void HRP_NeverInvertsMatrix_ShouldNotThrowOnSingularCovariance()
    {
        // Create nearly-singular scenario: assets that are near-identical
        // HRP should handle this without matrix inversion issues
        var model = new HierarchicalRiskParityConstruction();
        var a = new Asset("A");
        var b = new Asset("B");
        var c = new Asset("C");

        var returns1 = new[] { 0.01m, -0.01m, 0.02m, -0.02m, 0.01m };
        var returns2 = new[] { 0.01m, -0.01m, 0.02m, -0.02m, 0.0100001m }; // nearly identical to 1
        var returns3 = new[] { 0.01m, -0.01m, 0.02m, -0.02m, 0.0100002m }; // nearly identical to 1

        var act = () => model.ComputeTargetWeights([a, b, c], [returns1, returns2, returns3]);

        // Should NOT throw — HRP doesn't invert the covariance matrix
        var weights = act.Should().NotThrow().Subject;
        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void HRP_MaxWeightConstraint_ShouldCapWeights()
    {
        var model = new HierarchicalRiskParityConstruction(maxWeight: 0.35m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeLessThanOrEqualTo(0.35m + 1e-8m, "No weight should exceed maxWeight");
        }
    }

    [Fact]
    public void HRP_MinWeightConstraint_ShouldFloorWeights()
    {
        var model = new HierarchicalRiskParityConstruction(minWeight: 0.10m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-8m, "No weight should be below minWeight");
        }
    }

    [Fact]
    public void HRP_MinAndMaxWeight_CombinedConstraints()
    {
        var model = new HierarchicalRiskParityConstruction(minWeight: 0.10m, maxWeight: 0.40m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-8m, "No weight should be below minWeight");
            weight.Should().BeLessThanOrEqualTo(0.40m + 1e-8m, "No weight should exceed maxWeight");
        }
    }

    // ==================== ReturnTiltedHrpConstruction Tests ====================

    [Fact]
    public void ReturnTiltedHRP_Kappa0_ShouldMatchPureHRP()
    {
        var hrp = new HierarchicalRiskParityConstruction();
        var tiltedHrp = new ReturnTiltedHrpConstruction(kappa: 0m);

        var hrpWeights = hrp.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var tiltedWeights = tiltedHrp.ComputeTargetWeights(FourAssets, FourAssetReturns);

        foreach (var asset in FourAssets)
        {
            tiltedWeights[asset].Should().BeApproximately(hrpWeights[asset], Precision,
                $"kappa=0 should recover pure HRP weights for {asset.Ticker}");
        }
    }

    [Fact]
    public void ReturnTiltedHRP_Kappa1_ShouldTiltTowardHigherReturns()
    {
        // Create assets with clearly different mean returns but similar volatility
        var high = new Asset("HIGH");
        var low = new Asset("LOW");

        var returnsHigh = new[] { 0.02m, 0.01m, 0.03m, 0.015m, 0.025m };
        var returnsLow = new[] { 0.001m, -0.001m, 0.002m, 0.0m, 0.001m };

        var model = new ReturnTiltedHrpConstruction(kappa: 1.0m);
        var weights = model.ComputeTargetWeights([high, low], [returnsHigh, returnsLow]);

        weights[high].Should().BeGreaterThan(weights[low],
            "With kappa=1, asset with higher mean return should get more weight");
        AssertWeightsSumToOne(weights);
    }

    [Fact]
    public void ReturnTiltedHRP_ConstraintsRespected()
    {
        var model = new ReturnTiltedHrpConstruction();

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
        weights.Should().HaveCount(4);
    }

    [Fact]
    public void ReturnTiltedHRP_SingleAsset_ShouldReturn100Percent()
    {
        var model = new ReturnTiltedHrpConstruction();
        var assets = new List<Asset> { s_vti };

        var weights = model.ComputeTargetWeights(assets, [FourAssetReturns[0]]);

        weights[s_vti].Should().BeApproximately(1.0m, Precision);
    }

    [Fact]
    public void ReturnTiltedHRP_AllNegativeReturns_ShouldStillProduceValidWeights()
    {
        var a = new Asset("A");
        var b = new Asset("B");

        var returnsA = new[] { -0.02m, -0.01m, -0.03m, -0.015m, -0.025m };
        var returnsB = new[] { -0.04m, -0.02m, -0.06m, -0.03m, -0.05m };

        var model = new ReturnTiltedHrpConstruction(kappa: 0.5m);
        var weights = model.ComputeTargetWeights([a, b], [returnsA, returnsB]);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void ReturnTiltedHRP_InvalidKappa_ShouldThrow()
    {
        var actNeg = () => new ReturnTiltedHrpConstruction(kappa: -0.1m);
        actNeg.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("kappa");

        var actOver = () => new ReturnTiltedHrpConstruction(kappa: 1.1m);
        actOver.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("kappa");
    }

    [Fact]
    public void ReturnTiltedHRP_AllNegativeReturns_ReturnTiltStillApplied()
    {
        // With kappa > 0, all-negative returns should still produce different weights
        // than kappa = 0 (pure risk-only). Previously, the condition
        // (returnLeft > 0m || returnRight > 0m) skipped the tilt for all-negative returns.
        var riskOnly = new ReturnTiltedHrpConstruction(kappa: 0m);
        var tilted = new ReturnTiltedHrpConstruction(kappa: 0.8m);

        // 3 assets with varying negative mean returns
        decimal[][] returns =
        [
            [-0.01m, -0.02m, -0.03m, -0.01m, -0.02m, -0.04m, -0.01m, -0.02m, -0.03m, -0.015m], // A: mild loss
            [-0.05m, -0.06m, -0.04m, -0.05m, -0.07m, -0.06m, -0.05m, -0.04m, -0.06m, -0.05m], // B: heavy loss
            [-0.02m, -0.03m, -0.02m, -0.01m, -0.02m, -0.03m, -0.02m, -0.025m, -0.015m, -0.02m], // C: moderate loss
        ];

        IReadOnlyList<Asset> assets = [new("A"), new("B"), new("C")];

        var wRisk = riskOnly.ComputeTargetWeights(assets, returns);
        var wTilted = tilted.ComputeTargetWeights(assets, returns);

        // Both should be valid allocations
        wRisk.Values.Sum().Should().BeApproximately(1.0m, 1e-10m);
        wTilted.Values.Sum().Should().BeApproximately(1.0m, 1e-10m);

        // Weights should differ because the return tilt is active (softmax on negatives)
        var anyDifference = assets.Any(a => Math.Abs(wRisk[a] - wTilted[a]) > 1e-10m);
        anyDifference.Should().BeTrue(
            "With kappa=0.8 and all-negative returns, softmax should still produce a return tilt " +
            "that differs from pure risk-only weights. Previously this was bypassed.");
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
        [new BlackLittermanConstruction(new[] { 0.25m, 0.25m, 0.25m, 0.25m })],
        [new MaximumDiversificationConstruction()],
        [new HierarchicalRiskParityConstruction()],
        [new ReturnTiltedHrpConstruction()]
    ];

    // --- Helpers ---

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

    private static decimal ComputeDiversificationRatio(
        IReadOnlyList<Asset> assets,
        IReadOnlyDictionary<Asset, decimal> weights,
        decimal[,] cov)
    {
        var n = assets.Count;

        // Weighted average of individual volatilities
        var weightedVol = 0m;
        for (var i = 0; i < n; i++)
        {
            var vol = (decimal)Math.Sqrt((double)cov[i, i]);
            weightedVol += weights[assets[i]] * vol;
        }

        // Portfolio volatility
        var portVariance = ComputePortfolioVariance(assets, weights, cov);
        var portVol = (decimal)Math.Sqrt((double)portVariance);

        return portVol > 0m ? weightedVol / portVol : 1m;
    }

    // ==================== Infeasible Weight Constraint Tests ====================
    // When N_assets * maxWeight < 1.0, ProjectOntoSimplex must auto-relax maxWeight
    // to 1/N so weights can sum to 1.0. This happens during dynamic universe ramp-up
    // when only 2 of 5 ETFs are eligible but maxWeight was configured for 5 (e.g., 0.30).

    [Fact]
    public void MDP_TwoAssets_MaxWeight30Pct_ShouldSumToOne()
    {
        var model = new MaximumDiversificationConstruction(
            maxWeight: 0.30m, minWeight: 0.05m);
        var assets = new List<Asset> { s_vti, s_tlt };
        decimal[][] returns = [FourAssetReturns[0], FourAssetReturns[1]];

        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void RiskParity_TwoAssets_MaxWeight25Pct_ShouldSumToOne()
    {
        var model = new RiskParityConstruction(
            maxWeight: 0.25m, minWeight: 0.05m);
        var assets = new List<Asset> { s_vti, s_tlt };
        decimal[][] returns = [FourAssetReturns[0], FourAssetReturns[1]];

        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void HRP_TwoAssets_MaxWeight30Pct_ShouldSumToOne()
    {
        var model = new HierarchicalRiskParityConstruction(
            maxWeight: 0.30m, minWeight: 0.05m);
        var assets = new List<Asset> { s_vti, s_tlt };
        decimal[][] returns = [FourAssetReturns[0], FourAssetReturns[1]];

        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void MinVar_ThreeAssets_MaxWeight30Pct_ShouldSumToOne()
    {
        var model = new MinimumVarianceConstruction(
            maxWeight: 0.30m, minWeight: 0.05m);
        var assets = new List<Asset> { s_vti, s_tlt, s_gld };
        decimal[][] returns = [FourAssetReturns[0], FourAssetReturns[1], FourAssetReturns[2]];

        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    // ==================== WeightConstrainedConstruction Tests ====================

    [Fact]
    public void WeightConstrained_NoConstraints_ShouldPassthrough()
    {
        var inner = new EqualWeightConstruction();
        var model = new WeightConstrainedConstruction(inner);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        weights.Should().HaveCount(4);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeApproximately(0.25m, Precision);
        }

        AssertWeightsSumToOne(weights);
    }

    [Fact]
    public void WeightConstrained_EmptyAssets_ShouldReturnEmpty()
    {
        var inner = new EqualWeightConstruction();
        var model = new WeightConstrainedConstruction(inner);

        var weights = model.ComputeTargetWeights([], []);

        weights.Should().BeEmpty();
    }

    [Fact]
    public void WeightConstrained_CapRespected_ShouldClampAndRenormalize()
    {
        // Inner model gives InverseVol weights (TLT gets ~55% due to low vol).
        // Cap TLT at 30% — remaining weight redistributed.
        var inner = new InverseVolatilityConstruction();
        var caps = new Dictionary<Asset, decimal> { [s_tlt] = 0.30m };
        var model = new WeightConstrainedConstruction(inner, caps: caps);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        weights[s_tlt].Should().BeLessThanOrEqualTo(0.30m + 1e-8m);
        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void WeightConstrained_FloorRespected_ShouldClampAndRenormalize()
    {
        // Inner model gives InverseVol weights (VNQ gets lowest weight ~8%).
        // Floor VNQ at 20%.
        var inner = new InverseVolatilityConstruction();
        var floors = new Dictionary<Asset, decimal> { [s_vnq] = 0.20m };
        var model = new WeightConstrainedConstruction(inner, floors: floors);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        weights[s_vnq].Should().BeGreaterThanOrEqualTo(0.20m - 1e-8m);
        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void WeightConstrained_CombinedFloorAndCap_ShouldRespectBoth()
    {
        var inner = new InverseVolatilityConstruction();
        var floors = new Dictionary<Asset, decimal> { [s_vnq] = 0.15m };
        var caps = new Dictionary<Asset, decimal> { [s_tlt] = 0.35m };
        var model = new WeightConstrainedConstruction(inner, floors: floors, caps: caps);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        weights[s_vnq].Should().BeGreaterThanOrEqualTo(0.15m - 1e-8m);
        weights[s_tlt].Should().BeLessThanOrEqualTo(0.35m + 1e-8m);
        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void WeightConstrained_FloorExceedsCap_ShouldThrow()
    {
        var inner = new EqualWeightConstruction();
        var floors = new Dictionary<Asset, decimal> { [s_vti] = 0.50m };
        var caps = new Dictionary<Asset, decimal> { [s_vti] = 0.30m };

        var act = () => new WeightConstrainedConstruction(inner, floors: floors, caps: caps);

        act.Should().Throw<ArgumentException>().WithMessage("*Floor*exceeds cap*VTI*");
    }

    [Fact]
    public void WeightConstrained_FloorsExceedOne_ShouldThrow()
    {
        var inner = new EqualWeightConstruction();
        var floors = new Dictionary<Asset, decimal>
        {
            [s_vti] = 0.40m,
            [s_tlt] = 0.40m,
            [s_gld] = 0.30m
        };

        var act = () => new WeightConstrainedConstruction(inner, floors: floors);

        act.Should().Throw<ArgumentException>().WithMessage("*Sum of all floors*exceeds 1.0*");
    }

    [Fact]
    public void WeightConstrained_InvalidFloor_ShouldThrow()
    {
        var inner = new EqualWeightConstruction();
        var floors = new Dictionary<Asset, decimal> { [s_vti] = -0.1m };

        var act = () => new WeightConstrainedConstruction(inner, floors: floors);

        act.Should().Throw<ArgumentException>().WithMessage("*Floor*VTI*");
    }

    [Fact]
    public void WeightConstrained_InvalidCap_ShouldThrow()
    {
        var inner = new EqualWeightConstruction();
        var caps = new Dictionary<Asset, decimal> { [s_vti] = 1.5m };

        var act = () => new WeightConstrainedConstruction(inner, caps: caps);

        act.Should().Throw<ArgumentException>().WithMessage("*Cap*VTI*");
    }

    [Fact]
    public void WeightConstrained_AssetWeightConstraintsRecord_ShouldWork()
    {
        var inner = new InverseVolatilityConstruction();
        var constraints = new AssetWeightConstraints(
            Floors: new Dictionary<Asset, decimal> { [s_vnq] = 0.15m },
            Caps: new Dictionary<Asset, decimal> { [s_tlt] = 0.35m });
        var model = new WeightConstrainedConstruction(inner, constraints);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        weights[s_vnq].Should().BeGreaterThanOrEqualTo(0.15m - 1e-8m);
        weights[s_tlt].Should().BeLessThanOrEqualTo(0.35m + 1e-8m);
        AssertWeightsSumToOne(weights);
    }

    // ==================== RegimeWeightConstrainedConstruction Tests ====================

    [Fact]
    public void RegimeConstrained_AppliesCorrectRegimeConstraints()
    {
        var inner = new InverseVolatilityConstruction();

        var regimeConstraints = new Dictionary<EconomicRegime, AssetWeightConstraints>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new(
                Caps: new Dictionary<Asset, decimal> { [s_tlt] = 0.20m }),
            [EconomicRegime.FallingGrowthFallingInflation] = new(
                Floors: new Dictionary<Asset, decimal> { [s_tlt] = 0.40m })
        };

        // Rising growth: TLT capped at 20%
        var risingModel = new RegimeWeightConstrainedConstruction(
            inner, regimeConstraints, EconomicRegime.RisingGrowthRisingInflation);

        var risingWeights = risingModel.ComputeTargetWeights(FourAssets, FourAssetReturns);
        risingWeights[s_tlt].Should().BeLessThanOrEqualTo(0.20m + 1e-8m);
        AssertWeightsSumToOne(risingWeights);

        // Falling growth: TLT floored at 40%
        var fallingModel = new RegimeWeightConstrainedConstruction(
            inner, regimeConstraints, EconomicRegime.FallingGrowthFallingInflation);

        var fallingWeights = fallingModel.ComputeTargetWeights(FourAssets, FourAssetReturns);
        fallingWeights[s_tlt].Should().BeGreaterThanOrEqualTo(0.40m - 1e-8m);
        AssertWeightsSumToOne(fallingWeights);
    }

    [Fact]
    public void RegimeConstrained_UnconstrainedRegime_ShouldPassthrough()
    {
        var inner = new EqualWeightConstruction();

        var regimeConstraints = new Dictionary<EconomicRegime, AssetWeightConstraints>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new() // no floors or caps
        };

        var model = new RegimeWeightConstrainedConstruction(
            inner, regimeConstraints, EconomicRegime.RisingGrowthRisingInflation);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        foreach (var (_, weight) in weights)
        {
            weight.Should().BeApproximately(0.25m, Precision);
        }

        AssertWeightsSumToOne(weights);
    }

    [Fact]
    public void RegimeConstrained_MissingRegime_ShouldThrow()
    {
        var inner = new EqualWeightConstruction();

        var regimeConstraints = new Dictionary<EconomicRegime, AssetWeightConstraints>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new()
        };

        var act = () => new RegimeWeightConstrainedConstruction(
            inner, regimeConstraints, EconomicRegime.FallingGrowthFallingInflation);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*FallingGrowthFallingInflation*");
    }

    [Fact]
    public void RegimeConstrained_EmptyAssets_ShouldReturnEmpty()
    {
        var inner = new EqualWeightConstruction();

        var regimeConstraints = new Dictionary<EconomicRegime, AssetWeightConstraints>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new(
                Caps: new Dictionary<Asset, decimal> { [s_tlt] = 0.30m })
        };

        var model = new RegimeWeightConstrainedConstruction(
            inner, regimeConstraints, EconomicRegime.RisingGrowthRisingInflation);

        var weights = model.ComputeTargetWeights([], []);

        weights.Should().BeEmpty();
    }

    [Fact]
    public void RegimeConstrained_InvalidConstraints_ShouldThrowOnConstruction()
    {
        var inner = new EqualWeightConstruction();

        var regimeConstraints = new Dictionary<EconomicRegime, AssetWeightConstraints>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new(
                Floors: new Dictionary<Asset, decimal> { [s_vti] = 0.60m },
                Caps: new Dictionary<Asset, decimal> { [s_vti] = 0.30m })
        };

        var act = () => new RegimeWeightConstrainedConstruction(
            inner, regimeConstraints, EconomicRegime.RisingGrowthRisingInflation);

        act.Should().Throw<ArgumentException>().WithMessage("*Floor*exceeds cap*VTI*");
    }

    // ==================== HierarchicalEqualRiskContributionConstruction (HERC) Tests ====================

    [Fact]
    public void HERC_FourETFs_ConstraintsRespected()
    {
        var model = new HierarchicalEqualRiskContributionConstruction();

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        weights.Should().HaveCount(4);
        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void HERC_LowerVolAsset_ShouldGetHigherWeight()
    {
        var model = new HierarchicalEqualRiskContributionConstruction();

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        weights[s_tlt].Should().BeGreaterThan(weights[s_vnq],
            "Lower-vol TLT should get more weight than higher-vol VNQ in HERC");
    }

    [Fact]
    public void HERC_SingleAsset_ShouldReturn100Percent()
    {
        var model = new HierarchicalEqualRiskContributionConstruction();
        var assets = new List<Asset> { s_vti };
        decimal[][] returns = [FourAssetReturns[0]];

        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(1);
        weights[s_vti].Should().BeApproximately(1.0m, Precision);
    }

    [Fact]
    public void HERC_EmptyAssets_ShouldReturnEmpty()
    {
        var model = new HierarchicalEqualRiskContributionConstruction();

        var weights = model.ComputeTargetWeights([], []);

        weights.Should().BeEmpty();
    }

    [Fact]
    public void HERC_ProducesDifferentWeightsThanHRP()
    {
        // HERC uses 1/σ (inverse-stddev) vs HRP's 1/σ² (inverse-variance).
        // With assets of different volatilities, these produce different allocations.
        var hrp = new HierarchicalRiskParityConstruction();
        var herc = new HierarchicalEqualRiskContributionConstruction();

        var hrpWeights = hrp.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var hercWeights = herc.ComputeTargetWeights(FourAssets, FourAssetReturns);

        // Both must be valid allocations
        AssertWeightsSumToOne(hrpWeights);
        AssertWeightsSumToOne(hercWeights);

        // At least one asset should differ meaningfully (inverse-σ ≠ inverse-σ²)
        var maxDiff = FourAssets.Max(a => Math.Abs(hrpWeights[a] - hercWeights[a]));
        maxDiff.Should().BeGreaterThan(0.001m,
            "HERC (inverse-stddev) should differ from HRP (inverse-variance)");
    }

    [Fact]
    public void HERC_MaxWeightConstraint_ShouldBeRespected()
    {
        var model = new HierarchicalEqualRiskContributionConstruction(maxWeight: 0.30m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeLessThanOrEqualTo(0.30m + 1e-8m);
        }
    }

    [Fact]
    public void HERC_MinWeightConstraint_ShouldBeRespected()
    {
        var model = new HierarchicalEqualRiskContributionConstruction(minWeight: 0.10m);

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        AssertWeightsSumToOne(weights);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0.10m - 1e-8m);
        }
    }

    [Fact]
    public void HERC_TwoAssets_ShouldSumToOne()
    {
        var model = new HierarchicalEqualRiskContributionConstruction();
        var assets = new List<Asset> { s_vti, s_tlt };
        decimal[][] returns = [FourAssetReturns[0], FourAssetReturns[1]];

        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }
}
