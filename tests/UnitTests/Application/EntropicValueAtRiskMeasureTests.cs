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
using Boutquin.Trading.Domain.Interfaces;
using FluentAssertions;

/// <summary>
/// Tests for <see cref="EntropicValueAtRiskMeasure"/> (EVaR) and its integration with
/// <see cref="MeanDownsideRiskConstruction"/>.
/// </summary>
public sealed class EntropicValueAtRiskMeasureTests
{
    private static readonly Symbol s_vti = new("VTI");
    private static readonly Symbol s_tlt = new("TLT");
    private static readonly Symbol s_gld = new("GLD");
    private static readonly Symbol s_vnq = new("VNQ");

    private static decimal[][] FourAssetReturns =>
    [
        [0.02m, -0.03m, 0.04m, -0.01m, 0.03m, -0.02m, 0.01m, 0.05m, -0.04m, 0.02m],
        [0.005m, -0.003m, 0.004m, 0.002m, -0.001m, 0.003m, -0.002m, 0.001m, 0.004m, -0.003m],
        [0.01m, -0.02m, 0.015m, -0.005m, 0.02m, -0.01m, 0.005m, 0.03m, -0.025m, 0.01m],
        [0.03m, -0.04m, 0.05m, -0.02m, 0.04m, -0.03m, 0.02m, 0.06m, -0.05m, 0.03m]
    ];

    private static IReadOnlyList<Symbol> FourAssets => [s_vti, s_tlt, s_gld, s_vnq];

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

    private static void AssertWeightsSumToOne(IReadOnlyDictionary<Symbol, decimal> weights)
        => weights.Values.Sum().Should().BeApproximately(1.0m, 1e-8m, "Weights must sum to 1.0");

    private static void AssertAllWeightsNonNegative(IReadOnlyDictionary<Symbol, decimal> weights)
    {
        foreach (var (asset, weight) in weights)
        {
            weight.Should().BeGreaterThanOrEqualTo(0m, $"Weight for {asset} must be non-negative");
        }
    }

    // ==================== AC-1: implements IDownsideRiskMeasure ====================

    [Fact]
    public void EVaR_ImplementsIDownsideRiskMeasure()
    {
        var measure = new EntropicValueAtRiskMeasure();
        measure.Should().BeAssignableTo<IDownsideRiskMeasure>();
    }

    [Fact]
    public void EVaR_GradientHasCorrectLength()
    {
        var measure = new EntropicValueAtRiskMeasure();
        var weights = new[] { 0.25m, 0.25m, 0.25m, 0.25m };

        var (_, gradient) = measure.Evaluate(weights, FourAssetScenarios, 1m);

        gradient.Should().HaveCount(4);
    }

    // ==================== AC-4: degenerate-input guards ====================

