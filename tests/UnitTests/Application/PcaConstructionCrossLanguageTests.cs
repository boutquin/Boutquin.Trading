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
using Boutquin.Trading.Application.PortfolioConstruction;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification tests for PCA-based portfolio construction models
/// and the Detoned covariance estimator.
///
/// Validates C# implementations against Python reference vectors that replicate
/// the exact same algorithms.
///
/// Vectors: tests/Verification/vectors/construction_pc_risk_parity.json,
///          construction_pca_constrained.json, covariance_detoned.json
/// Generator: tests/Verification/generate_pca_construction_vectors.py
/// </summary>
public sealed class PcaConstructionCrossLanguageTests : CrossLanguageVerificationBase
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

    private static decimal[,] Get2DArray(JsonElement element)
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

    private static IReadOnlyList<Symbol> MakeAssets(int n)
    {
        return Enumerable.Range(0, n)
            .Select(i => new Symbol($"ASSET{i}"))
            .ToList();
    }

    private static void AssertWeightsMatch(
        IReadOnlyDictionary<Symbol, decimal> actual,
        decimal[] expected,
        IReadOnlyList<Symbol> assets,
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

    private static void AssertWeightsSumToOne(IReadOnlyDictionary<Symbol, decimal> weights, string label)
    {
        var sum = weights.Values.Sum();
        AssertWithinTolerance(sum, 1m, PrecisionNumeric,
            $"{label} weights sum: ");
    }

    private static void AssertWeightsNonNegative(IReadOnlyDictionary<Symbol, decimal> weights, string label)
    {
        foreach (var (asset, w) in weights)
        {
            Assert.True(w >= -PrecisionNumeric,
                $"{label} weight for {asset.Ticker} = {w} is negative");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PC Risk Parity
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("uncorrelated_3asset")]
    [InlineData("perfectly_correlated_3asset")]
    [InlineData("n2_minimum")]
    [InlineData("n5_moderate")]
    [InlineData("n10_large")]
    [InlineData("block_correlated_4asset")]
    [InlineData("q_less_than_1")]
    public void PcRiskParity_MatchesPythonReference(string caseName)
    {
        using var doc = LoadVector("construction_pc_risk_parity");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var returns = GetJaggedArray(testCase.GetProperty("returns"));
        var expectedWeights = GetWeights(testCase.GetProperty("weights"));
        var n = testCase.GetProperty("n").GetInt32();
        var assets = MakeAssets(n);

        var model = new PrincipalComponentRiskParityConstruction();
        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights, caseName);
        AssertWeightsNonNegative(weights, caseName);
        AssertWeightsMatch(weights, expectedWeights, assets, PrecisionStatistical, caseName);
    }

    [Fact]
    public void PcRiskParity_HighThresholdFallback_MatchesPythonReference()
    {
        using var doc = LoadVector("construction_pc_risk_parity");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("high_threshold_fallback");

        var returns = GetJaggedArray(testCase.GetProperty("returns"));
        var expectedWeights = GetWeights(testCase.GetProperty("weights"));
        var n = testCase.GetProperty("n").GetInt32();
        var signalThreshold = (decimal)testCase.GetProperty("signal_threshold").GetDouble();
        var assets = MakeAssets(n);

        var model = new PrincipalComponentRiskParityConstruction(signalThreshold: signalThreshold);
        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights, "high_threshold");
        AssertWeightsNonNegative(weights, "high_threshold");

        // Should be equal weight since all PCs below threshold
        var ew = 1m / n;
        for (var i = 0; i < n; i++)
        {
            AssertWithinTolerance(weights[assets[i]], ew, PrecisionNumeric,
                $"high_threshold[{i}]: ");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PCA-Constrained
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("passthrough_k_equals_n", 3)]
    [InlineData("fallback_k_zero", 0)]
    [InlineData("reduction_equal_weight_inner", 2)]
    [InlineData("single_asset", null)]
    public void PcaConstrained_EqualWeightInner_MatchesPythonReference(string caseName, int? maxComponents)
    {
        using var doc = LoadVector("construction_pca_constrained");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var returns = GetJaggedArray(testCase.GetProperty("returns"));
        var expectedWeights = GetWeights(testCase.GetProperty("weights"));
        var n = testCase.GetProperty("n").GetInt32();
        var assets = MakeAssets(n);

        var inner = new EqualWeightConstruction();
        var model = new PcaConstrainedConstruction(inner, maxComponents: maxComponents);
        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights, caseName);
        AssertWeightsNonNegative(weights, caseName);
        AssertWeightsMatch(weights, expectedWeights, assets, PrecisionStatistical, caseName);
    }

    [Fact]
    public void PcaConstrained_InverseVolInner_MatchesPythonReference()
    {
        using var doc = LoadVector("construction_pca_constrained");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("reduction_inverse_vol_inner");

        var returns = GetJaggedArray(testCase.GetProperty("returns"));
        var expectedWeights = GetWeights(testCase.GetProperty("weights"));
        var n = testCase.GetProperty("n").GetInt32();
        var assets = MakeAssets(n);

        var inner = new InverseVolatilityConstruction();
        var model = new PcaConstrainedConstruction(inner, maxComponents: 2);
        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights, "inv_vol_inner");
        AssertWeightsNonNegative(weights, "inv_vol_inner");
        AssertWeightsMatch(weights, expectedWeights, assets, PrecisionStatistical, "inv_vol_inner");
    }

    [Fact]
    public void PcaConstrained_AutoKFromMp_MatchesPythonReference()
    {
        using var doc = LoadVector("construction_pca_constrained");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("auto_k_from_mp");

        var returns = GetJaggedArray(testCase.GetProperty("returns"));
        var expectedWeights = GetWeights(testCase.GetProperty("weights"));
        var n = testCase.GetProperty("n").GetInt32();
        var assets = MakeAssets(n);

        var inner = new EqualWeightConstruction();
        var model = new PcaConstrainedConstruction(inner);
        var weights = model.ComputeTargetWeights(assets, returns);

        AssertWeightsSumToOne(weights, "auto_k");
        AssertWeightsNonNegative(weights, "auto_k");
        AssertWeightsMatch(weights, expectedWeights, assets, PrecisionStatistical, "auto_k");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Detoned Covariance Estimator
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void DetonedCovariance_ThreeAsset_MatchesPythonReference()
    {
        using var doc = LoadVector("covariance_detoned");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("three_asset");

        var returns = GetJaggedArray(testCase.GetProperty("returns"));
        var expectedAlpha1 = Get2DArray(testCase.GetProperty("detoned_alpha_1"));
        var expectedAlpha05 = Get2DArray(testCase.GetProperty("detoned_alpha_05"));
        var n = returns.Length;

        var estimator1 = new DetonedCovarianceEstimator(detoningAlpha: 1.0m);
        var estimator05 = new DetonedCovarianceEstimator(detoningAlpha: 0.5m);

        var cov1 = estimator1.Estimate(returns);
        var cov05 = estimator05.Estimate(returns);

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                AssertWithinTolerance(cov1[i, j], expectedAlpha1[i, j], PrecisionStatistical,
                    $"detoned_alpha1[{i},{j}]: ");
                AssertWithinTolerance(cov05[i, j], expectedAlpha05[i, j], PrecisionStatistical,
                    $"detoned_alpha05[{i},{j}]: ");
            }
        }
    }

    [Fact]
    public void DetonedCovariance_TwoAsset_DelegatesToSample()
    {
        using var doc = LoadVector("covariance_detoned");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("two_asset_delegates");

        var returns = GetJaggedArray(testCase.GetProperty("returns"));
        var expectedSample = Get2DArray(testCase.GetProperty("sample_covariance"));
        var n = returns.Length;

        var estimator = new DetonedCovarianceEstimator();
        var cov = estimator.Estimate(returns);

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                AssertWithinTolerance(cov[i, j], expectedSample[i, j], PrecisionExact,
                    $"detoned_2asset[{i},{j}]: ");
            }
        }
    }

    [Fact]
    public void DetonedCovariance_FiveAssetFactor_MatchesPythonReference()
    {
        using var doc = LoadVector("covariance_detoned");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("five_asset_factor");

        var returns = GetJaggedArray(testCase.GetProperty("returns"));
        var expectedDetoned = Get2DArray(testCase.GetProperty("detoned_covariance"));
        var n = returns.Length;

        var estimator = new DetonedCovarianceEstimator();
        var cov = estimator.Estimate(returns);

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                AssertWithinTolerance(cov[i, j], expectedDetoned[i, j], PrecisionStatistical,
                    $"detoned_5asset[{i},{j}]: ");
            }
        }
    }
}
