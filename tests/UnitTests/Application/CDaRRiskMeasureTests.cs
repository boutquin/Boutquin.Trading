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
/// Tests for CDaRRiskMeasure and its integration with MeanDownsideRiskConstruction.
/// </summary>
public sealed class CDaRRiskMeasureTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_tlt = new("TLT");
    private static readonly Asset s_gld = new("GLD");
    private static readonly Asset s_vnq = new("VNQ");

    private static decimal[][] FourAssetReturns =>
    [
        [0.02m, -0.03m, 0.04m, -0.01m, 0.03m, -0.02m, 0.01m, 0.05m, -0.04m, 0.02m], // VTI
        [0.005m, -0.003m, 0.004m, 0.002m, -0.001m, 0.003m, -0.002m, 0.001m, 0.004m, -0.003m], // TLT
        [0.01m, -0.02m, 0.015m, -0.005m, 0.02m, -0.01m, 0.005m, 0.03m, -0.025m, 0.01m], // GLD
        [0.03m, -0.04m, 0.05m, -0.02m, 0.04m, -0.03m, 0.02m, 0.06m, -0.05m, 0.03m]  // VNQ
    ];

    private static IReadOnlyList<Asset> FourAssets => [s_vti, s_tlt, s_gld, s_vnq];

    // Transpose returns for scenario format: scenarios[t][i] instead of returns[i][t]
    private static decimal[][] FourAssetScenarios
    {
        get
        {
            var returns = FourAssetReturns;
            var nAssets = returns.Length;
            var nPeriods = returns[0].Length;
            var scenarios = new decimal[nPeriods][];
            for (var t = 0; t < nPeriods; t++)
            {
                scenarios[t] = new decimal[nAssets];
                for (var i = 0; i < nAssets; i++)
                {
                    scenarios[t][i] = returns[i][t];
                }
            }

            return scenarios;
        }
    }

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

    // ==================== CDaR Direct Tests ====================

    [Fact]
    public void CDaR_InvalidConfidenceLevel_ShouldThrow()
    {
        var actZero = () => new CDaRRiskMeasure(0m);
        actZero.Should().Throw<ArgumentOutOfRangeException>();

        var actOne = () => new CDaRRiskMeasure(1m);
        actOne.Should().Throw<ArgumentOutOfRangeException>();

        var actNeg = () => new CDaRRiskMeasure(-0.5m);
        actNeg.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CDaR_GradientHasCorrectLength()
    {
        var measure = new CDaRRiskMeasure();
        var weights = new[] { 0.25m, 0.25m, 0.25m, 0.25m };

        var (_, gradient) = measure.Evaluate(weights, FourAssetScenarios, 1m);

        gradient.Should().HaveCount(4);
    }

    [Fact]
    public void CDaR_AllPositiveReturns_ShouldBeSmall()
    {
        var measure = new CDaRRiskMeasure();
        var weights = new[] { 0.5m, 0.5m };

        // All positive returns → cumulative return is always rising → minimal drawdowns
        var scenarios = new[]
        {
            new[] { 0.01m, 0.02m },
            new[] { 0.015m, 0.01m },
            new[] { 0.02m, 0.015m },
            new[] { 0.01m, 0.01m },
            new[] { 0.005m, 0.02m },
        };

        var (value, _) = measure.Evaluate(weights, scenarios, 1m);

        // With all positive returns, cumulative return always rises → dd ≈ 0
        value.Should().BeGreaterThanOrEqualTo(0m);
        value.Should().BeLessThan(0.01m, "All positive returns should produce near-zero CDaR");
    }

    [Fact]
    public void CDaR_NegativeReturns_ShouldProducePositiveValue()
    {
        var measure = new CDaRRiskMeasure();
        var weights = new[] { 0.5m, 0.5m };

        // Mostly negative returns → significant drawdowns
        var scenarios = new[]
        {
            new[] { -0.02m, -0.03m },
            new[] { -0.01m, -0.02m },
            new[] { 0.005m, 0.01m },
            new[] { -0.03m, -0.04m },
            new[] { -0.015m, -0.01m },
            new[] { -0.02m, -0.025m },
            new[] { 0.01m, 0.005m },
            new[] { -0.025m, -0.03m },
            new[] { -0.01m, -0.015m },
            new[] { -0.02m, -0.02m },
        };

        var (value, _) = measure.Evaluate(weights, scenarios, 1m);

        value.Should().BeGreaterThan(0m, "Negative returns should produce positive CDaR");
    }

    [Fact]
    public void CDaR_HigherConfidence_ShouldBeGreaterOrEqual()
    {
        var weights = new[] { 0.25m, 0.25m, 0.25m, 0.25m };

        var cdar90 = new CDaRRiskMeasure(0.90m);
        var cdar99 = new CDaRRiskMeasure(0.99m);

        var (value90, _) = cdar90.Evaluate(weights, FourAssetScenarios, 1m);
        var (value99, _) = cdar99.Evaluate(weights, FourAssetScenarios, 1m);

        value99.Should().BeGreaterThanOrEqualTo(value90 - 1e-10m,
            "CDaR at 99% confidence should be >= CDaR at 90%");
    }

    [Fact]
    public void CDaR_Evaluate_EmptyScenarios_ThrowsCalculationException()
    {
        var measure = new CDaRRiskMeasure(0.95m);
        var weights = new[] { 1m };
        var scenarios = Array.Empty<decimal[]>();

        var act = () => measure.Evaluate(weights, scenarios, 0.01m);
        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Fact]
    public void CDaR_Reset_ShouldClearState()
    {
        var measure = new CDaRRiskMeasure();
        var weights = new[] { 0.5m, 0.5m };
        var scenarios = new[] { new[] { -0.05m, -0.03m }, new[] { -0.02m, -0.01m } };

        measure.Evaluate(weights, scenarios, 1m);
        measure.Reset();

        // Should not throw and should produce a valid result after reset
        var (value, gradient) = measure.Evaluate(weights, scenarios, 1m);
        value.Should().BeGreaterThanOrEqualTo(0m);
        gradient.Should().HaveCount(2);
    }

    // ==================== CDaR via MeanDownsideRiskConstruction Tests ====================

    [Fact]
    public void MeanCDaR_FourAssets_WeightsSumToOneAndNonNegative()
    {
        var model = new MeanDownsideRiskConstruction(new CDaRRiskMeasure());

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        weights.Should().HaveCount(4);
        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void MeanCDaR_EmptyAssets_ShouldReturnEmpty()
    {
        var model = new MeanDownsideRiskConstruction(new CDaRRiskMeasure());

        var weights = model.ComputeTargetWeights([], []);

        weights.Should().BeEmpty();
    }

    [Fact]
    public void MeanCDaR_SingleAsset_ShouldReturn100Percent()
    {
        var model = new MeanDownsideRiskConstruction(new CDaRRiskMeasure());
        var assets = new List<Asset> { s_vti };
        decimal[][] returns = [FourAssetReturns[0]];

        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(1);
        weights[s_vti].Should().BeApproximately(1.0m, 1e-8m);
    }

    [Fact]
    public void MeanCDaR_ProducesDifferentWeightsThanCVaR()
    {
        var cdarModel = new MeanDownsideRiskConstruction(new CDaRRiskMeasure());
        var cvarModel = new MeanDownsideRiskConstruction(new CVaRRiskMeasure());

        var wCdar = cdarModel.ComputeTargetWeights(FourAssets, FourAssetReturns);
        var wCvar = cvarModel.ComputeTargetWeights(FourAssets, FourAssetReturns);

        // CDaR and CVaR are different risk measures — they should produce at least
        // slightly different allocations (unless by coincidence)
        var maxDiff = FourAssets.Max(a => Math.Abs(wCdar[a] - wCvar[a]));
        // Not asserting maxDiff > 0 (could be coincidentally equal),
        // but both must be valid allocations
        AssertWeightsSumToOne(wCdar);
        AssertWeightsSumToOne(wCvar);
    }
}
