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

using Boutquin.Trading.Application.DownsideRisk;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification tests for downside risk measures.
/// Validates C# implementations against Python reference vectors that replicate
/// the exact same algorithms (own-formula, not library).
///
/// Phase 4A-4C of the verification roadmap.
/// Vectors: tests/Verification/vectors/risk_measure_*.json
/// </summary>
public sealed class RiskMeasureCrossLanguageTests : CrossLanguageVerificationBase
{
    // ─── Helpers ────────────────────────────────────────────────────────

    private static decimal[][] GetScenarios(JsonElement element)
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

    private static decimal[] GetGradient(JsonElement element)
    {
        return element.EnumerateArray()
            .Select(e => (decimal)e.GetDouble())
            .ToArray();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4A. CVaR Risk Measure
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("standard_95", 0.95)]
    [InlineData("alpha_99", 0.99)]
    [InlineData("alpha_50", 0.50)]
    [InlineData("equal_weights", 0.95)]
    [InlineData("single_asset", 0.95)]
    [InlineData("five_asset", 0.95)]
    public void CVaR_MatchesPythonVectors(string caseName, decimal confidenceLevel)
    {
        using var doc = LoadVector("risk_measure_cvar");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var weights = GetWeights(testCase.GetProperty("weights"));
        var scenarios = GetScenarios(testCase.GetProperty("scenarios"));
        var expectedValue = GetDecimal(testCase, "value");
        var expectedGradient = GetGradient(testCase.GetProperty("gradient"));

        var measure = new CVaRRiskMeasure(confidenceLevel);
        var (value, gradient) = measure.Evaluate(weights, scenarios, 0.01m);

        // CVaR value — closed-form with sorted quantile, should be exact
        AssertWithinTolerance(value, expectedValue, PrecisionExact,
            $"CVaR {caseName} value: ");

        // Gradient
        Assert.Equal(expectedGradient.Length, gradient.Length);
        for (var i = 0; i < gradient.Length; i++)
        {
            AssertWithinTolerance(gradient[i], expectedGradient[i], PrecisionExact,
                $"CVaR {caseName} gradient[{i}]: ");
        }
    }

    [Fact]
    public void CVaR_HigherConfidence_HigherValue()
    {
        using var doc = LoadVector("risk_measure_cvar");
        var root = doc.RootElement;
        var cases = root.GetProperty("cases");

        var scenarios = GetScenarios(cases.GetProperty("standard_95").GetProperty("scenarios"));
        var weights = GetWeights(cases.GetProperty("standard_95").GetProperty("weights"));

        var cvar95 = new CVaRRiskMeasure(0.95m);
        var cvar99 = new CVaRRiskMeasure(0.99m);

        var (val95, _) = cvar95.Evaluate(weights, scenarios, 0.01m);
        var (val99, _) = cvar99.Evaluate(weights, scenarios, 0.01m);

        Assert.True(val99 >= val95 - PrecisionExact,
            $"CVaR α=0.99 ({val99}) should be ≥ CVaR α=0.95 ({val95})");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4B. CDaR Risk Measure
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("standard_95", 0.95)]
    [InlineData("alpha_80", 0.80)]
    [InlineData("rising_portfolio", 0.95)]
    [InlineData("equal_weights", 0.95)]
    [InlineData("single_asset", 0.95)]
    [InlineData("five_asset", 0.95)]
    public void CDaR_MatchesPythonVectors(string caseName, decimal confidenceLevel)
    {
        using var doc = LoadVector("risk_measure_cdar");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var weights = GetWeights(testCase.GetProperty("weights"));
        var scenarios = GetScenarios(testCase.GetProperty("scenarios"));
        var expectedValue = GetDecimal(testCase, "value");
        var expectedGradient = GetGradient(testCase.GetProperty("gradient"));

        var measure = new CDaRRiskMeasure(confidenceLevel);
        var (value, gradient) = measure.Evaluate(weights, scenarios, 0.01m);

        // CDaR value — closed-form with sorted quantile
        AssertWithinTolerance(value, expectedValue, PrecisionExact,
            $"CDaR {caseName} value: ");

        // CDaR gradient is path-dependent: cumulative returns determine the running
        // peak and which scenarios fall in the tail. Float↔decimal arithmetic
        // differences compound across 100 time steps, causing different scenarios to
        // cross the tail threshold. At α=0.80 (20 tail scenarios), a single scenario
        // flip changes gradient contributions by O(1e-3). At α=0.95 (5 tail scenarios),
        // divergence is smaller. Use 1e-2 tolerance for gradients.
        const decimal cdarGradientTolerance = 1e-2m;
        Assert.Equal(expectedGradient.Length, gradient.Length);
        for (var i = 0; i < gradient.Length; i++)
        {
            AssertWithinTolerance(gradient[i], expectedGradient[i], cdarGradientTolerance,
                $"CDaR {caseName} gradient[{i}]: ");
        }
    }

