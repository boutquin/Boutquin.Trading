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

using Boutquin.Trading.Application.Indicators;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification tests for indicator components.
/// Validates C# implementations against Python reference vectors that replicate
/// the exact same algorithms (own-formula, not library).
///
/// Phase 5A of the verification roadmap.
/// Vectors: tests/Verification/vectors/indicator_*.json
/// </summary>
public sealed class IndicatorCrossLanguageTests : CrossLanguageVerificationBase
{
    // ═══════════════════════════════════════════════════════════════════════
    // 5A-1. SimpleMovingAverage
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("sma_period_5", 5)]
    [InlineData("sma_period_10", 10)]
    [InlineData("sma_period_20", 20)]
    [InlineData("sma_period_50", 50)]
    [InlineData("sma_constant", 10)]
    public void SMA_MatchesPythonVectors(string caseName, int period)
    {
        using var doc = LoadVector("indicator_sma_ema");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var values = GetDecimalArray(testCase.GetProperty("values"));
        var expected = GetDecimal(testCase, "expected");

        var sma = new SimpleMovingAverage(period);
        var actual = sma.Compute(values);

        AssertWithinTolerance(actual, expected, PrecisionExact, $"SMA({period}): ");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5A-2. ExponentialMovingAverage
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("ema_period_5", 5)]
    [InlineData("ema_period_10", 10)]
    [InlineData("ema_period_20", 20)]
    [InlineData("ema_period_50", 50)]
    [InlineData("ema_constant", 10)]
    [InlineData("ema_span1", 1)]
    public void EMA_MatchesPythonVectors(string caseName, int period)
    {
        using var doc = LoadVector("indicator_sma_ema");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var values = GetDecimalArray(testCase.GetProperty("values"));
        var expected = GetDecimal(testCase, "expected");

        var ema = new ExponentialMovingAverage(period);
        var actual = ema.Compute(values);

        AssertWithinTolerance(actual, expected, PrecisionExact, $"EMA({period}): ");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5A-3. RealizedVolatility
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("realvol_window_20", 20)]
    [InlineData("realvol_window_60", 60)]
    [InlineData("realvol_window_120", 120)]
    [InlineData("realvol_constant", 20)]
    public void RealizedVolatility_MatchesPythonVectors(string caseName, int window)
    {
        using var doc = LoadVector("indicator_realvol_momentum");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var values = GetDecimalArray(testCase.GetProperty("values"));
        var expected = GetDecimal(testCase, "expected");
        var tradingDays = testCase.GetProperty("trading_days_per_year").GetInt32();

        var rv = new RealizedVolatility(window, tradingDays);
        var actual = rv.Compute(values);

        AssertWithinTolerance(actual, expected, PrecisionExact, $"RealVol({window}): ");

        // Layer 3: RealizedVolatility >= 0
        Assert.True(actual >= 0, "RealizedVolatility must be non-negative");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5A-4. MomentumScore
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("momentum_12_1", 12, 1)]
    [InlineData("momentum_6_1", 6, 1)]
    [InlineData("momentum_3_1", 3, 1)]
    [InlineData("momentum_zero_returns", 12, 1)]
    public void MomentumScore_MatchesPythonVectors(string caseName, int totalMonths, int skipMonths)
    {
        using var doc = LoadVector("indicator_realvol_momentum");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var values = GetDecimalArray(testCase.GetProperty("values"));
        var expected = GetDecimal(testCase, "expected");
        var tradingDaysPerMonth = testCase.GetProperty("trading_days_per_month").GetInt32();

        var mom = new MomentumScore(totalMonths, skipMonths, tradingDaysPerMonth);
        var actual = mom.Compute(values);

        AssertWithinTolerance(actual, expected, PrecisionExact, $"Momentum({totalMonths}-{skipMonths}): ");

        // Layer 3: Momentum >= -1 (can't lose more than 100%)
        Assert.True(actual >= -1m, "Momentum score must be >= -1");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5A-5. SpreadIndicator
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("spread_standard")]
    [InlineData("spread_identical")]
    public void SpreadIndicator_MatchesPythonVectors(string caseName)
    {
        using var doc = LoadVector("indicator_spread_roc");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var series1 = GetDecimalArray(testCase.GetProperty("series1"));
        var series2 = GetDecimalArray(testCase.GetProperty("series2"));
        var expected = GetDecimal(testCase, "expected");

        var spread = new SpreadIndicator();
        var actual = spread.Compute(series1, series2);

        AssertWithinTolerance(actual, expected, PrecisionExact, "Spread: ");

        // Layer 3: Spread = series1[^1] - series2[^1]
        Assert.Equal(series1[^1] - series2[^1], actual);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5A-6. RateOfChangeIndicator
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("roc_lookback_1", 1)]
    [InlineData("roc_lookback_5", 5)]
    [InlineData("roc_lookback_10", 10)]
    [InlineData("roc_constant_spread", 5)]
    public void RateOfChange_MatchesPythonVectors(string caseName, int lookback)
    {
        using var doc = LoadVector("indicator_spread_roc");
        var root = doc.RootElement;
        var testCase = root.GetProperty("cases").GetProperty(caseName);

        var series1 = GetDecimalArray(testCase.GetProperty("series1"));
        var series2 = GetDecimalArray(testCase.GetProperty("series2"));
        var expected = GetDecimal(testCase, "expected");

        var roc = new RateOfChangeIndicator(lookback);
        var actual = roc.Compute(series1, series2);

        AssertWithinTolerance(actual, expected, PrecisionExact, $"ROC({lookback}): ");
    }
}
