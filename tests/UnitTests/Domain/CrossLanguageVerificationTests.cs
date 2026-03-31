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

namespace Boutquin.Trading.Tests.UnitTests.Domain;

/// <summary>
/// Cross-language verification tests that validate C# financial calculations
/// against golden test vectors produced by independent Python implementations
/// (numpy, scipy, statsmodels, scikit-learn).
///
/// Run generate_vectors.py in tests/Verification/ first to produce the JSON vectors.
/// </summary>
public sealed class CrossLanguageVerificationTests
{
    // Tolerance tiers
    private const decimal PrecisionExact = 1e-10m;
    private const decimal PrecisionNumeric = 1e-6m;
    private const int TradingDaysPerYear = 252;

    /// <summary>
    /// Resolves the vectors directory. Walks up from the test assembly location
    /// until it finds tests/Verification/vectors/.
    /// </summary>
    private static string GetVectorsDir()
    {
        var dir = AppContext.BaseDirectory;
        // Walk up to find the repo root (contains tests/ directory)
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "tests", "Verification", "vectors");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "Cannot find tests/Verification/vectors/ directory. " +
            "Run 'python generate_vectors.py' in tests/Verification/ first.");
    }

    private static JsonDocument LoadVector(string name)
    {
        var path = Path.Combine(GetVectorsDir(), $"{name}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Vector file not found: {path}. Run generate_vectors.py first.",
                path);
        }

        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
    }

    private static decimal[] GetDecimalArray(JsonElement element)
    {
        return element.EnumerateArray()
            .Select(e => (decimal)e.GetDouble())
            .ToArray();
    }

    // ─── Return Metrics ─────────────────────────────────────────────────

    [Fact]
    public void AnnualizedReturn_MatchesPythonVector()
    {
        using var doc = LoadVector("returns");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("annualized_return").GetDouble();

        var result = dr.AnnualizedReturn(TradingDaysPerYear);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void CAGR_MatchesPythonVector()
    {
        using var doc = LoadVector("returns");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("cagr").GetDouble();

        var result = dr.CompoundAnnualGrowthRate(TradingDaysPerYear);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void EquityCurve_MatchesPythonVector()
    {
        using var doc = LoadVector("returns");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = GetDecimalArray(doc.RootElement.GetProperty("expected").GetProperty("equity_curve"));

        var result = dr.EquityCurve(10000m);

        Assert.Equal(expected.Length, result.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.InRange(Math.Abs(result[i] - expected[i]), 0, PrecisionExact);
        }
    }

    [Fact]
    public void DailyReturns_RoundTrip_MatchesPythonVector()
    {
        using var doc = LoadVector("returns");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = GetDecimalArray(doc.RootElement.GetProperty("expected").GetProperty("daily_returns_from_equity"));

        var equity = dr.EquityCurve(10000m);
        var result = equity.DailyReturns();

        Assert.Equal(expected.Length, result.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.InRange(Math.Abs(result[i] - expected[i]), 0, PrecisionExact);
        }
    }

    // ─── Volatility ─────────────────────────────────────────────────────

    [Fact]
    public void Volatility_MatchesPythonVector()
    {
        using var doc = LoadVector("volatility");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("daily_volatility").GetDouble();

        var result = dr.Volatility();

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void AnnualizedVolatility_MatchesPythonVector()
    {
        using var doc = LoadVector("volatility");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("annualized_volatility").GetDouble();

        var result = dr.AnnualizedVolatility(TradingDaysPerYear);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    // ─── Downside Deviation ──────────────────────────────────────────────

    [Fact]
    public void DownsideDeviation_MatchesPythonVector()
    {
        using var doc = LoadVector("downside_deviation");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("downside_deviation").GetDouble();

        var result = dr.DownsideDeviation(0m);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    // ─── Ratios ──────────────────────────────────────────────────────────

    [Fact]
    public void SharpeRatio_MatchesPythonVector()
    {
        using var doc = LoadVector("ratios");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("sharpe_ratio").GetDouble();

        var result = dr.SharpeRatio(0m);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void AnnualizedSharpeRatio_MatchesPythonVector()
    {
        using var doc = LoadVector("ratios");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("annualized_sharpe_ratio").GetDouble();

        var result = dr.AnnualizedSharpeRatio(0m, TradingDaysPerYear);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void SortinoRatio_MatchesPythonVector()
    {
        using var doc = LoadVector("ratios");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("sortino_ratio").GetDouble();

        var result = dr.SortinoRatio(0m);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void Beta_MatchesPythonVector()
    {
        using var doc = LoadVector("ratios");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var br = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("benchmark_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("beta").GetDouble();

        var result = dr.Beta(br);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void Alpha_MatchesPythonVector()
    {
        using var doc = LoadVector("ratios");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var br = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("benchmark_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("alpha").GetDouble();

        var result = dr.Alpha(br, 0m);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void InformationRatio_MatchesPythonVector()
    {
        using var doc = LoadVector("ratios");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var br = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("benchmark_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("information_ratio").GetDouble();

        var result = dr.InformationRatio(br);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    // ─── Derived Ratios ──────────────────────────────────────────────────

    [Fact]
    public void CalmarRatio_MatchesPythonVector()
    {
        using var doc = LoadVector("derived_ratios");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("calmar_ratio").GetDouble();

        var result = dr.CalmarRatio(TradingDaysPerYear);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void OmegaRatio_MatchesPythonVector()
    {
        using var doc = LoadVector("derived_ratios");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("omega_ratio").GetDouble();

        var result = dr.OmegaRatio(0m);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void WinRate_MatchesPythonVector()
    {
        using var doc = LoadVector("derived_ratios");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("win_rate").GetDouble();

        var result = dr.WinRate();

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void ProfitFactor_MatchesPythonVector()
    {
        using var doc = LoadVector("derived_ratios");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("profit_factor").GetDouble();

        var result = dr.ProfitFactor();

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void RecoveryFactor_MatchesPythonVector()
    {
        using var doc = LoadVector("derived_ratios");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("recovery_factor").GetDouble();

        var result = dr.RecoveryFactor();

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    // ─── VaR / CVaR ─────────────────────────────────────────────────────

    [Fact]
    public void HistoricalVaR_MatchesPythonVector()
    {
        using var doc = LoadVector("var");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("historical_var").GetDouble();

        var result = dr.HistoricalVaR(0.95m);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void ParametricVaR_MatchesPythonVector()
    {
        using var doc = LoadVector("var");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("parametric_var").GetDouble();

        var result = dr.ParametricVaR(0.95m);

        // Looser tolerance due to NormalInverseCdf approximation vs scipy.stats.norm.ppf
        Assert.InRange(Math.Abs(result - expected), 0, PrecisionNumeric);
    }

    [Fact]
    public void ConditionalVaR_MatchesPythonVector()
    {
        using var doc = LoadVector("var");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("conditional_var").GetDouble();

        var result = dr.ConditionalVaR(0.95m);

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    // ─── Statistics ──────────────────────────────────────────────────────

    [Fact]
    public void Skewness_MatchesPythonVector()
    {
        using var doc = LoadVector("statistics");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("skewness").GetDouble();

        var result = dr.Skewness();

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }

    [Fact]
    public void Kurtosis_MatchesPythonVector()
    {
        using var doc = LoadVector("statistics");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var expected = (decimal)doc.RootElement.GetProperty("expected").GetProperty("kurtosis").GetDouble();

        var result = dr.Kurtosis();

        Assert.InRange(Math.Abs(result - expected), 0, PrecisionExact);
    }
}
