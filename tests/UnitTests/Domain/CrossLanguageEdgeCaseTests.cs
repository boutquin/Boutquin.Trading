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
/// Cross-language verification tests for edge cases / degenerate inputs.
/// Validates that C# methods either:
///   (a) produce the same result as the Python reference, OR
///   (b) throw the expected exception type when Python marks "EXCEPTION:TypeName"
///
/// Run generate_edge_case_vectors.py in tests/Verification/ first.
/// </summary>
public sealed class CrossLanguageEdgeCaseTests : CrossLanguageVerificationBase
{
    // ═══════════════════════════════════════════════════════════════════════
    // Helper: run a single metric check against a vector file
    // ═══════════════════════════════════════════════════════════════════════

    private static void AssertMetric(
        JsonElement expected,
        string metricName,
        Func<decimal> compute,
        decimal tolerance = 0)
    {
        if (tolerance == 0)
        {
            tolerance = PrecisionExact;
        }

        if (IsExpectedException(expected, metricName, out var exceptionType))
        {
            var ex = Assert.ThrowsAny<Exception>(() => { compute(); });
            Assert.Contains(exceptionType, ex.GetType().Name,
                StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var expectedVal = GetDecimal(expected, metricName);
            var actual = compute();
            AssertWithinTolerance(actual, expectedVal, tolerance, $"{metricName}: ");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Two-element arrays (N=2): minimum valid for sample statistics
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void TwoElements_ReturnMetrics_MatchPython()
    {
        using var doc = LoadVector("edge_two_elements");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var br = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("benchmark_returns"));
        var exp = doc.RootElement.GetProperty("expected");

        // Should succeed for N=2
        AssertMetric(exp, "annualized_return", () => dr.AnnualizedReturn(TradingDaysPerYear));
        AssertMetric(exp, "cagr", () => dr.CompoundAnnualGrowthRate(TradingDaysPerYear));
        AssertMetric(exp, "volatility", dr.Volatility);
        AssertMetric(exp, "annualized_volatility", () => dr.AnnualizedVolatility(TradingDaysPerYear));
        AssertMetric(exp, "downside_deviation", () => dr.DownsideDeviation(0m));
        AssertMetric(exp, "sharpe_ratio", () => dr.SharpeRatio(0m));
        AssertMetric(exp, "beta", () => dr.Beta(br));
        AssertMetric(exp, "alpha", () => dr.Alpha(br, 0m));
        AssertMetric(exp, "information_ratio", () => dr.InformationRatio(br));

        // Should throw for N < 3 / N < 4
        AssertMetric(exp, "skewness", dr.Skewness);
        AssertMetric(exp, "kurtosis", dr.Kurtosis);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // All-zero returns: zero variance triggers division guards
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void AllZero_ZeroVarianceMetrics_ThrowCalculationException()
    {
        using var doc = LoadVector("edge_all_zero");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var br = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("benchmark_returns"));
        var exp = doc.RootElement.GetProperty("expected");

        // Returns should still work (cumulative=0, annualized=0)
        AssertMetric(exp, "cumulative_return", () =>
            dr.Aggregate(1m, (acc, r) => acc * (r + 1m)) - 1m);
        AssertMetric(exp, "annualized_return", () => dr.AnnualizedReturn(TradingDaysPerYear));
        AssertMetric(exp, "volatility", dr.Volatility);

        // Zero-variance metrics should throw
        AssertMetric(exp, "sharpe_ratio", () => dr.SharpeRatio(0m));
        AssertMetric(exp, "annualized_sharpe_ratio", () => dr.AnnualizedSharpeRatio(0m, TradingDaysPerYear));
        AssertMetric(exp, "sortino_ratio", () => dr.SortinoRatio(0m));
        AssertMetric(exp, "annualized_sortino_ratio", () => dr.AnnualizedSortinoRatio(0m, TradingDaysPerYear));
        AssertMetric(exp, "beta", () => dr.Beta(br));
        AssertMetric(exp, "information_ratio", () => dr.InformationRatio(br));
        AssertMetric(exp, "omega_ratio", () => dr.OmegaRatio(0m));
        AssertMetric(exp, "profit_factor", dr.ProfitFactor);
        AssertMetric(exp, "skewness", dr.Skewness);
        AssertMetric(exp, "kurtosis", dr.Kurtosis);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // All-identical positive returns: zero variance
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void IdenticalPositive_ZeroVariance_ThrowsWhereExpected()
    {
        using var doc = LoadVector("edge_identical_positive");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var br = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("benchmark_returns"));
        var exp = doc.RootElement.GetProperty("expected");

        // Valid metrics
        AssertMetric(exp, "annualized_return", () => dr.AnnualizedReturn(TradingDaysPerYear));
        AssertMetric(exp, "cagr", () => dr.CompoundAnnualGrowthRate(TradingDaysPerYear));
        AssertMetric(exp, "win_rate", dr.WinRate);

        // Zero-variance: should throw
        AssertMetric(exp, "sharpe_ratio", () => dr.SharpeRatio(0m));
        AssertMetric(exp, "sortino_ratio", () => dr.SortinoRatio(0m));
        AssertMetric(exp, "beta", () => dr.Beta(br));
        AssertMetric(exp, "information_ratio", () => dr.InformationRatio(br));
        AssertMetric(exp, "skewness", dr.Skewness);
        AssertMetric(exp, "kurtosis", dr.Kurtosis);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // All-negative returns: valid but edge behavior
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void AllNegative_MetricsMatchPython()
    {
        using var doc = LoadVector("edge_all_negative");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var br = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("benchmark_returns"));
        var exp = doc.RootElement.GetProperty("expected");

        AssertMetric(exp, "annualized_return", () => dr.AnnualizedReturn(TradingDaysPerYear));
        AssertMetric(exp, "cagr", () => dr.CompoundAnnualGrowthRate(TradingDaysPerYear));
        AssertMetric(exp, "volatility", dr.Volatility);
        AssertMetric(exp, "downside_deviation", () => dr.DownsideDeviation(0m));
        AssertMetric(exp, "sharpe_ratio", () => dr.SharpeRatio(0m));
        AssertMetric(exp, "sortino_ratio", () => dr.SortinoRatio(0m));
        AssertMetric(exp, "beta", () => dr.Beta(br));
        AssertMetric(exp, "alpha", () => dr.Alpha(br, 0m));
        AssertMetric(exp, "information_ratio", () => dr.InformationRatio(br));
        AssertMetric(exp, "max_drawdown", () =>
        {
            var eq = dr.EquityCurve(10000m);
            var peak = eq[0];
            var maxDD = 0m;
            foreach (var v in eq)
            {
                if (v > peak)
                {
                    peak = v;
                }

                var dd = peak == 0 ? 0 : (v / peak) - 1;
                if (dd < maxDD)
                {
                    maxDD = dd;
                }
            }
            return maxDD;
        });
        AssertMetric(exp, "win_rate", dr.WinRate);
        AssertMetric(exp, "profit_factor", dr.ProfitFactor);
        AssertMetric(exp, "skewness", dr.Skewness);
        AssertMetric(exp, "kurtosis", dr.Kurtosis);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Extreme returns: ±50% single day moves
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ExtremeReturns_MetricsMatchPython()
    {
        using var doc = LoadVector("edge_extreme_returns");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var br = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("benchmark_returns"));
        var exp = doc.RootElement.GetProperty("expected");

        AssertMetric(exp, "annualized_return", () => dr.AnnualizedReturn(TradingDaysPerYear));
        AssertMetric(exp, "volatility", dr.Volatility);
        AssertMetric(exp, "sharpe_ratio", () => dr.SharpeRatio(0m));
        AssertMetric(exp, "sortino_ratio", () => dr.SortinoRatio(0m));
        AssertMetric(exp, "beta", () => dr.Beta(br));
        AssertMetric(exp, "historical_var", () => dr.HistoricalVaR(0.95m));
        AssertMetric(exp, "parametric_var", () => dr.ParametricVaR(0.95m), PrecisionNumeric);
        AssertMetric(exp, "conditional_var", () => dr.ConditionalVaR(0.95m));
        AssertMetric(exp, "skewness", dr.Skewness);
        AssertMetric(exp, "kurtosis", dr.Kurtosis);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Total wipeout: cumulative return = -100%
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Wipeout_AnnualizedReturnAndCAGR_ThrowCalculationException()
    {
        using var doc = LoadVector("edge_wipeout");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var exp = doc.RootElement.GetProperty("expected");

        AssertMetric(exp, "annualized_return", () => dr.AnnualizedReturn(TradingDaysPerYear));
        AssertMetric(exp, "cagr", () => dr.CompoundAnnualGrowthRate(TradingDaysPerYear));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Single spike: +100% in otherwise flat series
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SingleSpike_MetricsMatchPython()
    {
        using var doc = LoadVector("edge_single_spike");
        var dr = GetDecimalArray(doc.RootElement.GetProperty("inputs").GetProperty("daily_returns"));
        var exp = doc.RootElement.GetProperty("expected");

        AssertMetric(exp, "annualized_return", () => dr.AnnualizedReturn(TradingDaysPerYear));
        AssertMetric(exp, "volatility", dr.Volatility);
        AssertMetric(exp, "sharpe_ratio", () => dr.SharpeRatio(0m));
        // Sortino: downside deviation should be 0 (all returns >= 0), so throws
        AssertMetric(exp, "sortino_ratio", () => dr.SortinoRatio(0m));
        AssertMetric(exp, "win_rate", dr.WinRate);
        // ProfitFactor: losses=0 should throw
        AssertMetric(exp, "profit_factor", dr.ProfitFactor);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Single-element array: should throw InsufficientData for sample stats
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SingleElement_SampleStats_ThrowInsufficientData()
    {
        var dr = new[] { 0.01m };

        // AnnualizedReturn should work (no sample stat needed)
        var ann = dr.AnnualizedReturn(TradingDaysPerYear);
        Assert.True(ann > 0, "Single positive return should produce positive annualized return");

        // Sample statistics require N >= 2
        Assert.Throws<InsufficientDataException>(() => dr.Volatility());
        Assert.Throws<InsufficientDataException>(() => dr.AnnualizedVolatility(TradingDaysPerYear));
        Assert.Throws<InsufficientDataException>(() => dr.SharpeRatio(0m));
        Assert.Throws<InsufficientDataException>(() => dr.SortinoRatio(0m));
        Assert.Throws<InsufficientDataException>(() => dr.DownsideDeviation(0m));
    }
}
