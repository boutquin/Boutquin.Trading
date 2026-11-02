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

using System.Text.Json;
using Boutquin.Trading.Application.CovarianceEstimators;
using Boutquin.Trading.Application.DownsideRisk;
using Boutquin.Trading.Application.ExpectedReturns;
using Boutquin.Trading.Application.PortfolioConstruction;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// M-7 fidelity tests for the expected-return estimators and their behavior-preserving integration into
/// <see cref="MeanVarianceConstruction"/> and <see cref="MeanDownsideRiskConstruction"/>:
/// <list type="bullet">
///   <item><description><b>Byte-identity equivalence</b> — the old constructor and the new
///   (estimator-taking) constructor with a <see cref="HistoricalMeanExpectedReturnEstimator"/> produce
///   byte-identical weights on the existing cross-language vectors (SPEC-INV-02).</description></item>
///   <item><description><b>Estimator is consumed</b> — injecting a non-historical estimator changes the
///   models' weights (the wired-not-inert pin).</description></item>
///   <item><description><b>Constant under constant aux</b> — <see cref="YieldAnchoredExpectedReturnEstimator"/>
///   output is constant regardless of realized-return noise (Ilmanen forward-looking property).</description></item>
///   <item><description><b>Missing-input</b> — <see cref="YieldAnchoredExpectedReturnEstimator"/> throws
///   <see cref="Boutquin.Trading.Domain.Exceptions.CalculationException"/> naming <b>every</b> symbol whose
///   required series is absent/too short.</description></item>
///   <item><description><b>Constructor-bound reachability</b> — the estimator constructed with bound
///   per-symbol series is reachable without passing aux to the method.</description></item>
/// </list>
/// </summary>
public sealed class ExpectedReturnEstimatorTests : CrossLanguageVerificationBase
{
    private static IReadOnlyList<Symbol> MakeAssets(int n)
        => Enumerable.Range(0, n).Select(i => new Symbol($"ASSET{i}")).ToList();

    private static decimal[][] GetJagged(JsonElement element)
        => element.EnumerateArray().Select(GetDecimalArray).ToArray();

    private static decimal[,] GetMatrix(JsonElement element)
    {
        var rows = element.EnumerateArray().ToArray();
        var n = rows.Length;
        var m = rows[0].EnumerateArray().Count();
        var result = new decimal[n, m];
        for (var i = 0; i < n; i++)
        {
            var cols = rows[i].EnumerateArray().ToArray();
            for (var j = 0; j < m; j++)
            {
                result[i, j] = (decimal)cols[j].GetDouble();
            }
        }

        return result;
    }

    /// <summary>
    /// A test estimator returning caller-supplied means, ignoring returns/aux. Used to prove the injected
    /// estimator is actually consumed by the construction model's executed path (anti-inertness).
    /// </summary>
    private sealed class FixedMeansEstimator(decimal[] means) : IExpectedReturnEstimator
    {
        public decimal[] EstimateExpectedReturns(
            IReadOnlyList<Symbol> assets,
            decimal[][] returns,
            IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null)
            => (decimal[])means.Clone();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Byte-identity equivalence (the critical AC): old ctor ≡ new ctor + HistoricalMean, on existing vectors
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("three_asset_lambda1", 1.0)]
    [InlineData("three_asset_lambda05", 0.5)]
    [InlineData("three_asset_lambda5", 5.0)]
    public void MeanVariance_OldCtor_And_NewCtorHistoricalMean_ByteIdentical(string caseName, double riskAversion)
    {
        using var doc = LoadVector("construction_mean_variance");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJagged(caseData.GetProperty("returns"));
        var assets = MakeAssets(3);

        var oldModel = new MeanVarianceConstruction(riskAversion: (decimal)riskAversion);
        var newModel = new MeanVarianceConstruction(
            new HistoricalMeanExpectedReturnEstimator(), riskAversion: (decimal)riskAversion);

        var oldWeights = oldModel.ComputeTargetWeights(assets, returns);
        var newWeights = newModel.ComputeTargetWeights(assets, returns);

        Assert.Equal(oldWeights.Count, newWeights.Count);
        foreach (var asset in assets)
        {
            // Deterministic path => exact byte equality, not within-tolerance.
            Assert.Equal(oldWeights[asset], newWeights[asset]);
        }
    }

