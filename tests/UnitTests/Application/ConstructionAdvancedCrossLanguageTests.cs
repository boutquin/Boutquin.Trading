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
using Boutquin.Trading.Application.PortfolioConstruction;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification tests for advanced portfolio construction models.
/// Validates C# implementations against Python reference vectors that replicate
/// the exact same algorithms (own-formula, not library).
///
/// Phase 3 of the verification roadmap.
/// Vectors: tests/Verification/vectors/construction_*.json (from generate_construction_advanced_vectors.py)
/// </summary>
public sealed class ConstructionAdvancedCrossLanguageTests : CrossLanguageVerificationBase
{
    // ─── Helpers ────────────────────────────────────────────────────────

    private static decimal[][] GetJaggedArray(JsonElement element)
    {
        var outer = element.EnumerateArray().ToArray();
        var result = new decimal[outer.Length][];
        for (var i = 0; i < outer.Length; i++)
        {
            result[i] = outer[i].EnumerateArray()
                .Select(e => (decimal)e.GetDouble())
                .ToArray();
        }

        return result;
    }

    private static decimal[] GetWeights(JsonElement element)
    {
        return element.EnumerateArray()
            .Select(e => (decimal)e.GetDouble())
            .ToArray();
    }

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

    private static IReadOnlyList<Asset> MakeAssets(int n)
    {
        return Enumerable.Range(0, n)
            .Select(i => new Asset($"ASSET{i}"))
            .ToList();
    }