    [Fact]
    public void CDaR_RisingPortfolio_SmallValue()
    {
        using var doc = LoadVector("risk_measure_cdar");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("rising_portfolio");

        var weights = GetWeights(testCase.GetProperty("weights"));
        var scenarios = GetScenarios(testCase.GetProperty("scenarios"));

        var measure = new CDaRRiskMeasure(0.95m);
        var (value, _) = measure.Evaluate(weights, scenarios, 0.01m);

        Assert.True(value <= 0.01m,
            $"CDaR of rising portfolio ({value}) should be small");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4C. Downside Deviation Risk Measure
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("standard_mar0", 0.0)]
    [InlineData("mar_001", 0.01)]
    [InlineData("mar_neg001", -0.01)]
    [InlineData("equal_weights", 0.0)]
    [InlineData("single_asset", 0.0)]
    [InlineData("five_asset", 0.0)]
    public void DownsideDeviation_MatchesPythonVectors(string caseName, decimal mar)
    {
        using var doc = LoadVector("risk_measure_downside_deviation");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var weights = GetWeights(testCase.GetProperty("weights"));
        var scenarios = GetScenarios(testCase.GetProperty("scenarios"));
        var expectedValue = GetDecimal(testCase, "value");
        var expectedGradient = GetGradient(testCase.GetProperty("gradient"));

        var measure = new DownsideDeviationRiskMeasure(mar);
        var (value, gradient) = measure.Evaluate(weights, scenarios, 0.01m);

        AssertWithinTolerance(value, expectedValue, PrecisionExact,
            $"DownsideDev {caseName} value: ");

        Assert.Equal(expectedGradient.Length, gradient.Length);
        for (var i = 0; i < gradient.Length; i++)
        {
            AssertWithinTolerance(gradient[i], expectedGradient[i], PrecisionExact,
                $"DownsideDev {caseName} gradient[{i}]: ");
        }
    }

    [Fact]
    public void DownsideDeviation_AllAboveMAR_ReturnsZero()
    {
        using var doc = LoadVector("risk_measure_downside_deviation");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("all_above_mar");

        var weights = GetWeights(testCase.GetProperty("weights"));
        var scenarios = GetScenarios(testCase.GetProperty("scenarios"));

        var measure = new DownsideDeviationRiskMeasure(0m);
        var (value, gradient) = measure.Evaluate(weights, scenarios, 0.01m);

        Assert.Equal(0m, value);
        foreach (var g in gradient)
        {
            Assert.Equal(0m, g);
        }
    }

    [Fact]
    public void DownsideDeviation_HigherMAR_HigherValue()
    {
        using var doc = LoadVector("risk_measure_downside_deviation");
        var root = doc.RootElement;
        var cases = root.GetProperty("cases");

        var scenarios = GetScenarios(cases.GetProperty("standard_mar0").GetProperty("scenarios"));
        var weights = GetWeights(cases.GetProperty("standard_mar0").GetProperty("weights"));

        var dd0 = new DownsideDeviationRiskMeasure(0m);
        var dd01 = new DownsideDeviationRiskMeasure(0.01m);

        var (val0, _) = dd0.Evaluate(weights, scenarios, 0.01m);
        var (val01, _) = dd01.Evaluate(weights, scenarios, 0.01m);

        Assert.True(val01 >= val0 - PrecisionExact,
            $"DownsideDev MAR=0.01 ({val01}) should be ≥ MAR=0 ({val0})");
    }
}
