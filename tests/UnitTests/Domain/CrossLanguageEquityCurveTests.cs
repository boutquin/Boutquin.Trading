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
/// Cross-language verification tests for EquityCurveExtensions:
///   - CalculateDrawdownsAndMaxDrawdownInfo
///   - MonthlyReturns
///   - AnnualReturns
///
/// Run generate_edge_case_vectors.py in tests/Verification/ first.
/// </summary>
public sealed class CrossLanguageEquityCurveTests : CrossLanguageVerificationBase
{
    private static SortedDictionary<DateOnly, decimal> ParseEquityCurve(JsonElement ecElement)
    {
        var result = new SortedDictionary<DateOnly, decimal>();
        foreach (var prop in ecElement.EnumerateObject())
        {
            var date = DateOnly.Parse(prop.Name);
            var value = (decimal)prop.Value.GetDouble();
            result[date] = value;
        }
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Drawdown analysis with multiple drawdown phases
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void DrawdownAnalysis_MultiplePhases_MatchesPython()
    {
        using var doc = LoadVector("edge_equity_drawdowns");
        var ec = ParseEquityCurve(doc.RootElement.GetProperty("inputs").GetProperty("equity_curve"));
        var exp = doc.RootElement.GetProperty("expected");

        var (drawdowns, maxDrawdown, maxDrawdownDuration) = ec.CalculateDrawdownsAndMaxDrawdownInfo();

        // Max drawdown
        var expectedMaxDD = GetDecimal(exp, "max_drawdown");
        AssertWithinTolerance(maxDrawdown, expectedMaxDD, PrecisionExact, "MaxDrawdown: ");

        // Max drawdown duration (trading days)
        var expectedDuration = exp.GetProperty("max_drawdown_duration").GetInt32();
        Assert.Equal(expectedDuration, maxDrawdownDuration);

        // Per-date drawdowns
        var expectedDrawdowns = exp.GetProperty("drawdowns");
        foreach (var prop in expectedDrawdowns.EnumerateObject())
        {
            var date = DateOnly.Parse(prop.Name);
            var expectedDD = (decimal)prop.Value.GetDouble();
            Assert.True(drawdowns.ContainsKey(date), $"Missing drawdown entry for {date}");
            AssertWithinTolerance(drawdowns[date], expectedDD, PrecisionExact, $"Drawdown on {date}: ");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Monotonically increasing equity: no drawdown
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void DrawdownAnalysis_Monotonic_NoDrawdown()
    {
        using var doc = LoadVector("edge_equity_monotonic");
        var ec = ParseEquityCurve(doc.RootElement.GetProperty("inputs").GetProperty("equity_curve"));
        var exp = doc.RootElement.GetProperty("expected");

        var (drawdowns, maxDrawdown, maxDrawdownDuration) = ec.CalculateDrawdownsAndMaxDrawdownInfo();

        Assert.Equal(0m, maxDrawdown);
        Assert.Equal(0, maxDrawdownDuration);

        // All drawdowns should be 0
        foreach (var dd in drawdowns.Values)
        {
            Assert.Equal(0m, dd);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Monthly returns from multi-year equity curve
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MonthlyReturns_ThreeYearCurve_MatchesPython()
    {
        using var doc = LoadVector("edge_monthly_annual_returns");
        var ec = ParseEquityCurve(doc.RootElement.GetProperty("inputs").GetProperty("equity_curve"));
        var exp = doc.RootElement.GetProperty("expected").GetProperty("monthly_returns");

        var monthlyReturns = ec.MonthlyReturns();

        foreach (var prop in exp.EnumerateObject())
        {
            // Key format: "YYYY-MM"
            var parts = prop.Name.Split('-');
            var year = int.Parse(parts[0]);
            var month = int.Parse(parts[1]);
            var expectedReturn = (decimal)prop.Value.GetDouble();

            Assert.True(monthlyReturns.ContainsKey((year, month)),
                $"Missing monthly return for {year}-{month:D2}");
            AssertWithinTolerance(monthlyReturns[(year, month)], expectedReturn, PrecisionExact,
                $"Monthly return {year}-{month:D2}: ");
        }

        // Verify count matches
        Assert.Equal(exp.EnumerateObject().Count(), monthlyReturns.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Annual returns from multi-year equity curve
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void AnnualReturns_ThreeYearCurve_MatchesPython()
    {
        using var doc = LoadVector("edge_monthly_annual_returns");
        var ec = ParseEquityCurve(doc.RootElement.GetProperty("inputs").GetProperty("equity_curve"));
        var exp = doc.RootElement.GetProperty("expected").GetProperty("annual_returns");

        var annualReturns = ec.AnnualReturns();

        foreach (var prop in exp.EnumerateObject())
        {
            var year = int.Parse(prop.Name);
            var expectedReturn = (decimal)prop.Value.GetDouble();

            Assert.True(annualReturns.ContainsKey(year),
                $"Missing annual return for {year}");
            AssertWithinTolerance(annualReturns[year], expectedReturn, PrecisionExact,
                $"Annual return {year}: ");
        }

        Assert.Equal(exp.EnumerateObject().Count(), annualReturns.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Single-entry equity curve: should throw InsufficientDataException
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void DrawdownAnalysis_SingleEntry_ThrowsInsufficientData()
    {
        var ec = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2023, 1, 1), 10000m }
        };

        Assert.Throws<InsufficientDataException>(() => ec.CalculateDrawdownsAndMaxDrawdownInfo());
    }
}