    [Theory]
    [InlineData("construction_mean_cvar")]
    [InlineData("construction_mean_sortino")]
    public void MeanDownsideRisk_OldCtor_And_NewCtorHistoricalMean_ByteIdentical(string vectorName)
    {
        using var doc = LoadVector(vectorName);
        var cases = doc.RootElement.GetProperty("cases");
        foreach (var caseProp in cases.EnumerateObject())
        {
            var caseData = caseProp.Value;
            if (!caseData.TryGetProperty("returns", out var returnsEl))
            {
                continue;
            }

            var returns = GetJagged(returnsEl);
            var n = returns.Length;
            if (n == 0)
            {
                continue;
            }

            var assets = MakeAssets(n);
            var riskMeasure = vectorName.Contains("cvar", StringComparison.OrdinalIgnoreCase)
                ? (IDownsideRiskMeasure)new CVaRRiskMeasure()
                : new DownsideDeviationRiskMeasure();

            var oldModel = new MeanDownsideRiskConstruction(riskMeasure);
            var newModel = new MeanDownsideRiskConstruction(
                new HistoricalMeanExpectedReturnEstimator(), riskMeasure);

            var oldWeights = oldModel.ComputeTargetWeights(assets, returns);
            var newWeights = newModel.ComputeTargetWeights(assets, returns);

            Assert.Equal(oldWeights.Count, newWeights.Count);
            foreach (var asset in assets)
            {
                Assert.Equal(oldWeights[asset], newWeights[asset]);
            }
        }
    }

    [Theory]
    [InlineData("single_scenario")]
    [InlineData("two_scenarios")]
    public void RobustMeanVariance_OldCtor_And_NewCtorHistoricalMean_ByteIdentical(string caseName)
    {
        using var doc = LoadVector("construction_robust_mean_variance");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJagged(caseData.GetProperty("returns"));
        var riskAversion = (decimal)caseData.GetProperty("risk_aversion").GetDouble();
        var scenarios = caseData.GetProperty("cov_scenarios").EnumerateArray().Select(GetMatrix).ToArray();
        var assets = MakeAssets(returns.Length);

        var oldModel = new RobustMeanVarianceConstruction(
            new SampleCovarianceEstimator(), riskAversion: riskAversion);
        var newModel = new RobustMeanVarianceConstruction(
            new HistoricalMeanExpectedReturnEstimator(),
            new SampleCovarianceEstimator(), riskAversion: riskAversion);

        // single_scenario exercises the base-interface path (estimator-supplied covariance);
        // two_scenarios exercises the minimax scenario overload directly.
        var oldWeights = scenarios.Length == 1
            ? oldModel.ComputeTargetWeights(assets, returns)
            : oldModel.ComputeTargetWeights(assets, returns, scenarios);
        var newWeights = scenarios.Length == 1
            ? newModel.ComputeTargetWeights(assets, returns)
            : newModel.ComputeTargetWeights(assets, returns, scenarios);

        Assert.Equal(oldWeights.Count, newWeights.Count);
        foreach (var asset in assets)
        {
            // Deterministic path => exact byte equality, not within-tolerance.
            Assert.Equal(oldWeights[asset], newWeights[asset]);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // Estimator is consumed on the executed path (wired-not-inert): a different estimator changes the weights
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MeanVariance_InjectedEstimator_ChangesWeights()
    {
        var assets = MakeAssets(3);
        // Symmetric returns so historical means are ~equal; the injected estimator tilts hard to asset 0.
        var returns = new[]
        {
            new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m },
            new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m },
            new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m },
        };

        var historical = new MeanVarianceConstruction();
        var injected = new MeanVarianceConstruction(new FixedMeansEstimator([0.50m, 0.0m, 0.0m]));

        var wHistorical = historical.ComputeTargetWeights(assets, returns);
        var wInjected = injected.ComputeTargetWeights(assets, returns);

