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
/// Cross-language verification tests for the M-2 <see cref="CrossSectionalMomentumSignal"/>. Validates
/// that the C# signal reproduces the Python own-formula reference vectors (from
/// generate_signal_vectors.py) that INDEPENDENTLY re-implement the 12-1 momentum window arithmetic —
/// catching cross-language drift in the cumulative-return / skip-window computation. A JSON
/// <c>null</c> expected score marks an asset the signal must omit (insufficient history).
///
/// Vectors: tests/Verification/vectors/signal_xs_momentum.json.
/// </summary>
public sealed class CrossSectionalMomentumCrossLanguageTests : CrossLanguageVerificationBase
{
    private static IReadOnlyList<Symbol> MakeAssets(int n)
        => Enumerable.Range(0, n).Select(i => new Symbol($"ASSET{i}")).ToList();

    [Theory]
    [InlineData("three_asset_rank_monotone")]
    [InlineData("skip_month")]
    [InlineData("insufficient_history")]
    public void CrossSectionalMomentum_MatchesPython(string caseName)
    {
        using var doc = LoadVector("signal_xs_momentum");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty(caseName);

        var lookback = caseData.GetProperty("lookback_months").GetInt32();
        var skip = caseData.GetProperty("skip_months").GetInt32();
        var dpm = caseData.GetProperty("trading_days_per_month").GetInt32();
        var n = caseData.GetProperty("n_assets").GetInt32();
        var assets = MakeAssets(n);

        // Build the jagged per-asset return series.
        var returns = new decimal[n][];
        var idx = 0;
        foreach (var seriesEl in caseData.GetProperty("returns").EnumerateArray())
        {
            returns[idx++] = GetDecimalArray(seriesEl);
        }

        var signal = new CrossSectionalMomentumSignal(lookback, skip, dpm);
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
                    $"XSMomentum_{caseName}: ASSET{asset} must be omitted (insufficient history)");
            }
            else
            {
                Assert.True(result.ContainsKey(assets[asset]),
                    $"XSMomentum_{caseName}: ASSET{asset} must be scored");
                var expected = (decimal)scoreEl.GetDouble();
                AssertWithinTolerance(result[assets[asset]], expected, PrecisionNumeric,
                    $"XSMomentum_{caseName}[ASSET{asset}]: ");
                expectedScored++;
            }

            asset++;
        }

        Assert.Equal(expectedScored, result.Count);
    }
}
