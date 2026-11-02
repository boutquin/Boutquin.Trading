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
/// Cross-language and fidelity verification tests for <see cref="EntropicValueAtRiskMeasure"/>
/// (Ahmadi-Javid 2012 EVaR; restated/confirmed coherent by Delbaen 2015).
///
/// Vectors: tests/Verification/vectors/risk_measure_evar.json (own-formula Python replica,
/// generate_evar_vectors.py — golden-section over z + exponential-tilt gradient).
///
/// Spec-prose correction (M-7): reference-planned.md §E3 states the ordering
/// "CVaR_α ≥ EVaR_α". That is backwards — Ahmadi-Javid (2012, Prop. 3.2) establishes EVaR
/// as the Chernoff-bound UPPER envelope of CVaR, so VaR ≤ CVaR ≤ EVaR. The fidelity test
/// below asserts the mathematically-correct EVaR ≥ CVaR, and that ordering still proves the
/// inf-over-z is genuinely solved (a constant/E[X]/stub return would NOT exceed CVaR).
/// </summary>
public sealed class EntropicValueAtRiskCrossLanguageTests : CrossLanguageVerificationBase
{
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
        => element.EnumerateArray().Select(e => (decimal)e.GetDouble()).ToArray();

    // ═══════════════════════════════════════════════════════════════════════
    // AC-2: EVaR value matches the Python own-formula vectors (PrecisionNumeric)
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("standard_95", 0.95)]
    [InlineData("alpha_99", 0.99)]
    [InlineData("alpha_50", 0.50)]
    [InlineData("equal_weights", 0.95)]
    [InlineData("single_asset", 0.95)]
    [InlineData("five_asset", 0.95)]
    public void EVaR_Value_MatchesPythonVectors(string caseName, decimal confidenceLevel)
    {
        using var doc = LoadVector("risk_measure_evar");
        var testCase = doc.RootElement.GetProperty("cases").GetProperty(caseName);

        var weights = GetWeights(testCase.GetProperty("weights"));
        var scenarios = GetScenarios(testCase.GetProperty("scenarios"));
        var expectedValue = GetDecimal(testCase, "value");

        var measure = new EntropicValueAtRiskMeasure(confidenceLevel);
        var (value, _) = measure.Evaluate(weights, scenarios, 0.01m);

        AssertWithinTolerance(value, expectedValue, PrecisionNumeric, $"EVaR {caseName} value: ");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-2: EVaR gradient checked against a finite-difference of the value.
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("standard_95", 0.95)]
    [InlineData("five_asset", 0.95)]
    public void EVaR_Gradient_MatchesFiniteDifference(string caseName, decimal confidenceLevel)
    {
        using var doc = LoadVector("risk_measure_evar");
        var testCase = doc.RootElement.GetProperty("cases").GetProperty(caseName);

        var weights = GetWeights(testCase.GetProperty("weights"));
        var scenarios = GetScenarios(testCase.GetProperty("scenarios"));

        var measure = new EntropicValueAtRiskMeasure(confidenceLevel);
        var (_, gradient) = measure.Evaluate(weights, scenarios, 0.01m);

        // Central finite difference of EVaR(w) w.r.t. each weight (analytic gradient holds
        // z* fixed at the optimum — the envelope theorem makes ∂EVaR/∂w the partial
        // derivative at the optimal z*, so FD on the value must match).
        const decimal h = 1e-5m;
        for (var i = 0; i < weights.Length; i++)
        {
            var wPlus = (decimal[])weights.Clone();
            var wMinus = (decimal[])weights.Clone();
            wPlus[i] += h;
            wMinus[i] -= h;

            var (vPlus, _) = measure.Evaluate(wPlus, scenarios, 0m);
            var (vMinus, _) = measure.Evaluate(wMinus, scenarios, 0m);
            var fd = (vPlus - vMinus) / (2m * h);

            // FD over decimal/double exp arithmetic — 1e-4 tolerance.
            AssertWithinTolerance(gradient[i], fd, 1e-4m,
                $"EVaR {caseName} gradient[{i}] vs finite-difference: ");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC-3: Fidelity — EVaR ≥ CVaR at the same tail (proves inf-over-z is solved,
    // not a stub) + the E[X] limit as confidence → 0.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EVaR_IsGreaterThanOrEqualToCVaR_AtSameAlpha()
    {
        using var doc = LoadVector("risk_measure_evar");
        var testCase = doc.RootElement.GetProperty("cases").GetProperty("standard_95");
        var weights = GetWeights(testCase.GetProperty("weights"));
        var scenarios = GetScenarios(testCase.GetProperty("scenarios"));

        var evar = new EntropicValueAtRiskMeasure(0.95m);
        var cvar = new CVaRRiskMeasure(0.95m);

        var (evarVal, _) = evar.Evaluate(weights, scenarios, 0m);
        var (cvarVal, _) = cvar.Evaluate(weights, scenarios, 0m);

        Assert.True(evarVal >= cvarVal - 1e-9m,
            $"EVaR₉₅ ({evarVal}) must be ≥ CVaR₉₅ ({cvarVal}) — EVaR is the tighter coherent bound (Ahmadi-Javid 2012).");
        // Strict separation on this panel — confirms EVaR is not coincidentally equal to CVaR.
        Assert.True(evarVal > cvarVal,
            $"On this loss panel EVaR₉₅ ({evarVal}) should strictly exceed CVaR₉₅ ({cvarVal}).");
    }

    [Fact]
    public void EVaR_ApproachesMeanLoss_AsConfidenceApproachesZero()
    {
        using var doc = LoadVector("risk_measure_evar");
        var testCase = doc.RootElement.GetProperty("cases").GetProperty("standard_95");
        var weights = GetWeights(testCase.GetProperty("weights"));
        var scenarios = GetScenarios(testCase.GetProperty("scenarios"));

        // As confidence → 0 (tail → 1, inner α → 1, ln α → 0), EVaR → E[X] = mean loss.
        var measure = new EntropicValueAtRiskMeasure(0.001m);
        var (value, _) = measure.Evaluate(weights, scenarios, 0m);

        var meanLoss = 0m;
        foreach (var sc in scenarios)
        {
            var port = 0m;
            for (var i = 0; i < weights.Length; i++)
            {
                port += weights[i] * sc[i];
            }

            meanLoss += -port;
        }

        meanLoss /= scenarios.Length;

        AssertWithinTolerance(value, meanLoss, 1e-3m, "EVaR → E[X] as confidence → 0: ");
    }

    [Fact]
    public void EVaR_HigherConfidence_HigherValue()
    {
        using var doc = LoadVector("risk_measure_evar");
        var testCase = doc.RootElement.GetProperty("cases").GetProperty("standard_95");
        var weights = GetWeights(testCase.GetProperty("weights"));
        var scenarios = GetScenarios(testCase.GetProperty("scenarios"));

        var evar95 = new EntropicValueAtRiskMeasure(0.95m);
        var evar99 = new EntropicValueAtRiskMeasure(0.99m);

        var (val95, _) = evar95.Evaluate(weights, scenarios, 0m);
        var (val99, _) = evar99.Evaluate(weights, scenarios, 0m);

        Assert.True(val99 >= val95 - 1e-9m,
            $"EVaR α=0.99 ({val99}) should be ≥ EVaR α=0.95 ({val95}).");
    }
}