        // If the estimator were inert (returns[i].Average() still used) the two would be identical.
        Assert.True(wInjected[assets[0]] > wHistorical[assets[0]] + 0.05m,
            $"Injected high-mean estimator must overweight ASSET0: historical={wHistorical[assets[0]]}, " +
            $"injected={wInjected[assets[0]]}");
    }

    [Fact]
    public void MeanDownsideRisk_InjectedEstimator_ChangesWeights()
    {
        var assets = MakeAssets(3);
        var returns = new[]
        {
            new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m },
            new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m },
            new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m },
        };

        var historical = new MeanDownsideRiskConstruction(new DownsideDeviationRiskMeasure());
        var injected = new MeanDownsideRiskConstruction(
            new FixedMeansEstimator([0.50m, 0.0m, 0.0m]), new DownsideDeviationRiskMeasure());

        var wHistorical = historical.ComputeTargetWeights(assets, returns);
        var wInjected = injected.ComputeTargetWeights(assets, returns);

        Assert.True(wInjected[assets[0]] > wHistorical[assets[0]] + 0.05m,
            $"Injected high-mean estimator must overweight ASSET0: historical={wHistorical[assets[0]]}, " +
            $"injected={wInjected[assets[0]]}");
    }

    [Fact]
    public void RobustMeanVariance_InjectedEstimator_ChangesWeights()
    {
        var assets = MakeAssets(3);
        // Symmetric returns so historical means are ~equal; the injected estimator tilts hard to asset 0.
        var returns = new[]
        {
            new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m },
            new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m },
            new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m },
        };

        var historical = new RobustMeanVarianceConstruction(new SampleCovarianceEstimator());
        var injected = new RobustMeanVarianceConstruction(
            new FixedMeansEstimator([0.50m, 0.0m, 0.0m]), new SampleCovarianceEstimator());

        var wHistorical = historical.ComputeTargetWeights(assets, returns);
        var wInjected = injected.ComputeTargetWeights(assets, returns);

        // If the estimator were inert (returns[i].Average() still used) the two would be identical.
        Assert.True(wInjected[assets[0]] > wHistorical[assets[0]] + 0.05m,
            $"Injected high-mean estimator must overweight ASSET0: historical={wHistorical[assets[0]]}, " +
            $"injected={wInjected[assets[0]]}");
    }

    [Fact]
    public void RobustMeanVariance_InjectedEstimator_ChangesWeights_ScenarioOverload()
    {
        var assets = MakeAssets(2);
        var returns = new[]
        {
            new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m },
            new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m },
        };
        // Two mildly different diagonal scenarios so the minimax path (not the single-scenario
        // shortcut) carries the injected means into the alternating optimization.
        var scenarioA = new decimal[,] { { 0.0004m, 0m }, { 0m, 0.0004m } };
        var scenarioB = new decimal[,] { { 0.0006m, 0m }, { 0m, 0.0002m } };
        var scenarios = new[] { scenarioA, scenarioB };

        var historical = new RobustMeanVarianceConstruction(new SampleCovarianceEstimator());
        var injected = new RobustMeanVarianceConstruction(
            new FixedMeansEstimator([0.50m, 0.0m]), new SampleCovarianceEstimator());

        var wHistorical = historical.ComputeTargetWeights(assets, returns, scenarios);
        var wInjected = injected.ComputeTargetWeights(assets, returns, scenarios);

        Assert.True(wInjected[assets[0]] > wHistorical[assets[0]] + 0.05m,
            $"Injected high-mean estimator must overweight ASSET0 in the minimax path: " +
            $"historical={wHistorical[assets[0]]}, injected={wInjected[assets[0]]}");
    }

    [Fact]
    public void RobustMeanVariance_NullEstimator_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RobustMeanVarianceConstruction(
            (IExpectedReturnEstimator)null!, new SampleCovarianceEstimator()));
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // YieldAnchored: constant under constant aux (Ilmanen forward-looking — does NOT chase the sample mean)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void YieldAnchored_ConstantAux_ConstantOutput_RegardlessOfReturnNoise()
    {
        var assets = MakeAssets(2);
        var classes = new Dictionary<Symbol, ExpectedReturnAssetClass>
        {
            [assets[0]] = ExpectedReturnAssetClass.Bond,
            [assets[1]] = ExpectedReturnAssetClass.Equity,
        };
        var aux = new Dictionary<Symbol, decimal[]>
        {
            [assets[0]] = [0.038m, 0.040m],           // bond current yield
            [assets[1]] = [0.019m, 0.020m],           // equity income yield
            [new Symbol("REALGROWTH")] = [0.014m, 0.015m],
            [new Symbol("T10YIE")] = [0.024m, 0.025m],
        };
        var estimator = new YieldAnchoredExpectedReturnEstimator(classes);

        // Two wildly different realized-return panels, identical aux.
        var returnsA = new[] { new[] { 0.10m, -0.30m, 0.22m }, new[] { -0.20m, 0.33m, -0.11m } };
        var returnsB = new[] { new[] { -0.90m, 0.90m, -0.50m }, new[] { 0.80m, -0.80m, 0.40m } };

        var erA = estimator.EstimateExpectedReturns(assets, returnsA, aux);
        var erB = estimator.EstimateExpectedReturns(assets, returnsB, aux);

        Assert.Equal(erA[0], erB[0]);
        Assert.Equal(erA[1], erB[1]);
        // And it equals the building-block anchor, not the sample mean of either panel.
        Assert.Equal(0.040m, erA[0]);                 // bond current yield (last obs)
        Assert.Equal(0.020m + 0.015m + 0.025m, erA[1]); // D/P + G_real + inflation
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // YieldAnchored: missing-input throws CalculationException naming EVERY affected symbol (no silent hybrid)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void YieldAnchored_MissingInputs_ThrowsNamingEverySymbol()
    {
        var assets = MakeAssets(4);
        var classes = new Dictionary<Symbol, ExpectedReturnAssetClass>
        {
            [assets[0]] = ExpectedReturnAssetClass.Bond,   // OK (yield present)
            [assets[1]] = ExpectedReturnAssetClass.Bond,   // MISSING per-asset yield
            [assets[2]] = ExpectedReturnAssetClass.Equity, // MISSING market-wide inflation
            // assets[3] absent from the class map entirely -> MISSING (unmapped)
        };
        var aux = new Dictionary<Symbol, decimal[]>
        {
            [assets[0]] = [0.040m],
            [assets[2]] = [0.020m],
            [new Symbol("REALGROWTH")] = [0.015m],
            // T10YIE absent -> equity assets[2] cannot be estimated
        };
        var estimator = new YieldAnchoredExpectedReturnEstimator(classes);
        var returns = Enumerable.Range(0, 4).Select(_ => new[] { 0m, 0m }).ToArray();

        var ex = Assert.Throws<Boutquin.Trading.Domain.Exceptions.CalculationException>(
            () => estimator.EstimateExpectedReturns(assets, returns, aux));

        // Every un-estimable symbol must be named; the estimable one must NOT be falsely flagged.
        Assert.Contains("ASSET1", ex.Message);
        Assert.Contains("ASSET2", ex.Message);
        Assert.Contains("ASSET3", ex.Message);
        Assert.DoesNotContain("ASSET0", ex.Message);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════════
    // YieldAnchored: constructor-bound reachability (no aux passed to the method -> uses bound series)
    // ═══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void YieldAnchored_ConstructorBoundAux_ReachableWithoutMethodAux()
    {
        var assets = MakeAssets(2);
        var classes = new Dictionary<Symbol, ExpectedReturnAssetClass>
        {
            [assets[0]] = ExpectedReturnAssetClass.Bond,
            [assets[1]] = ExpectedReturnAssetClass.Equity,
        };
        var boundAux = new Dictionary<Symbol, decimal[]>
        {
            [assets[0]] = [0.030m, 0.045m],
            [assets[1]] = [0.028m],
            [new Symbol("REALGROWTH")] = [0.012m],
            [new Symbol("T10YIE")] = [0.020m],
        };

        // Constructor-bound aux; the method is called WITHOUT an auxiliarySeries argument.
        var estimator = new YieldAnchoredExpectedReturnEstimator(classes, boundAux);
        var returns = new[] { new[] { 0m, 0m }, new[] { 0m, 0m } };

        var er = estimator.EstimateExpectedReturns(assets, returns);

        Assert.Equal(0.045m, er[0]);                    // bond current yield (bound, last obs)
        Assert.Equal(0.028m + 0.012m + 0.020m, er[1]);  // equity building block (bound)
    }

    [Fact]
    public void YieldAnchored_MethodAux_OverridesBoundAux()
    {
        var assets = MakeAssets(1);
        var classes = new Dictionary<Symbol, ExpectedReturnAssetClass> { [assets[0]] = ExpectedReturnAssetClass.Bond };
        var boundAux = new Dictionary<Symbol, decimal[]> { [assets[0]] = [0.030m] };
        var overrideAux = new Dictionary<Symbol, decimal[]> { [assets[0]] = [0.055m] };

        var estimator = new YieldAnchoredExpectedReturnEstimator(classes, boundAux);
        var returns = new[] { new[] { 0m, 0m } };

        Assert.Equal(0.030m, estimator.EstimateExpectedReturns(assets, returns)[0]);          // bound
        Assert.Equal(0.055m, estimator.EstimateExpectedReturns(assets, returns, overrideAux)[0]); // override wins
    }
}