    private static void AssertWeightsMatch(
        IReadOnlyDictionary<Asset, decimal> actual,
        decimal[] expected,
        IReadOnlyList<Asset> assets,
        decimal tolerance,
        string label)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            AssertWithinTolerance(actual[assets[i]], expected[i], tolerance,
                $"{label}[{i}]: ");
        }
    }

    private static void AssertWeightsSumToOne(IReadOnlyDictionary<Asset, decimal> weights, string label)
    {
        var sum = weights.Values.Sum();
        AssertWithinTolerance(sum, 1m, PrecisionNumeric,
            $"{label} weights sum: ");
    }

    private static void AssertWeightsNonNegative(IReadOnlyDictionary<Asset, decimal> weights, string label)
    {
        foreach (var (asset, w) in weights)
        {
            Assert.True(w >= -PrecisionNumeric,
                $"{label} weight for {asset.Ticker} = {w} is negative");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3A. HRP
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("five_asset")]
    [InlineData("three_asset")]
    [InlineData("two_asset")]
    public void HRP_MatchesPythonVectors(string caseName)
    {
        var doc = LoadVector("construction_hrp");
        var c = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var expected = GetWeights(c.GetProperty("weights"));
        var n = returns.Length;
        var assets = MakeAssets(n);

        var model = new HierarchicalRiskParityConstruction();
        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionStatistical, $"HRP {caseName}");
        AssertWeightsSumToOne(actual, $"HRP {caseName}");
        AssertWeightsNonNegative(actual, $"HRP {caseName}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3B. Return-Tilted HRP
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("five_asset_kappa0", 0.0)]
    [InlineData("five_asset_kappa05", 0.5)]
    [InlineData("five_asset_kappa1", 1.0)]
    [InlineData("three_asset_kappa05", 0.5)]
    public void ReturnTiltedHRP_MatchesPythonVectors(string caseName, double kappa)
    {
        var doc = LoadVector("construction_return_tilted_hrp");
        var c = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var expected = GetWeights(c.GetProperty("weights"));
        var n = returns.Length;
        var assets = MakeAssets(n);

        var model = new ReturnTiltedHrpConstruction(kappa: (decimal)kappa);
        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionStatistical,
            $"ReturnTiltedHRP {caseName}");
        AssertWeightsSumToOne(actual, $"ReturnTiltedHRP {caseName}");
        AssertWeightsNonNegative(actual, $"ReturnTiltedHRP {caseName}");
    }

    [Fact]
    public void ReturnTiltedHRP_Kappa0_RecoversPureHRP()
    {
        var doc = LoadVector("construction_return_tilted_hrp");
        var c = doc.RootElement.GetProperty("cases").GetProperty("five_asset_kappa0");
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var n = returns.Length;
        var assets = MakeAssets(n);

        var hrpModel = new HierarchicalRiskParityConstruction();
        var tiltedModel = new ReturnTiltedHrpConstruction(kappa: 0m);

        var hrpWeights = hrpModel.ComputeTargetWeights(assets, returns);
        var tiltedWeights = tiltedModel.ComputeTargetWeights(assets, returns);

        for (var i = 0; i < n; i++)
        {
            AssertWithinTolerance(tiltedWeights[assets[i]], hrpWeights[assets[i]],
                PrecisionStatistical, $"kappa=0 vs HRP [{i}]: ");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3C. Black-Litterman
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BlackLitterman_NoViews_MatchesPython()
    {
        var doc = LoadVector("construction_black_litterman");
        var c = doc.RootElement.GetProperty("cases").GetProperty("no_views");
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var eqWeights = GetWeights(c.GetProperty("equilibrium_weights"));
        var expected = GetWeights(c.GetProperty("weights"));
        var riskAversion = (decimal)c.GetProperty("risk_aversion").GetDouble();
        var tau = (decimal)c.GetProperty("tau").GetDouble();
        var n = returns.Length;
        var assets = MakeAssets(n);

        var model = new BlackLittermanConstruction(
            eqWeights, riskAversion, tau);
        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionNumeric, "BL no-views");
        AssertWeightsSumToOne(actual, "BL no-views");
        AssertWeightsNonNegative(actual, "BL no-views");
    }

    [Fact]
    public void BlackLitterman_WithAbsoluteView_MatchesPython()
    {
        var doc = LoadVector("construction_black_litterman");
        var c = doc.RootElement.GetProperty("cases").GetProperty("one_absolute_view");
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var eqWeights = GetWeights(c.GetProperty("equilibrium_weights"));
        var expected = GetWeights(c.GetProperty("weights"));
        var riskAversion = (decimal)c.GetProperty("risk_aversion").GetDouble();
        var tau = (decimal)c.GetProperty("tau").GetDouble();
        var pickMatrix = GetMatrix(c.GetProperty("pick_matrix"));
        var viewReturns = GetWeights(c.GetProperty("view_returns"));
        var viewUncertainty = GetMatrix(c.GetProperty("view_uncertainty"));
        var n = returns.Length;
        var assets = MakeAssets(n);

        var model = new BlackLittermanConstruction(
            eqWeights, riskAversion, tau, pickMatrix, viewReturns, viewUncertainty);
        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionNumeric, "BL absolute-view");
        AssertWeightsSumToOne(actual, "BL absolute-view");
        AssertWeightsNonNegative(actual, "BL absolute-view");
    }

    [Fact]
    public void BlackLitterman_WithRelativeView_MatchesPython()
    {
        var doc = LoadVector("construction_black_litterman");
        var c = doc.RootElement.GetProperty("cases").GetProperty("one_relative_view");
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var eqWeights = GetWeights(c.GetProperty("equilibrium_weights"));
        var expected = GetWeights(c.GetProperty("weights"));
        var riskAversion = (decimal)c.GetProperty("risk_aversion").GetDouble();
        var tau = (decimal)c.GetProperty("tau").GetDouble();
        var pickMatrix = GetMatrix(c.GetProperty("pick_matrix"));
        var viewReturns = GetWeights(c.GetProperty("view_returns"));
        var viewUncertainty = GetMatrix(c.GetProperty("view_uncertainty"));
        var n = returns.Length;
        var assets = MakeAssets(n);

        var model = new BlackLittermanConstruction(
            eqWeights, riskAversion, tau, pickMatrix, viewReturns, viewUncertainty);
        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionNumeric, "BL relative-view");
        AssertWeightsSumToOne(actual, "BL relative-view");
        AssertWeightsNonNegative(actual, "BL relative-view");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3D. Robust Mean-Variance
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void RobustMeanVariance_SingleScenario_MatchesPython()
    {
        var doc = LoadVector("construction_robust_mean_variance");
        var c = doc.RootElement.GetProperty("cases").GetProperty("single_scenario");
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var riskAversion = (decimal)c.GetProperty("risk_aversion").GetDouble();
        var expected = GetWeights(c.GetProperty("weights"));
        var n = returns.Length;
        var assets = MakeAssets(n);

        var model = new RobustMeanVarianceConstruction(
            new SampleCovarianceEstimator(),
            riskAversion: riskAversion);
        // Use single-scenario via base interface
        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionNumeric, "RobustMV single");
        AssertWeightsSumToOne(actual, "RobustMV single");
        AssertWeightsNonNegative(actual, "RobustMV single");
    }

    [Fact]
    public void RobustMeanVariance_TwoScenarios_MatchesPython()
    {
        var doc = LoadVector("construction_robust_mean_variance");
        var c = doc.RootElement.GetProperty("cases").GetProperty("two_scenarios");
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var riskAversion = (decimal)c.GetProperty("risk_aversion").GetDouble();
        var expected = GetWeights(c.GetProperty("weights"));
        var scenarios = c.GetProperty("cov_scenarios").EnumerateArray()
            .Select(GetMatrix).ToArray();
        var n = returns.Length;
        var assets = MakeAssets(n);

        var model = new RobustMeanVarianceConstruction(
            new SampleCovarianceEstimator(),
            riskAversion: riskAversion);
        var actual = model.ComputeTargetWeights(assets, returns, scenarios);

        AssertWeightsMatch(actual, expected, assets, PrecisionNumeric, "RobustMV two-scenario");
        AssertWeightsSumToOne(actual, "RobustMV two-scenario");
        AssertWeightsNonNegative(actual, "RobustMV two-scenario");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3E. MeanDownsideRisk — CVaR
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("three_asset", 0.95, 1.0)]
    [InlineData("three_asset_high_ra", 0.95, 5.0)]
    [InlineData("five_asset", 0.95, 1.0)]
    public void MeanCVaR_MatchesPythonVectors(string caseName, double confidence, double riskAversion)
    {
        var doc = LoadVector("construction_mean_cvar");
        var c = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var expected = GetWeights(c.GetProperty("weights"));
        var n = returns.Length;
        var assets = MakeAssets(n);

        var riskMeasure = new CVaRRiskMeasure((decimal)confidence);
        var model = new MeanDownsideRiskConstruction(
            riskMeasure, riskAversion: (decimal)riskAversion);
        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionNumeric, $"MeanCVaR {caseName}");
        AssertWeightsSumToOne(actual, $"MeanCVaR {caseName}");
        AssertWeightsNonNegative(actual, $"MeanCVaR {caseName}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3F. MeanDownsideRisk — Sortino
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("three_asset", 0.0, 1.0)]
    [InlineData("three_asset_mar", 0.001, 1.0)]
    [InlineData("five_asset", 0.0, 1.0)]
    public void MeanSortino_MatchesPythonVectors(string caseName, double mar, double riskAversion)
    {
        var doc = LoadVector("construction_mean_sortino");
        var c = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var expected = GetWeights(c.GetProperty("weights"));
        var n = returns.Length;
        var assets = MakeAssets(n);

        var riskMeasure = new DownsideDeviationRiskMeasure((decimal)mar);
        var model = new MeanDownsideRiskConstruction(
            riskMeasure, riskAversion: (decimal)riskAversion);
        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionNumeric, $"MeanSortino {caseName}");
        AssertWeightsSumToOne(actual, $"MeanSortino {caseName}");
        AssertWeightsNonNegative(actual, $"MeanSortino {caseName}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3G. Turnover Penalized + Volatility Targeting
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("turnover_lam005", 0.05)]
    [InlineData("turnover_lam0", 0.0)]
    [InlineData("turnover_lam05", 0.5)]
    public void TurnoverPenalized_MatchesPythonVectors(string caseName, double lambda)
    {
        var doc = LoadVector("construction_turnover_voltarget");
        var c = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var innerWeights = GetWeights(c.GetProperty("inner_model_weights"));
        var prevWeights = GetWeights(c.GetProperty("previous_weights"));
        var expected = GetWeights(c.GetProperty("call_2_weights"));
        var n = returns.Length;
        var assets = MakeAssets(n);

        // Create a stub inner model that always returns the known inner weights
        var stubInner = new StubConstructionModel(innerWeights, assets);

        var model = new TurnoverPenalizedConstruction(
            stubInner, lambda: (decimal)lambda);

        // Call 1: first call stores weights (no previous weights → delegates to inner)
        var call1 = model.ComputeTargetWeights(assets, returns);
        // Verify call 1 matches inner model weights
        for (var i = 0; i < n; i++)
        {
            AssertWithinTolerance(call1[assets[i]], innerWeights[i], PrecisionNumeric,
                $"TurnoverPenalized {caseName} call1[{i}]: ");
        }

        // Use a model that returns prevWeights first, then innerWeights
        var sequenceModel = new SequenceConstructionModel(
            new[] { prevWeights, innerWeights }, assets);
        var finalModel = new TurnoverPenalizedConstruction(
            sequenceModel, lambda: (decimal)lambda);

        // Call 1: returns prevWeights (stored as _previousWeights)
        _ = finalModel.ComputeTargetWeights(assets, returns);
        // Call 2: inner returns innerWeights, penalty applied vs prevWeights
        var actual = finalModel.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionNumeric,
            $"TurnoverPenalized {caseName}");
        AssertWeightsSumToOne(actual, $"TurnoverPenalized {caseName}");
        AssertWeightsNonNegative(actual, $"TurnoverPenalized {caseName}");
    }

    [Theory]
    [InlineData("voltarget_no_leverage", 1.0)]
    [InlineData("voltarget_with_leverage", 2.0)]
    public void VolatilityTargeting_MatchesPythonVectors(string caseName, double maxLeverage)
    {
        var doc = LoadVector("construction_turnover_voltarget");
        var c = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var baseWeights = GetWeights(c.GetProperty("base_weights"));
        var targetVol = (decimal)c.GetProperty("target_volatility").GetDouble();
        var expected = GetWeights(c.GetProperty("weights"));
        var n = returns.Length;
        var assets = MakeAssets(n);

        var stubBase = new StubConstructionModel(baseWeights, assets);
        var model = new VolatilityTargetingConstruction(
            stubBase, targetVol, (decimal)maxLeverage);
        var actual = model.ComputeTargetWeights(assets, returns);

        // VolTarget weights may not sum to 1 (leveraged model)
        for (var i = 0; i < n; i++)
        {
            AssertWithinTolerance(actual[assets[i]], expected[i], PrecisionExact,
                $"VolTarget {caseName}[{i}]: ");
        }

        // Verify proportionality to base weights
        if (baseWeights.All(w => w > 0))
        {
            var ratios = assets.Select(a => actual[a] / baseWeights[Array.IndexOf(
                assets.Select(aa => aa).ToArray(), a)]).ToArray();
            var ratio0 = ratios[0];
            for (var i = 1; i < n; i++)
            {
                AssertWithinTolerance(ratios[i], ratio0, PrecisionExact,
                    $"VolTarget {caseName} proportionality [{i}]: ");
            }
        }
    }

    // ─── Stub helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Returns fixed weights regardless of inputs.
    /// </summary>
    private sealed class StubConstructionModel : IPortfolioConstructionModel
    {
        private readonly decimal[] _weights;
        private readonly IReadOnlyList<Asset> _assets;

        public StubConstructionModel(decimal[] weights, IReadOnlyList<Asset> assets)
        {
            _weights = weights;
            _assets = assets;
        }

        public IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
            IReadOnlyList<Asset> assets, decimal[][] returns)
        {
            var result = new Dictionary<Asset, decimal>();
            for (var i = 0; i < _assets.Count; i++)
            {
                result[_assets[i]] = _weights[i];
            }

            return result;
        }
    }

    /// <summary>
    /// Returns a sequence of weight arrays on successive calls.
    /// </summary>
    private sealed class SequenceConstructionModel : IPortfolioConstructionModel
    {
        private readonly decimal[][] _weightSequence;
        private readonly IReadOnlyList<Asset> _assets;
        private int _callIndex;

        public SequenceConstructionModel(decimal[][] weightSequence, IReadOnlyList<Asset> assets)
        {
            _weightSequence = weightSequence;
            _assets = assets;
        }

        public IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
            IReadOnlyList<Asset> assets, decimal[][] returns)
        {
            var weights = _weightSequence[Math.Min(_callIndex, _weightSequence.Length - 1)];
            _callIndex++;
            var result = new Dictionary<Asset, decimal>();
            for (var i = 0; i < _assets.Count; i++)
            {
                result[_assets[i]] = weights[i];
            }

            return result;
        }
    }
}