    [Fact]
    public void EVaR_InvalidConfidenceLevel_ShouldThrow()
    {
        var actZero = () => new EntropicValueAtRiskMeasure(0m);
        actZero.Should().Throw<ArgumentOutOfRangeException>();

        var actOne = () => new EntropicValueAtRiskMeasure(1m);
        actOne.Should().Throw<ArgumentOutOfRangeException>();

        var actNeg = () => new EntropicValueAtRiskMeasure(-0.5m);
        actNeg.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EVaR_Evaluate_EmptyScenarios_ThrowsCalculationException()
    {
        var measure = new EntropicValueAtRiskMeasure(0.95m);
        var weights = new[] { 1m };
        var scenarios = Array.Empty<decimal[]>();

        var act = () => measure.Evaluate(weights, scenarios, 0.01m);
        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    [Fact]
    public void EVaR_AllZeroLosses_ShouldBeZero()
    {
        // All-zero returns → all losses are zero → MGF mean is 1 → ln(1)=0 →
        // EVaR = (0 - ln α)/z, minimized as z → ∞ → 0. Must be finite and ≈ 0.
        var measure = new EntropicValueAtRiskMeasure(0.95m);
        var weights = new[] { 0.5m, 0.5m };
        var scenarios = new[]
        {
            new[] { 0m, 0m },
            new[] { 0m, 0m },
            new[] { 0m, 0m },
        };

        var (value, gradient) = measure.Evaluate(weights, scenarios, 0m);

        // EVaR(zero losses) = inf_{z>0} (−ln α)/z, whose infimum is 0 (attained as z→∞).
        // With the finite golden-section bracket the minimizer rides the ceiling ZHi, so the
        // value is the bracket residual −ln α / ZHi ≈ 3e-4 — small and positive, → 0 as the
        // bracket widens. Assert near-zero within that residual band.
        value.Should().BeGreaterThanOrEqualTo(0m);
        value.Should().BeApproximately(0m, 1e-3m, "All-zero losses → EVaR → 0 (within bracket residual)");
        gradient.Should().HaveCount(2);
        gradient.Should().OnlyContain(g => g == 0m, "All-zero returns → zero tilt gradient");
    }

    [Fact]
    public void EVaR_SingleScenario_ShouldReturnThatLoss()
    {
        // A single scenario → MGF mean = exp(z·loss) → ln(mean)/... → EVaR → that loss
        // (minus ln α / z, → 0 as z → ∞). EVaR → the single loss value.
        var measure = new EntropicValueAtRiskMeasure(0.95m);
        var weights = new[] { 1m };
        var scenarios = new[] { new[] { -0.04m } }; // loss = +0.04

        var (value, _) = measure.Evaluate(weights, scenarios, 0m);

        // EVaR(single loss L) = inf_{z>0} [ L + (−ln α)/z ], infimum L (as z→∞). The finite
        // bracket leaves the residual −ln α / ZHi ≈ 3e-4 on top of L. Assert L within it.
        value.Should().BeApproximately(0.04m, 1e-3m, "Single scenario → EVaR → that loss (within bracket residual)");
    }

    [Fact]
    public void EVaR_NoOverflow_OnLargeLosses()
    {
        // Large losses with large z must not overflow exp (log-sum-exp shift guards it).
        var measure = new EntropicValueAtRiskMeasure(0.99m);
        var weights = new[] { 0.5m, 0.5m };
        var scenarios = new[]
        {
            new[] { -0.5m, -0.4m },
            new[] { 0.1m, 0.05m },
            new[] { -0.3m, -0.45m },
            new[] { 0.2m, 0.15m },
        };

        // If the log-sum-exp shift were missing, exp(z·loss) at large z would overflow to
        // double.PositiveInfinity and the decimal cast would throw OverflowException — so a
        // finite decimal result IS the overflow-guard proof (decimal has no Inf/NaN state).
        var (value, gradient) = measure.Evaluate(weights, scenarios, 0m);

        value.Should().BeGreaterThan(0m);
        gradient.Should().HaveCount(2);
    }

    // ==================== EVaR via MeanDownsideRiskConstruction (wired path) ====================

    [Fact]
    public void MeanEVaR_FourAssets_WeightsSumToOneAndNonNegative()
    {
        var model = new MeanDownsideRiskConstruction(new EntropicValueAtRiskMeasure());

        var weights = model.ComputeTargetWeights(FourAssets, FourAssetReturns);

        weights.Should().HaveCount(4);
        AssertWeightsSumToOne(weights);
        AssertAllWeightsNonNegative(weights);
    }

    [Fact]
    public void MeanEVaR_EmptyAssets_ShouldReturnEmpty()
    {
        var model = new MeanDownsideRiskConstruction(new EntropicValueAtRiskMeasure());

        var weights = model.ComputeTargetWeights([], []);

        weights.Should().BeEmpty();
    }

    [Fact]
    public void MeanEVaR_SingleAsset_ShouldReturn100Percent()
    {
        var model = new MeanDownsideRiskConstruction(new EntropicValueAtRiskMeasure());
        var assets = new List<Symbol> { s_vti };
        decimal[][] returns = [FourAssetReturns[0]];

        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(1);
        weights[s_vti].Should().BeApproximately(1.0m, 1e-8m);
    }
}
