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

using Boutquin.Trading.Application.PortfolioConstruction;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification tests for HERC, DynamicBlackLitterman, and TacticalOverlay.
/// Post-roadmap gap closure with three-layer cross-checks.
/// Vectors: tests/Verification/vectors/construction_herc.json,
///          construction_dynamic_bl.json, construction_tactical_overlay_direct.json
/// </summary>
public sealed class RemainingConstructionCrossLanguageTests : CrossLanguageVerificationBase
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

    private static decimal[] GetWeights(JsonElement element) =>
        element.EnumerateArray()
            .Select(e => (decimal)e.GetDouble())
            .ToArray();

    private static IReadOnlyList<Asset> MakeAssets(int n) =>
        Enumerable.Range(0, n)
            .Select(i => new Asset($"ASSET{i}"))
            .ToList();

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
    // HERC: Hierarchical Equal Risk Contribution
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("five_asset")]
    [InlineData("three_asset")]
    [InlineData("two_asset")]
    [InlineData("two_identical")]
    [InlineData("single_asset")]
    public void HERC_MatchesPythonVectors(string caseName)
    {
        var doc = LoadVector("construction_herc");
        var c = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var expected = GetWeights(c.GetProperty("weights"));
        var n = returns.Length;
        var assets = MakeAssets(n);

        var model = new HierarchicalEqualRiskContributionConstruction();
        var actual = model.ComputeTargetWeights(assets, returns);

        // HERC uses Jacobi eigendecomposition indirectly via clustering —
        // use PrecisionStatistical like HRP
        AssertWeightsMatch(actual, expected, assets, PrecisionStatistical, $"HERC {caseName}");
        AssertWeightsSumToOne(actual, $"HERC {caseName}");
        AssertWeightsNonNegative(actual, $"HERC {caseName}");
    }

    [Fact]
    public void HERC_DiffersFromHRP()
    {
        // HERC uses 1/σ (inverse risk), HRP uses 1/σ² (inverse variance)
        // They should produce different weights on diverse data
        var doc = LoadVector("construction_herc");
        var c = doc.RootElement.GetProperty("cases").GetProperty("five_asset");
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var assets = MakeAssets(returns.Length);

        var herc = new HierarchicalEqualRiskContributionConstruction();
        var hrp = new HierarchicalRiskParityConstruction();

        var hercWeights = herc.ComputeTargetWeights(assets, returns);
        var hrpWeights = hrp.ComputeTargetWeights(assets, returns);

        var maxDiff = assets.Max(a => Math.Abs(hercWeights[a] - hrpWeights[a]));
        Assert.True(maxDiff > 1e-6m, "HERC and HRP should produce different weights");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DynamicBlackLitterman
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void DynamicBL_NoViews_MatchesPythonVectors()
    {
        var doc = LoadVector("construction_dynamic_bl");
        var c = doc.RootElement.GetProperty("cases").GetProperty("no_views");
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var expected = GetWeights(c.GetProperty("weights"));
        var assets = MakeAssets(returns.Length);

        var model = new DynamicBlackLittermanConstruction(
            viewSpecs: Array.Empty<BlackLittermanViewSpec>(),
            riskAversionCoefficient: 2.5m,
            tau: 0.05m);

        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionNumeric, "DynBL no-views");
        AssertWeightsSumToOne(actual, "DynBL no-views");
        AssertWeightsNonNegative(actual, "DynBL no-views");
    }

    [Fact]
    public void DynamicBL_AbsoluteView_MatchesPythonVectors()
    {
        var doc = LoadVector("construction_dynamic_bl");
        var c = doc.RootElement.GetProperty("cases").GetProperty("one_absolute_view");
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var expected = GetWeights(c.GetProperty("weights"));
        var assets = MakeAssets(returns.Length);

        var viewSpecs = new[]
        {
            new BlackLittermanViewSpec(
                BlackLittermanViewType.Absolute,
                Asset: "ASSET0",
                LongAsset: null,
                ShortAsset: null,
                ExpectedReturn: 0.08m,
                Confidence: 0.8m),
        };

        var model = new DynamicBlackLittermanConstruction(
            viewSpecs: viewSpecs,
            riskAversionCoefficient: 2.5m,
            tau: 0.05m);

        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionNumeric, "DynBL abs-view");
        AssertWeightsSumToOne(actual, "DynBL abs-view");
        AssertWeightsNonNegative(actual, "DynBL abs-view");

        // Property: view should tilt ASSET0 upward
        var noViewWeights = GetWeights(doc.RootElement.GetProperty("cases")
            .GetProperty("no_views").GetProperty("weights"));
        Assert.True(actual[assets[0]] > (decimal)noViewWeights[0],
            "Absolute view should increase ASSET0 weight");
    }

    [Fact]
    public void DynamicBL_RelativeView_MatchesPythonVectors()
    {
        var doc = LoadVector("construction_dynamic_bl");
        var c = doc.RootElement.GetProperty("cases").GetProperty("one_relative_view");
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var expected = GetWeights(c.GetProperty("weights"));
        var assets = MakeAssets(returns.Length);

        var viewSpecs = new[]
        {
            new BlackLittermanViewSpec(
                BlackLittermanViewType.Relative,
                Asset: null,
                LongAsset: "ASSET1",
                ShortAsset: "ASSET2",
                ExpectedReturn: 0.03m,
                Confidence: 0.6m),
        };

        var model = new DynamicBlackLittermanConstruction(
            viewSpecs: viewSpecs,
            riskAversionCoefficient: 2.5m,
            tau: 0.05m);

        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionNumeric, "DynBL rel-view");
        AssertWeightsSumToOne(actual, "DynBL rel-view");

        // Property: ASSET1 > ASSET2 due to relative view
        Assert.True(actual[assets[1]] > actual[assets[2]],
            "Relative view: ASSET1 should outweigh ASSET2");
    }

    [Fact]
    public void DynamicBL_HighConfidence_MatchesPythonVectors()
    {
        var doc = LoadVector("construction_dynamic_bl");
        var c = doc.RootElement.GetProperty("cases").GetProperty("high_confidence_view");
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var expected = GetWeights(c.GetProperty("weights"));
        var assets = MakeAssets(returns.Length);

        var viewSpecs = new[]
        {
            new BlackLittermanViewSpec(
                BlackLittermanViewType.Absolute,
                Asset: "ASSET0",
                LongAsset: null,
                ShortAsset: null,
                ExpectedReturn: 0.08m,
                Confidence: 0.99m),
        };

        var model = new DynamicBlackLittermanConstruction(
            viewSpecs: viewSpecs,
            riskAversionCoefficient: 2.5m,
            tau: 0.05m);

        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionNumeric, "DynBL high-conf");
        AssertWeightsSumToOne(actual, "DynBL high-conf");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TacticalOverlay: direct algorithm verification
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("zero_tilts")]
    [InlineData("positive_tilt")]
    [InlineData("momentum_only")]
    [InlineData("tilt_plus_momentum")]
    [InlineData("floor_at_zero")]
    public void TacticalOverlay_MatchesPythonVectors(string caseName)
    {
        var doc = LoadVector("construction_tactical_overlay_direct");
        var c = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var expected = GetWeights(c.GetProperty("weights"));
        var n = returns.Length;
        var assets = MakeAssets(n);

        // Extract base weights for the stub inner model
        var baseWeights = GetWeights(c.GetProperty("base_weights"));

        // Extract tilts
        var tiltsElement = c.GetProperty("tilts");
        var tilts = new Dictionary<Asset, decimal>();
        foreach (var prop in tiltsElement.EnumerateObject())
        {
            var idx = int.Parse(prop.Name);
            tilts[assets[idx]] = (decimal)prop.Value.GetDouble();
        }

        // Extract momentum scores (nullable)
        IReadOnlyDictionary<Asset, decimal>? momentumScores = null;
        if (c.TryGetProperty("momentum_scores", out var msElement) &&
            msElement.ValueKind != JsonValueKind.Null)
        {
            var scores = new Dictionary<Asset, decimal>();
            foreach (var prop in msElement.EnumerateObject())
            {
                var idx = int.Parse(prop.Name);
                scores[assets[idx]] = (decimal)prop.Value.GetDouble();
            }

            momentumScores = scores;
        }

        var momentumStrength = (decimal)c.GetProperty("momentum_strength").GetDouble();

        // Build stub inner model that returns base weights
        var innerWeightsDict = new Dictionary<Asset, decimal>();
        for (var i = 0; i < n; i++)
        {
            innerWeightsDict[assets[i]] = baseWeights[i];
        }

        var inner = new StubConstructionModel(innerWeightsDict);

        // Build regime tilts (single regime — use RisingGrowthRisingInflation)
        var regime = EconomicRegime.RisingGrowthRisingInflation;
        var regimeTilts = new Dictionary<EconomicRegime, IReadOnlyDictionary<Asset, decimal>>
        {
            [regime] = tilts,
        };

        var model = new TacticalOverlayConstruction(
            inner,
            regimeTilts,
            regime,
            momentumScores,
            momentumStrength);

        var actual = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(actual, expected, assets, PrecisionExact, $"Tactical {caseName}");
        AssertWeightsSumToOne(actual, $"Tactical {caseName}");
        AssertWeightsNonNegative(actual, $"Tactical {caseName}");
    }

    // ─── Stub inner model ──────────────────────────────────────────────

    private sealed class StubConstructionModel : IPortfolioConstructionModel
    {
        private readonly IReadOnlyDictionary<Asset, decimal> _weights;

        public StubConstructionModel(IReadOnlyDictionary<Asset, decimal> weights) =>
            _weights = weights;

        public IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
            IReadOnlyList<Asset> assets, decimal[][] returns) =>
            _weights;
    }
}
