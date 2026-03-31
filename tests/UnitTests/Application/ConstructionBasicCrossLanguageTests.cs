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
/// Cross-language verification tests for basic portfolio construction models.
/// Validates C# implementations against Python reference vectors that replicate
/// the exact same algorithms (own-formula, not library).
///
/// Phase 2 of the verification roadmap.
/// Vectors: tests/Verification/vectors/construction_*.json (from generate_construction_basic_vectors.py)
/// </summary>
public sealed class ConstructionBasicCrossLanguageTests : CrossLanguageVerificationBase
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
    // 2A. EqualWeight
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void EqualWeight_NAssets_MatchesPython(int n)
    {
        using var doc = LoadVector("construction_equal_weight");
        var caseKey = $"n{n}";
        var caseData = doc.RootElement.GetProperty("cases").GetProperty(caseKey);
        var expectedWeights = GetWeights(caseData.GetProperty("weights"));

        var assets = MakeAssets(n);
        var model = new EqualWeightConstruction();
        var result = model.ComputeTargetWeights(assets, new decimal[n][]);

        AssertWeightsMatch(result, expectedWeights, assets, PrecisionExact, $"EqualWeight_N{n}");
        AssertWeightsSumToOne(result, $"EqualWeight_N{n}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2B. InverseVolatility
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("three_asset")]
    [InlineData("five_asset")]
    [InlineData("two_asset")]
    public void InverseVolatility_MatchesPython(string caseName)
    {
        using var doc = LoadVector("construction_inverse_volatility");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJaggedArray(caseData.GetProperty("returns"));
        var expectedWeights = GetWeights(caseData.GetProperty("weights"));

        var n = returns.Length;
        var assets = MakeAssets(n);
        var model = new InverseVolatilityConstruction();
        var result = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(result, expectedWeights, assets, PrecisionExact, $"InvVol_{caseName}");
        AssertWeightsSumToOne(result, $"InvVol_{caseName}");
        AssertWeightsNonNegative(result, $"InvVol_{caseName}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2C. MinimumVariance
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MinimumVariance_3Asset_MatchesPython()
    {
        using var doc = LoadVector("construction_minimum_variance");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty("three_asset");
        var returns = GetJaggedArray(caseData.GetProperty("returns"));
        var expectedWeights = GetWeights(caseData.GetProperty("weights"));

        var assets = MakeAssets(3);
        var model = new MinimumVarianceConstruction();
        var result = model.ComputeTargetWeights(assets, returns);

        // Analytical Cholesky solver — use PrecisionExact
        AssertWeightsMatch(result, expectedWeights, assets, PrecisionExact, "MinVar_3Asset");
        AssertWeightsSumToOne(result, "MinVar_3Asset");
        AssertWeightsNonNegative(result, "MinVar_3Asset");
    }

    [Fact]
    public void MinimumVariance_2Asset_MatchesPython()
    {
        using var doc = LoadVector("construction_minimum_variance");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty("two_asset");
        var returns = GetJaggedArray(caseData.GetProperty("returns"));
        var expectedWeights = GetWeights(caseData.GetProperty("weights"));

        var assets = MakeAssets(2);
        var model = new MinimumVarianceConstruction();
        var result = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(result, expectedWeights, assets, PrecisionExact, "MinVar_2Asset");
        AssertWeightsSumToOne(result, "MinVar_2Asset");
    }

    [Fact]
    public void MinimumVariance_Constrained_MatchesPython()
    {
        using var doc = LoadVector("construction_minimum_variance");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty("three_asset_constrained");
        var returns = GetJaggedArray(caseData.GetProperty("returns"));
        var expectedWeights = GetWeights(caseData.GetProperty("weights"));
        var minW = GetDecimal(caseData, "min_weight");
        var maxW = GetDecimal(caseData, "max_weight");

        var assets = MakeAssets(3);
        var model = new MinimumVarianceConstruction(minWeight: minW, maxWeight: maxW);
        var result = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(result, expectedWeights, assets, PrecisionExact, "MinVar_Constrained");
        AssertWeightsSumToOne(result, "MinVar_Constrained");

        // Verify constraints are satisfied
        foreach (var (_, w) in result)
        {
            Assert.True(w >= minW - PrecisionNumeric,
                $"Weight {w} below min {minW}");
            Assert.True(w <= maxW + PrecisionNumeric,
                $"Weight {w} above max {maxW}");
        }
    }

    [Fact]
    public void MinimumVariance_5Asset_MatchesPython()
    {
        using var doc = LoadVector("construction_minimum_variance");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty("five_asset");
        var returns = GetJaggedArray(caseData.GetProperty("returns"));
        var expectedWeights = GetWeights(caseData.GetProperty("weights"));

        var assets = MakeAssets(5);
        var model = new MinimumVarianceConstruction();
        var result = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(result, expectedWeights, assets, PrecisionExact, "MinVar_5Asset");
        AssertWeightsSumToOne(result, "MinVar_5Asset");
        AssertWeightsNonNegative(result, "MinVar_5Asset");
    }

    [Fact]
    public void MinimumVariance_PortfolioVariance_LessThanEqualWeight()
    {
        using var doc = LoadVector("construction_minimum_variance");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty("three_asset");
        var mvVar = GetDecimal(caseData, "portfolio_variance");
        var ewVar = GetDecimal(caseData, "equal_weight_variance");

        Assert.True(mvVar <= ewVar + PrecisionNumeric,
            $"MinVar portfolio variance ({mvVar}) should be <= EqualWeight ({ewVar})");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2D. MeanVariance
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("three_asset_lambda1", 1.0)]
    [InlineData("three_asset_lambda05", 0.5)]
    [InlineData("three_asset_lambda5", 5.0)]
    public void MeanVariance_3Asset_LambdaVariants_MatchesPython(string caseName, double riskAversion)
    {
        using var doc = LoadVector("construction_mean_variance");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJaggedArray(caseData.GetProperty("returns"));
        var expectedWeights = GetWeights(caseData.GetProperty("weights"));

        var assets = MakeAssets(3);
        var model = new MeanVarianceConstruction(riskAversion: (decimal)riskAversion);
        var result = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(result, expectedWeights, assets, PrecisionExact, $"MeanVar_{caseName}");
        AssertWeightsSumToOne(result, $"MeanVar_{caseName}");
        AssertWeightsNonNegative(result, $"MeanVar_{caseName}");
    }

    [Fact]
    public void MeanVariance_SingleAsset_WeightIsOne()
    {
        using var doc = LoadVector("construction_mean_variance");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty("single_asset");
        var returns = GetJaggedArray(caseData.GetProperty("returns"));
        var expectedWeights = GetWeights(caseData.GetProperty("weights"));

        var assets = MakeAssets(1);
        var model = new MeanVarianceConstruction();
        var result = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(result, expectedWeights, assets, PrecisionExact, "MeanVar_Single");
        AssertWithinTolerance(result[assets[0]], 1m, PrecisionExact, "MeanVar_Single weight: ");
    }

    [Fact]
    public void MeanVariance_Utility_GreaterThanEqualWeight()
    {
        using var doc = LoadVector("construction_mean_variance");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty("three_asset_lambda1");
        var optUtility = GetDecimal(caseData, "utility");
        var ewUtility = GetDecimal(caseData, "equal_weight_utility");

        Assert.True(optUtility >= ewUtility - PrecisionNumeric,
            $"Optimal utility ({optUtility}) should be >= EqualWeight ({ewUtility})");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2E. RiskParity
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("three_asset")]
    [InlineData("two_asset")]
    [InlineData("five_asset")]
    public void RiskParity_MatchesPython(string caseName)
    {
        using var doc = LoadVector("construction_risk_parity");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty(caseName);
        var returns = GetJaggedArray(caseData.GetProperty("returns"));
        var expectedWeights = GetWeights(caseData.GetProperty("weights"));

        var n = returns.Length;
        var assets = MakeAssets(n);
        var model = new RiskParityConstruction();
        var result = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(result, expectedWeights, assets, PrecisionNumeric, $"RiskParity_{caseName}");
        AssertWeightsSumToOne(result, $"RiskParity_{caseName}");
        AssertWeightsNonNegative(result, $"RiskParity_{caseName}");
    }

    [Fact]
    public void RiskParity_3Asset_RiskContributionsApproximatelyEqual()
    {
        using var doc = LoadVector("construction_risk_parity");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty("three_asset");
        var maxDev = GetDecimal(caseData, "risk_contrib_max_deviation");

        // Risk contributions should be approximately equal (max deviation < 1e-6)
        Assert.True(maxDev < PrecisionNumeric,
            $"Risk contribution max deviation ({maxDev}) should be < {PrecisionNumeric}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2F. MaximumDiversification
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MaxDiversification_3Asset_MatchesPython()
    {
        using var doc = LoadVector("construction_max_diversification");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty("three_asset");
        var returns = GetJaggedArray(caseData.GetProperty("returns"));
        var expectedWeights = GetWeights(caseData.GetProperty("weights"));

        var assets = MakeAssets(3);
        var model = new MaximumDiversificationConstruction();
        var result = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(result, expectedWeights, assets, PrecisionExact, "MaxDiv_3Asset");
        AssertWeightsSumToOne(result, "MaxDiv_3Asset");
        AssertWeightsNonNegative(result, "MaxDiv_3Asset");
    }

    [Fact]
    public void MaxDiversification_SingleAsset_WeightIsOne()
    {
        using var doc = LoadVector("construction_max_diversification");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty("single_asset");
        var returns = GetJaggedArray(caseData.GetProperty("returns"));
        var expectedWeights = GetWeights(caseData.GetProperty("weights"));

        var assets = MakeAssets(1);
        var model = new MaximumDiversificationConstruction();
        var result = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(result, expectedWeights, assets, PrecisionExact, "MaxDiv_1Asset");
        AssertWithinTolerance(result[assets[0]], 1m, PrecisionExact, "MaxDiv_1Asset weight: ");
    }

    [Fact]
    public void MaxDiversification_5Asset_MatchesPython()
    {
        using var doc = LoadVector("construction_max_diversification");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty("five_asset");
        var returns = GetJaggedArray(caseData.GetProperty("returns"));
        var expectedWeights = GetWeights(caseData.GetProperty("weights"));

        var assets = MakeAssets(5);
        var model = new MaximumDiversificationConstruction();
        var result = model.ComputeTargetWeights(assets, returns);

        AssertWeightsMatch(result, expectedWeights, assets, PrecisionExact, "MaxDiv_5Asset");
        AssertWeightsSumToOne(result, "MaxDiv_5Asset");
        AssertWeightsNonNegative(result, "MaxDiv_5Asset");
    }

    [Fact]
    public void MaxDiversification_DRatio_GreaterThanEqualWeight()
    {
        using var doc = LoadVector("construction_max_diversification");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty("three_asset");
        var optDR = GetDecimal(caseData, "diversification_ratio");
        var ewDR = GetDecimal(caseData, "equal_weight_dr");

        Assert.True(optDR >= ewDR - PrecisionNumeric,
            $"Optimal DR ({optDR}) should be >= EqualWeight DR ({ewDR})");
    }
}
