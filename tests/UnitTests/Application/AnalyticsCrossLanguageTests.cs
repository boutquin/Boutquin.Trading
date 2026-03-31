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

using Boutquin.Trading.Application.Analytics;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification tests for analytics components.
/// Validates C# implementations against Python reference vectors.
///
/// Phase 4D-4H of the verification roadmap.
/// Vectors: tests/Verification/vectors/analytics_*.json
/// </summary>
public sealed class AnalyticsCrossLanguageTests : CrossLanguageVerificationBase
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

    // ═══════════════════════════════════════════════════════════════════════
    // 4D. BrinsonFachler Attribution
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("four_sector")]
    [InlineData("identical_weights")]
    [InlineData("identical_returns")]
    [InlineData("two_sector")]
    public void BrinsonFachler_MatchesPythonVectors(string caseName)
    {
        using var doc = LoadVector("analytics_brinson_fachler");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var assetNames = testCase.GetProperty("assets").EnumerateArray()
            .Select(e => new Asset(e.GetString()!))
            .ToList();

        var portfolioWeights = new Dictionary<Asset, decimal>();
        var benchmarkWeights = new Dictionary<Asset, decimal>();
        var portfolioReturns = new Dictionary<Asset, decimal>();
        var benchmarkReturns = new Dictionary<Asset, decimal>();

        foreach (var asset in assetNames)
        {
            portfolioWeights[asset] = GetDecimal(testCase.GetProperty("portfolio_weights"), asset.Ticker);
            benchmarkWeights[asset] = GetDecimal(testCase.GetProperty("benchmark_weights"), asset.Ticker);
            portfolioReturns[asset] = GetDecimal(testCase.GetProperty("portfolio_returns"), asset.Ticker);
            benchmarkReturns[asset] = GetDecimal(testCase.GetProperty("benchmark_returns"), asset.Ticker);
        }

        var expectedAlloc = GetDecimal(testCase, "allocation_effect");
        var expectedSel = GetDecimal(testCase, "selection_effect");
        var expectedInter = GetDecimal(testCase, "interaction_effect");
        var expectedTotal = GetDecimal(testCase, "total_active_return");

        var result = BrinsonFachlerAttributor.Attribute(
            assetNames, portfolioWeights, benchmarkWeights, portfolioReturns, benchmarkReturns);

        // BF is pure arithmetic — PrecisionExact
        AssertWithinTolerance(result.AllocationEffect, expectedAlloc, PrecisionExact,
            $"BF {caseName} allocation: ");
        AssertWithinTolerance(result.SelectionEffect, expectedSel, PrecisionExact,
            $"BF {caseName} selection: ");
        AssertWithinTolerance(result.InteractionEffect, expectedInter, PrecisionExact,
            $"BF {caseName} interaction: ");
        AssertWithinTolerance(result.TotalActiveReturn, expectedTotal, PrecisionExact,
            $"BF {caseName} total: ");

        // Verify effects sum to total
        var sum = result.AllocationEffect + result.SelectionEffect + result.InteractionEffect;
        AssertWithinTolerance(sum, result.TotalActiveReturn, PrecisionExact,
            $"BF {caseName} sum check: ");

        // Verify per-asset effects
        var expectedAssetAlloc = testCase.GetProperty("asset_allocation_effects");
        var expectedAssetSel = testCase.GetProperty("asset_selection_effects");
        var expectedAssetInter = testCase.GetProperty("asset_interaction_effects");

        foreach (var asset in assetNames)
        {
            AssertWithinTolerance(
                result.AssetAllocationEffects[asset],
                GetDecimal(expectedAssetAlloc, asset.Ticker),
                PrecisionExact,
                $"BF {caseName} asset_alloc[{asset.Ticker}]: ");
            AssertWithinTolerance(
                result.AssetSelectionEffects[asset],
                GetDecimal(expectedAssetSel, asset.Ticker),
                PrecisionExact,
                $"BF {caseName} asset_sel[{asset.Ticker}]: ");
            AssertWithinTolerance(
                result.AssetInteractionEffects[asset],
                GetDecimal(expectedAssetInter, asset.Ticker),
                PrecisionExact,
                $"BF {caseName} asset_inter[{asset.Ticker}]: ");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4E. Factor Regression
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("three_factor")]
    [InlineData("perfect_linear")]
    [InlineData("single_factor")]
    [InlineData("five_factor")]
    public void FactorRegression_MatchesPythonVectors(string caseName)
    {
        using var doc = LoadVector("analytics_factor_regression");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var portfolioReturns = GetDecimalArray(testCase.GetProperty("portfolio_returns"));
        var factorNames = testCase.GetProperty("factor_names").EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();

        var factorReturns = new decimal[factorNames.Count][];
        for (var i = 0; i < factorNames.Count; i++)
        {
            factorReturns[i] = GetDecimalArray(
                testCase.GetProperty("factor_returns").GetProperty(factorNames[i]));
        }

        var expectedAlpha = GetDecimal(testCase, "alpha");
        var expectedR2 = GetDecimal(testCase, "r_squared");
        var expectedRse = GetDecimal(testCase, "residual_std_error");

        var result = FactorRegressor.Regress(portfolioReturns, factorNames, factorReturns);

        // Gaussian elimination is deterministic — PrecisionExact
        AssertWithinTolerance(result.Alpha, expectedAlpha, PrecisionExact,
            $"FactorReg {caseName} alpha: ");
        AssertWithinTolerance(result.RSquared, expectedR2, PrecisionExact,
            $"FactorReg {caseName} R²: ");
        AssertWithinTolerance(result.ResidualStandardError, expectedRse, PrecisionExact,
            $"FactorReg {caseName} RSE: ");

        // Factor loadings
        var expectedLoadings = testCase.GetProperty("factor_loadings");
        foreach (var name in factorNames)
        {
            AssertWithinTolerance(
                result.FactorLoadings[name],
                GetDecimal(expectedLoadings, name),
                PrecisionExact,
                $"FactorReg {caseName} beta_{name}: ");
        }

        // R² ∈ [0, 1]
        Assert.True(result.RSquared >= 0m && result.RSquared <= 1m,
            $"R² = {result.RSquared} should be in [0, 1]");
    }

    [Fact]
    public void FactorRegression_PerfectLinear_RSquaredIsOne()
    {
        using var doc = LoadVector("analytics_factor_regression");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("perfect_linear");

        var portfolioReturns = GetDecimalArray(testCase.GetProperty("portfolio_returns"));
        var factorNames = testCase.GetProperty("factor_names").EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();

        var factorReturns = new decimal[factorNames.Count][];
        for (var i = 0; i < factorNames.Count; i++)
        {
            factorReturns[i] = GetDecimalArray(
                testCase.GetProperty("factor_returns").GetProperty(factorNames[i]));
        }

        var result = FactorRegressor.Regress(portfolioReturns, factorNames, factorReturns);

        AssertWithinTolerance(result.RSquared, 1m, PrecisionExact,
            "FactorReg perfect linear R²: ");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4F. CorrelationAnalyzer
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("three_asset", 3)]
    [InlineData("two_asset", 2)]
    [InlineData("five_asset", 5)]
    public void CorrelationAnalyzer_MatchesPythonVectors(string caseName, int nAssets)
    {
        using var doc = LoadVector("analytics_correlation");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var returns = GetJaggedArray(testCase.GetProperty("returns"));
        var weights = GetDecimalArray(testCase.GetProperty("weights"));
        var expectedCorr = GetMatrix(testCase.GetProperty("correlation_matrix"));
        var expectedDR = GetDecimal(testCase, "diversification_ratio");

        var assets = MakeAssets(nAssets);
        var result = CorrelationAnalyzer.Analyze(assets, returns, weights);

        // Correlation matrix — sample covariance is exact arithmetic
        for (var i = 0; i < nAssets; i++)
        {
            for (var j = 0; j < nAssets; j++)
            {
                AssertWithinTolerance(
                    result.CorrelationMatrix[i, j],
                    expectedCorr[i, j],
                    PrecisionExact,
                    $"Corr {caseName}[{i},{j}]: ");
            }
        }

        // Diversification ratio
        AssertWithinTolerance(result.DiversificationRatio, expectedDR, PrecisionExact,
            $"DR {caseName}: ");

        // DR ≥ 1
        Assert.True(result.DiversificationRatio >= 1m - PrecisionExact,
            $"DR = {result.DiversificationRatio} should be ≥ 1");

        // Diagonal = 1
        for (var i = 0; i < nAssets; i++)
        {
            AssertWithinTolerance(result.CorrelationMatrix[i, i], 1m, PrecisionExact,
                $"Corr {caseName} diagonal[{i}]: ");
        }

        // |corr| ≤ 1
        for (var i = 0; i < nAssets; i++)
        {
            for (var j = 0; j < nAssets; j++)
            {
                Assert.True(Math.Abs(result.CorrelationMatrix[i, j]) <= 1m + PrecisionExact,
                    $"|corr[{i},{j}]| = {Math.Abs(result.CorrelationMatrix[i, j])} > 1");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4G. Effective Number of Bets
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("identity_3", 3.0)]
    [InlineData("identity_5", 5.0)]
    [InlineData("rank_1", 1.0)]
    public void ENB_AnalyticalCases(string caseName, decimal expectedEnb)
    {
        using var doc = LoadVector("analytics_enb");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var corrMatrix = GetMatrix(testCase.GetProperty("correlation_matrix"));

        var enb = EffectiveNumberOfBets.Compute(corrMatrix);

        // Identity and rank-1 are simple eigenvalue structures — exact
        AssertWithinTolerance(enb, expectedEnb, PrecisionExact,
            $"ENB {caseName}: ");
    }

    [Theory]
    [InlineData("high_corr")]
    [InlineData("low_corr")]
    [InlineData("realistic_4")]
    public void ENB_RealisticCases_MatchesPythonVectors(string caseName)
    {
        using var doc = LoadVector("analytics_enb");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var corrMatrix = GetMatrix(testCase.GetProperty("correlation_matrix"));
        var expectedEnb = GetDecimal(testCase, "enb");

        var enb = EffectiveNumberOfBets.Compute(corrMatrix);

        // C# uses Jacobi eigensolver — PrecisionStatistical
        AssertWithinTolerance(enb, expectedEnb, PrecisionStatistical,
            $"ENB {caseName}: ");

        // ENB ∈ [1, N]
        var n = corrMatrix.GetLength(0);
        Assert.True(enb >= 1m - PrecisionStatistical,
            $"ENB = {enb} should be ≥ 1");
        Assert.True(enb <= n + PrecisionStatistical,
            $"ENB = {enb} should be ≤ {n}");
    }

    [Fact]
    public void ENB_HighCorrelation_LessThan_LowCorrelation()
    {
        using var doc = LoadVector("analytics_enb");
        var root = doc.RootElement;

        var highCorr = GetMatrix(
            root.GetProperty("cases").GetProperty("high_corr").GetProperty("correlation_matrix"));
        var lowCorr = GetMatrix(
            root.GetProperty("cases").GetProperty("low_corr").GetProperty("correlation_matrix"));

        var enbHigh = EffectiveNumberOfBets.Compute(highCorr);
        var enbLow = EffectiveNumberOfBets.Compute(lowCorr);

        Assert.True(enbHigh < enbLow + PrecisionStatistical,
            $"ENB high_corr ({enbHigh}) should be < ENB low_corr ({enbLow})");
    }

    [Fact]
    public void ENB_FromReturns_MatchesPythonVector()
    {
        using var doc = LoadVector("analytics_enb");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("from_returns_5");

        var returns = GetJaggedArray(testCase.GetProperty("returns"));
        var expectedEnb = GetDecimal(testCase, "enb");

        var enb = EffectiveNumberOfBets.ComputeFromReturns(returns);

        AssertWithinTolerance(enb, expectedEnb, PrecisionStatistical,
            "ENB from_returns_5: ");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4H. DrawdownAnalyzer
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void DrawdownAnalyzer_SimpleDip()
    {
        using var doc = LoadVector("analytics_drawdown");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("simple_dip");

        var dates = testCase.GetProperty("dates").EnumerateArray()
            .Select(e => DateOnly.Parse(e.GetString()!))
            .ToList();
        var equity = testCase.GetProperty("equity").EnumerateArray()
            .Select(e => (decimal)e.GetDouble())
            .ToList();
        var expectedPeriods = testCase.GetProperty("periods").EnumerateArray().ToArray();

        var equityCurve = new SortedDictionary<DateOnly, decimal>();
        for (var i = 0; i < dates.Count; i++)
        {
            equityCurve[dates[i]] = equity[i];
        }

        var periods = DrawdownAnalyzer.AnalyzeDrawdownPeriods(equityCurve);

        Assert.Equal(expectedPeriods.Length, periods.Count);
        for (var i = 0; i < expectedPeriods.Length; i++)
        {
            var expected = expectedPeriods[i];
            var actual = periods[i];

            AssertWithinTolerance(actual.Depth, GetDecimal(expected, "depth"), PrecisionExact,
                $"Drawdown simple_dip[{i}] depth: ");
            Assert.Equal(expected.GetProperty("duration_days").GetInt32(), actual.DurationDays);

            if (expected.GetProperty("recovery_days").ValueKind != JsonValueKind.Null)
            {
                Assert.NotNull(actual.RecoveryDays);
                Assert.Equal(expected.GetProperty("recovery_days").GetInt32(), actual.RecoveryDays);
            }
            else
            {
                Assert.Null(actual.RecoveryDays);
            }
        }
    }

    [Fact]
    public void DrawdownAnalyzer_Monotonic_NoDrawdowns()
    {
        using var doc = LoadVector("analytics_drawdown");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("monotonic");

        var dates = testCase.GetProperty("dates").EnumerateArray()
            .Select(e => DateOnly.Parse(e.GetString()!))
            .ToList();
        var equity = testCase.GetProperty("equity").EnumerateArray()
            .Select(e => (decimal)e.GetDouble())
            .ToList();

        var equityCurve = new SortedDictionary<DateOnly, decimal>();
        for (var i = 0; i < dates.Count; i++)
        {
            equityCurve[dates[i]] = equity[i];
        }

        var periods = DrawdownAnalyzer.AnalyzeDrawdownPeriods(equityCurve);

        Assert.Empty(periods);
    }

    [Fact]
    public void DrawdownAnalyzer_OngoingDecline()
    {
        using var doc = LoadVector("analytics_drawdown");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("ongoing_decline");

        var dates = testCase.GetProperty("dates").EnumerateArray()
            .Select(e => DateOnly.Parse(e.GetString()!))
            .ToList();
        var equity = testCase.GetProperty("equity").EnumerateArray()
            .Select(e => (decimal)e.GetDouble())
            .ToList();

        var equityCurve = new SortedDictionary<DateOnly, decimal>();
        for (var i = 0; i < dates.Count; i++)
        {
            equityCurve[dates[i]] = equity[i];
        }

        var periods = DrawdownAnalyzer.AnalyzeDrawdownPeriods(equityCurve);

        Assert.Single(periods);
        Assert.Null(periods[0].RecoveryDate);
        Assert.Null(periods[0].RecoveryDays);
        AssertWithinTolerance(periods[0].Depth, -0.20m, PrecisionExact,
            "Drawdown ongoing depth: ");
    }

    [Fact]
    public void DrawdownAnalyzer_FullYear()
    {
        using var doc = LoadVector("analytics_drawdown");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("full_year");

        var dates = testCase.GetProperty("dates").EnumerateArray()
            .Select(e => DateOnly.Parse(e.GetString()!))
            .ToList();
        var equity = testCase.GetProperty("equity").EnumerateArray()
            .Select(e => (decimal)e.GetDouble())
            .ToList();
        var expectedPeriods = testCase.GetProperty("periods").EnumerateArray().ToArray();

        var equityCurve = new SortedDictionary<DateOnly, decimal>();
        for (var i = 0; i < dates.Count; i++)
        {
            equityCurve[dates[i]] = equity[i];
        }

        var periods = DrawdownAnalyzer.AnalyzeDrawdownPeriods(equityCurve);

        Assert.Equal(expectedPeriods.Length, periods.Count);

        // All depths must be in [-1, 0]
        foreach (var p in periods)
        {
            Assert.True(p.Depth >= -1m && p.Depth <= 0m,
                $"Drawdown depth {p.Depth} should be in [-1, 0]");
            Assert.True(p.DurationDays > 0, "Duration must be positive");
        }
    }
}
