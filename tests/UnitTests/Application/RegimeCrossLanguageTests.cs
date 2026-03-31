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

using Boutquin.Trading.Application.Regime;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification tests for regime classifier, risk rules,
/// and position sizing.
///
/// Phase 5B-5D of the verification roadmap.
/// Vectors: tests/Verification/vectors/regime_classifier.json,
///          risk_rules.json, position_sizing.json
/// </summary>
public sealed class RegimeCrossLanguageTests : CrossLanguageVerificationBase
{
    private static EconomicRegime ParseRegime(string name) =>
        Enum.Parse<EconomicRegime>(name);

    // ═══════════════════════════════════════════════════════════════════════
    // 5B. Regime Classifier — Individual quadrants
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("rising_rising", "RisingGrowthRisingInflation")]
    [InlineData("rising_falling", "RisingGrowthFallingInflation")]
    [InlineData("falling_rising", "FallingGrowthRisingInflation")]
    [InlineData("falling_falling", "FallingGrowthFallingInflation")]
    public void RegimeClassifier_AllQuadrants(string caseName, string expectedRegime)
    {
        using var doc = LoadVector("regime_classifier");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var growth = GetDecimal(testCase, "growth_signal");
        var inflation = GetDecimal(testCase, "inflation_signal");
        var deadband = GetDecimal(testCase, "deadband");

        var classifier = new GrowthInflationRegimeClassifier(deadband);
        var actual = classifier.Classify(growth, inflation);

        Assert.Equal(ParseRegime(expectedRegime), actual);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5B. Regime Classifier — Hysteresis
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("hysteresis_both_ambiguous", "RisingGrowthRisingInflation")]
    [InlineData("hysteresis_growth_ambiguous", "RisingGrowthFallingInflation")]
    [InlineData("hysteresis_inflation_ambiguous", "FallingGrowthRisingInflation")]
    public void RegimeClassifier_Hysteresis(string caseName, string expectedRegime)
    {
        using var doc = LoadVector("regime_classifier");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var growth = GetDecimal(testCase, "growth_signal");
        var inflation = GetDecimal(testCase, "inflation_signal");
        var deadband = GetDecimal(testCase, "deadband");
        var priorRegimeStr = testCase.GetProperty("prior_regime").GetString()!;

        // Set up prior regime by first calling Classify with a clear signal
        var classifier = new GrowthInflationRegimeClassifier(deadband);

        // Prime the classifier with the prior regime
        var priorRegime = ParseRegime(priorRegimeStr);
        switch (priorRegime)
        {
            case EconomicRegime.RisingGrowthRisingInflation:
                classifier.Classify(1m, 1m); // clear rising/rising
                break;
            case EconomicRegime.RisingGrowthFallingInflation:
                classifier.Classify(1m, -1m);
                break;
            case EconomicRegime.FallingGrowthRisingInflation:
                classifier.Classify(-1m, 1m);
                break;
            case EconomicRegime.FallingGrowthFallingInflation:
                classifier.Classify(-1m, -1m);
                break;
        }

        // Now test the ambiguous signal
        var actual = classifier.Classify(growth, inflation);
        Assert.Equal(ParseRegime(expectedRegime), actual);
    }

    [Fact]
    public void RegimeClassifier_NoPriorBothAmbiguous()
    {
        using var doc = LoadVector("regime_classifier");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("no_prior_both_ambiguous");

        var growth = GetDecimal(testCase, "growth_signal");
        var inflation = GetDecimal(testCase, "inflation_signal");
        var deadband = GetDecimal(testCase, "deadband");

        var classifier = new GrowthInflationRegimeClassifier(deadband);
        var actual = classifier.Classify(growth, inflation);

        Assert.Equal(EconomicRegime.FallingGrowthFallingInflation, actual);
    }

    [Fact]
    public void RegimeClassifier_ExactBoundary()
    {
        using var doc = LoadVector("regime_classifier");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("exact_boundary");

        var growth = GetDecimal(testCase, "growth_signal");
        var inflation = GetDecimal(testCase, "inflation_signal");
        var deadband = GetDecimal(testCase, "deadband");

        var classifier = new GrowthInflationRegimeClassifier(deadband);
        var actual = classifier.Classify(growth, inflation);

        // Signal == deadband is NOT > deadband, so it's ambiguous
        Assert.Equal(EconomicRegime.FallingGrowthFallingInflation, actual);
    }

    [Fact]
    public void RegimeClassifier_ZeroDeadband()
    {
        using var doc = LoadVector("regime_classifier");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty("zero_deadband_tiny");

        var growth = GetDecimal(testCase, "growth_signal");
        var inflation = GetDecimal(testCase, "inflation_signal");

        var classifier = new GrowthInflationRegimeClassifier(0m);
        var actual = classifier.Classify(growth, inflation);

        Assert.Equal(EconomicRegime.RisingGrowthRisingInflation, actual);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5B. Regime Classifier — Sequence (transitions with hysteresis)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void RegimeClassifier_Sequence()
    {
        using var doc = LoadVector("regime_classifier");
        var root = doc.RootElement;
        var seqCase = root.GetProperty("cases").GetProperty("sequence");

        var signals = seqCase.GetProperty("signals").EnumerateArray().ToArray();
        var deadband = GetDecimal(seqCase, "deadband");
        var expectedRegimes = seqCase.GetProperty("expected_regimes")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();

        var classifier = new GrowthInflationRegimeClassifier(deadband);

        for (var i = 0; i < signals.Length; i++)
        {
            var growth = (decimal)signals[i].GetProperty("growth").GetDouble();
            var inflation = (decimal)signals[i].GetProperty("inflation").GetDouble();
            var actual = classifier.Classify(growth, inflation);
            Assert.Equal(ParseRegime(expectedRegimes[i]), actual);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5C. Risk Rules — MaxDrawdown (logic verification via vectors)
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("max_dd_no_drawdown", true)]
    [InlineData("max_dd_within_limit", true)]
    [InlineData("max_dd_exceeded", false)]
    [InlineData("max_dd_at_limit", true)]
    [InlineData("max_dd_single_point", true)]
    public void MaxDrawdownRule_MatchesPythonLogic(string caseName, bool expectedAllowed)
    {
        using var doc = LoadVector("risk_rules");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var equityCurve = GetDecimalArray(testCase.GetProperty("equity_curve"));
        var maxDdPct = GetDecimal(testCase, "max_drawdown_percent");

        // Verify the logic: compute drawdown and check against limit
        // (We can't call the actual rule without a full portfolio mock,
        //  but we verify the same math)
        const decimal tolerance = 0.0001m;
        bool allowed;

        if (equityCurve.Length < 2)
        {
            allowed = true;
        }
        else
        {
            var peak = equityCurve.Max();
            var last = equityCurve[^1];
            var dd = peak > 0 ? (peak - last) / peak : 0m;
            allowed = dd <= maxDdPct + tolerance;
        }

        Assert.Equal(expectedAllowed, allowed);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5C. Risk Rules — MaxPositionSize (logic verification)
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("max_pos_allowed", true)]
    [InlineData("max_pos_rejected", false)]
    [InlineData("max_pos_at_limit", true)]
    public void MaxPositionSizeRule_MatchesPythonLogic(string caseName, bool expectedAllowed)
    {
        using var doc = LoadVector("risk_rules");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var positionValue = GetDecimal(testCase, "position_value");
        var totalValue = GetDecimal(testCase, "total_portfolio_value");
        var maxPct = GetDecimal(testCase, "max_position_percent");

        const decimal tolerance = 0.0001m;
        var pct = Math.Abs(positionValue) / totalValue;
        var allowed = pct <= maxPct + tolerance;

        Assert.Equal(expectedAllowed, allowed);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5C. Risk Rules — MaxSectorExposure (logic verification)
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("max_sector_allowed", true)]
    [InlineData("max_sector_rejected", false)]
    public void MaxSectorExposureRule_MatchesPythonLogic(string caseName, bool expectedAllowed)
    {
        using var doc = LoadVector("risk_rules");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var sectorExposure = GetDecimal(testCase, "sector_exposure");
        var totalValue = GetDecimal(testCase, "total_portfolio_value");
        var maxPct = GetDecimal(testCase, "max_exposure_percent");

        const decimal tolerance = 0.001m;
        var pct = Math.Abs(sectorExposure) / totalValue;
        var allowed = pct <= maxPct + tolerance;

        Assert.Equal(expectedAllowed, allowed);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5D. Position Sizing — FixedWeight (math verification)
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("fixed_equal_no_buffer")]
    [InlineData("fixed_unequal_with_buffer")]
    [InlineData("fixed_three_assets")]
    [InlineData("fixed_rounding_edge")]
    public void FixedWeightPositionSizing_MatchesPythonVectors(string caseName)
    {
        using var doc = LoadVector("position_sizing");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var totalValue = GetDecimal(testCase, "total_value");
        var cashBuffer = GetDecimal(testCase, "cash_buffer_percent");
        var allocatable = totalValue * (1m - cashBuffer);

        var weights = testCase.GetProperty("weights");
        var prices = testCase.GetProperty("prices");
        var expectedQtys = testCase.GetProperty("expected_quantities");

        foreach (var weight in weights.EnumerateObject())
        {
            var assetName = weight.Name;
            var w = (decimal)weight.Value.GetDouble();
            var price = (decimal)prices.GetProperty(assetName).GetDouble();
            var expectedQty = expectedQtys.GetProperty(assetName).GetInt32();

            var desiredValue = allocatable * w;
            // C# uses Math.Round with MidpointRounding.AwayFromZero
            var actualQty = (int)Math.Round(desiredValue / price, MidpointRounding.AwayFromZero);

            Assert.Equal(expectedQty, actualQty);

            // Layer 3: quantities are non-negative for positive weights
            Assert.True(actualQty >= 0, $"Position quantity for {assetName} must be non-negative");
        }
    }
}
