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

using Boutquin.Trading.Application.Configuration;
using Boutquin.Trading.Application.CovarianceEstimators;
using Boutquin.Trading.Domain.Interfaces;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Verifies that <c>CreateCovarianceEstimator</c> honours the declarative
/// <see cref="CovarianceEstimatorParameters"/> hyperparameters and that
/// <see cref="ICovarianceEstimator"/> is resolvable from the DI container (M-2).
/// </summary>
public sealed class CovarianceEstimatorConfigurationTests
{
    private static IServiceProvider BuildServiceProvider(Dictionary<string, string?> overrides)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Backtest:ConstructionModel"] = "MinimumVariance",
            ["CostModel:TransactionCostType"] = "FixedPerTrade",
            ["CostModel:CommissionRate"] = "10",
            ["CostModel:SlippageType"] = "NoSlippage",
            ["RiskManagement:MaxDrawdownPercent"] = "0",
            ["RiskManagement:MaxPositionSizePercent"] = "0",
            ["RiskManagement:MaxSectorExposurePercent"] = "0",
        };

        foreach (var (key, value) in overrides)
        {
            configData[key] = value;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddBoutquinTrading(configuration);

        return services.BuildServiceProvider();
    }

    // ============================================================
    // AC 2-1 — every estimator name resolves to its concrete type
    // ============================================================

    [Theory]
    [InlineData("Sample", typeof(SampleCovarianceEstimator))]
    [InlineData("EWMA", typeof(ExponentiallyWeightedCovarianceEstimator))]
    [InlineData("LedoitWolf", typeof(LedoitWolfShrinkageEstimator))]
    [InlineData("LedoitWolfConstantCorrelation", typeof(LedoitWolfConstantCorrelationEstimator))]
    [InlineData("LedoitWolfSingleFactor", typeof(LedoitWolfSingleFactorEstimator))]
    [InlineData("OAS", typeof(OracleApproximatingShrinkageEstimator))]
    [InlineData("QIS", typeof(QuadraticInverseShrinkageEstimator))]
    [InlineData("Denoised", typeof(DenoisedCovarianceEstimator))]
    [InlineData("DenoisedLW", typeof(DenoisedCovarianceEstimator))]
    [InlineData("Gerber", typeof(GerberCovarianceEstimator))]
    [InlineData("FactorGraphicalLasso", typeof(FactorGraphicalLassoEstimator))]
    [InlineData("DccNl", typeof(DynamicCovarianceAdapter))]
    [InlineData("TracyWidomDenoised", typeof(TracyWidomDenoisedCovarianceEstimator))]
    [InlineData("Detoned", typeof(DetonedCovarianceEstimator))]
    [InlineData("NERCOME", typeof(NercomeCovarianceEstimator))]
    [InlineData("POET", typeof(PoetCovarianceEstimator))]
    [InlineData("DoublySparse", typeof(DoublySparseEstimator))]
    public void CovarianceEstimator_ResolvesToConcreteType(string name, Type expectedType)
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:CovarianceEstimator"] = name,
        });

        sp.GetRequiredService<ICovarianceEstimator>().Should().BeOfType(expectedType);
    }

    [Fact]
    public void CovarianceEstimator_Unknown_Throws()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:CovarianceEstimator"] = "NotAnEstimator",
        });

        var act = sp.GetRequiredService<ICovarianceEstimator>;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ============================================================
    // AC 2-2 — out-of-range hyperparameters flow to the constructor
    // and surface as the estimator's own validation at resolution.
    // ============================================================

    [Theory]
    [InlineData("EWMA", "EwmaLambda", "1.5")]      // lambda must be in (0, 1)
    [InlineData("Gerber", "GerberThreshold", "0")] // c must be > 0
    [InlineData("Detoned", "DetoningAlpha", "1.5")] // detoningAlpha must be in [0, 1]
    public void CovarianceEstimator_InvalidHyperparameter_ThrowsAtResolution(
        string name, string parameter, string value)
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:CovarianceEstimator"] = name,
            [$"Backtest:CovarianceParameters:{parameter}"] = value,
        });

        var act = sp.GetRequiredService<ICovarianceEstimator>;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DccNl_AlphaPlusBetaAtLeastOne_ThrowsAtResolution()
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:CovarianceEstimator"] = "DccNl",
            ["Backtest:CovarianceParameters:DccNlAlpha"] = "0.6",
            ["Backtest:CovarianceParameters:DccNlBeta"] = "0.7",
        });

        var act = sp.GetRequiredService<ICovarianceEstimator>;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ============================================================
    // AC 2-3 — DccNl opt-out + custom (alpha, beta) flow end-to-end.
    // The estimateParameters:false path is deterministic, so the
    // resolved estimator's Estimate() must match a directly-built one.
    // ============================================================

    [Fact]
    public void DccNl_OptOutWithCustomAlphaBeta_MatchesDirectConstruction()
    {
        var panel = SyntheticPanel(n: 5, t: 60, seed: 4242);

        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:CovarianceEstimator"] = "DccNl",
            ["Backtest:CovarianceParameters:DccNlEstimateParameters"] = "false",
            ["Backtest:CovarianceParameters:DccNlAlpha"] = "0.05",
            ["Backtest:CovarianceParameters:DccNlBeta"] = "0.90",
        });

        var resolved = sp.GetRequiredService<ICovarianceEstimator>().Estimate(panel);

        var direct = new DynamicCovarianceAdapter(
            new DccNlCovarianceEstimator(0.05m, 0.90m, estimateParameters: false))
            .Estimate(panel);

        resolved.GetLength(0).Should().Be(5);
        for (var i = 0; i < 5; i++)
        {
            for (var j = 0; j < 5; j++)
            {
                resolved[i, j].Should().BeApproximately(direct[i, j], 1e-18m);
            }
        }
    }

    // ============================================================
    // AC 2-4 — default preservation: no CovarianceParameters section
    // => DccNl still estimates (alpha, beta) by default (a wrapped
    // DynamicCovarianceAdapter producing a 5x5 covariance).
    // ============================================================

    [Fact]
    public void DccNl_NoParametersSection_PreservesEstimatingDefault()
    {
        var panel = SyntheticPanel(n: 5, t: 60, seed: 7);

        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:CovarianceEstimator"] = "DccNl",
        });

        var estimator = sp.GetRequiredService<ICovarianceEstimator>();
        estimator.Should().BeOfType<DynamicCovarianceAdapter>();

        var forecast = estimator.Estimate(panel);
        forecast.GetLength(0).Should().Be(5);
        forecast.GetLength(1).Should().Be(5);
    }

    // ============================================================
    // AC 2-5 — non-validated hyperparameters resolve with a
    // non-default value (positive-path wiring coverage).
    // ============================================================

    [Theory]
    [InlineData("Denoised", "DenoisedApplyLedoitWolfShrinkage", "true", typeof(DenoisedCovarianceEstimator))]
    [InlineData("DoublySparse", "DoublySparseEigenvectorThreshold", "0.2", typeof(DoublySparseEstimator))]
    [InlineData("FactorGraphicalLasso", "FactorGraphicalLassoNumFactors", "2", typeof(FactorGraphicalLassoEstimator))]
    [InlineData("NERCOME", "NercomeSplitFraction", "0.6", typeof(NercomeCovarianceEstimator))]
    [InlineData("POET", "PoetNumFactors", "2", typeof(PoetCovarianceEstimator))]
    [InlineData("POET", "PoetThresholdMultiplier", "0.75", typeof(PoetCovarianceEstimator))]
    public void CovarianceEstimator_NonValidatedHyperparameter_Resolves(
        string name, string parameter, string value, Type expectedType)
    {
        var sp = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Backtest:CovarianceEstimator"] = name,
            [$"Backtest:CovarianceParameters:{parameter}"] = value,
        });

        sp.GetRequiredService<ICovarianceEstimator>().Should().BeOfType(expectedType);
    }

    /// <summary>
    /// Deterministic N-asset, T-observation panel (jagged <c>returns[asset][time]</c>) with a
    /// shared market factor and GARCH-like volatility clustering, so the per-asset GARCH(1,1)
    /// fit underlying DCC-NL is well-posed.
    /// </summary>
    private static decimal[][] SyntheticPanel(int n, int t, int seed)
    {
        var rng = new Random(seed);
        var market = new double[t];
        for (var k = 0; k < t; k++)
        {
            market[k] = 0.008 * (rng.NextDouble() - 0.5);
        }

        var panel = new decimal[n][];
        for (var i = 0; i < n; i++)
        {
            var series = new double[t];
            var vol = 0.01;
            for (var k = 0; k < t; k++)
            {
                vol = 0.0008 + (0.85 * vol) + (0.1 * Math.Abs(series[Math.Max(0, k - 1)]));
                var idio = vol * (rng.NextDouble() - 0.5);
                series[k] = (0.6 * market[k]) + idio;
            }

            panel[i] = series.Select(x => (decimal)x).ToArray();
        }

        return panel;
    }
}
