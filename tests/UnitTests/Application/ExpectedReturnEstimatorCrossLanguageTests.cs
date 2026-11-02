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

using Boutquin.Trading.Application.ExpectedReturns;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification tests for the M-7 expected-return estimators
/// (<see cref="HistoricalMeanExpectedReturnEstimator"/> and <see cref="YieldAnchoredExpectedReturnEstimator"/>).
/// Validates that the C# estimators reproduce the Python own-formula reference vectors (from
/// generate_expected_return_vectors.py) that INDEPENDENTLY re-implement the sample-mean baseline and the
/// Ilmanen (2012) building-block arithmetic — bond current yield, equity income-yield + trend-real-growth +
/// inflation — including the last-observation selection.
///
/// Vectors: tests/Verification/vectors/expected_return_estimators.json.
/// </summary>
public sealed class ExpectedReturnEstimatorCrossLanguageTests : CrossLanguageVerificationBase
{
    private static IReadOnlyList<Symbol> MakeAssets(int n)
        => Enumerable.Range(0, n).Select(i => new Symbol($"ASSET{i}")).ToList();

    private static decimal[][] GetJagged(JsonElement element)
        => element.EnumerateArray().Select(GetDecimalArray).ToArray();

    // ── HistoricalMean: E[r_i] = sample mean of returns[i] ─────────────────────────────────────────────
    [Theory]
    [InlineData("single_asset")]
    [InlineData("three_asset")]
    public void HistoricalMean_MatchesPython(string caseName)
    {
        using var doc = LoadVector("expected_return_estimators");
        var caseData = doc.RootElement.GetProperty("historical_mean").GetProperty("cases").GetProperty(caseName);
        var n = caseData.GetProperty("n_assets").GetInt32();
        var returns = GetJagged(caseData.GetProperty("returns"));
        var expected = GetDecimalArray(caseData.GetProperty("expected"));

        var assets = MakeAssets(n);
        var estimator = new HistoricalMeanExpectedReturnEstimator();
        var result = estimator.EstimateExpectedReturns(assets, returns);

        Assert.Equal(n, result.Length);
        for (var i = 0; i < n; i++)
        {
            AssertWithinTolerance(result[i], expected[i], PrecisionExact, $"HistoricalMean_{caseName}[{i}]: ");
        }
    }

    // ── YieldAnchored: Ilmanen (2012) building-block arithmetic by asset class ──────────────────────────
    [Theory]
    [InlineData("multi_class")]
    [InlineData("bond_pair")]
    [InlineData("equity_pair")]
    public void YieldAnchored_MatchesPython(string caseName)
    {
        using var doc = LoadVector("expected_return_estimators");
        var root = doc.RootElement.GetProperty("yield_anchored");
        var realGrowthId = root.GetProperty("real_growth_id").GetString()!;
        var inflationId = root.GetProperty("inflation_id").GetString()!;
        var caseData = root.GetProperty("cases").GetProperty(caseName);

        var n = caseData.GetProperty("n_assets").GetInt32();
        var assets = MakeAssets(n);

        var classes = new Dictionary<Symbol, ExpectedReturnAssetClass>(n);
        var ci = 0;
        foreach (var classEl in caseData.GetProperty("asset_classes").EnumerateArray())
        {
            classes[assets[ci++]] = Enum.Parse<ExpectedReturnAssetClass>(classEl.GetString()!);
        }

        var aux = new Dictionary<Symbol, decimal[]>();
        foreach (var marketEl in caseData.GetProperty("market_series").EnumerateObject())
        {
            aux[new Symbol(marketEl.Name)] = GetDecimalArray(marketEl.Value);
        }

        var ai = 0;
        foreach (var perAssetEl in caseData.GetProperty("per_asset_series").EnumerateArray())
        {
            if (perAssetEl.ValueKind != JsonValueKind.Null)
            {
                aux[assets[ai]] = GetDecimalArray(perAssetEl);
            }

            ai++;
        }

        var returns = GetJagged(caseData.GetProperty("returns"));
        var expected = GetDecimalArray(caseData.GetProperty("expected"));

        var estimator = new YieldAnchoredExpectedReturnEstimator(
            classes, auxiliarySeries: null, realGrowthSeriesId: realGrowthId, inflationSeriesId: inflationId);
        var result = estimator.EstimateExpectedReturns(assets, returns, aux);

        Assert.Equal(n, result.Length);
        for (var i = 0; i < n; i++)
        {
            AssertWithinTolerance(result[i], expected[i], PrecisionExact, $"YieldAnchored_{caseName}[{i}]: ");
        }
    }
}
