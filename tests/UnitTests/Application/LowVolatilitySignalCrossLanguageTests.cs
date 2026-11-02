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

using Boutquin.Trading.Application.Signals;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification tests for the M-6 <see cref="LowVolatilitySignal"/>. Validates that the C#
/// signal reproduces the Python own-formula reference vectors (from generate_signal_vectors.py) that
/// INDEPENDENTLY re-implement <c>score = −z(realized σ)</c> — reusing the own-formula realized-volatility
/// estimate, cross-sectionally population-z-scoring the surviving realized vols, and negating (a higher
/// realized σ maps to a strictly lower score, the tilt toward low-vol assets) — including the
/// insufficient-history omission contract. A JSON <c>null</c> expected score marks an asset the signal
/// must omit.
///
/// Vectors: tests/Verification/vectors/signal_lowvol.json.
/// </summary>
public sealed class LowVolatilitySignalCrossLanguageTests : CrossLanguageVerificationBase
{
    private static IReadOnlyList<Symbol> MakeAssets(int n)
        => Enumerable.Range(0, n).Select(i => new Symbol($"ASSET{i}")).ToList();

    [Theory]
    [InlineData("three_asset_strictly_decreasing")]
    [InlineData("equal_vol_two_tie")]
    [InlineData("insufficient_history")]
    public void LowVolatilitySignal_MatchesPython(string caseName)
    {
        using var doc = LoadVector("signal_lowvol");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty(caseName);

        var window = caseData.GetProperty("volatility_window").GetInt32();
        var dpy = caseData.GetProperty("trading_days_per_year").GetInt32();
        var n = caseData.GetProperty("n_assets").GetInt32();
        var assets = MakeAssets(n);

        var returns = new decimal[n][];
        var ri = 0;
        foreach (var seriesEl in caseData.GetProperty("returns").EnumerateArray())
        {
            returns[ri++] = GetDecimalArray(seriesEl);
        }

        var signal = new LowVolatilitySignal(window, dpy);
        var result = signal.ComputeScores(assets, returns);

        // Expected scores: null => the asset must be omitted (absent key).
        var scoresEl = caseData.GetProperty("scores");
        var expectedScored = 0;
        var asset = 0;
        foreach (var scoreEl in scoresEl.EnumerateArray())
        {
            if (scoreEl.ValueKind == JsonValueKind.Null)
            {
                Assert.False(result.ContainsKey(assets[asset]),
                    $"LowVol_{caseName}: ASSET{asset} must be omitted (insufficient history)");
            }
            else
            {
                Assert.True(result.ContainsKey(assets[asset]),
                    $"LowVol_{caseName}: ASSET{asset} must be scored");
                var expected = (decimal)scoreEl.GetDouble();
                AssertWithinTolerance(result[assets[asset]], expected, PrecisionNumeric,
                    $"LowVol_{caseName}[ASSET{asset}]: ");
                expectedScored++;
            }

            asset++;
        }

        Assert.Equal(expectedScored, result.Count);
    }
}
