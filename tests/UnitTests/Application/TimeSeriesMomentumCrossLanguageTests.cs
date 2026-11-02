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
/// Cross-language verification tests for the M-3 <see cref="TimeSeriesMomentumSignal"/>. Validates that
/// the C# signal reproduces the Python own-formula reference vectors (from generate_signal_vectors.py)
/// that INDEPENDENTLY re-implement the sign × inverse-volatility arithmetic — the trailing
/// cumulative-return window, the excess-over-risk-free subtraction, the long-only sign clamp, and the
/// annualized <c>RealizedVolatility</c> — catching cross-language drift. A JSON <c>null</c> expected
/// score marks an asset the signal must omit (insufficient history).
///
/// Vectors: tests/Verification/vectors/signal_tsmom.json.
/// </summary>
public sealed class TimeSeriesMomentumCrossLanguageTests : CrossLanguageVerificationBase
{
    private static IReadOnlyList<Symbol> MakeAssets(int n)
        => Enumerable.Range(0, n).Select(i => new Symbol($"ASSET{i}")).ToList();

    [Theory]
    [InlineData("sign_rule")]
    [InlineData("inverse_vol_half")]
    [InlineData("excess_with_rf")]
    [InlineData("raw_fallback_no_rf")]
    [InlineData("insufficient_history")]
    public void TimeSeriesMomentum_MatchesPython(string caseName)
    {
        using var doc = LoadVector("signal_tsmom");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty(caseName);

        var lookback = caseData.GetProperty("lookback_months").GetInt32();
        var dpm = caseData.GetProperty("trading_days_per_month").GetInt32();
        var volWindow = caseData.GetProperty("vol_window").GetInt32();
        var dpy = caseData.GetProperty("trading_days_per_year").GetInt32();
        var n = caseData.GetProperty("n_assets").GetInt32();
        var assets = MakeAssets(n);

        // Optional market-wide risk-free auxiliary series.
        var riskFreeId = caseData.TryGetProperty("risk_free_id", out var idEl)
            ? idEl.GetString()!
            : "DGS3MO";

        IReadOnlyDictionary<Symbol, decimal[]>? aux = null;
        if (caseData.TryGetProperty("risk_free_series", out var rfEl))
        {
            aux = new Dictionary<Symbol, decimal[]> { [new Symbol(riskFreeId)] = GetDecimalArray(rfEl) };
        }

        // Build the jagged per-asset return series.
        var returns = new decimal[n][];
        var idx = 0;
        foreach (var seriesEl in caseData.GetProperty("returns").EnumerateArray())
        {
            returns[idx++] = GetDecimalArray(seriesEl);
        }

        var signal = new TimeSeriesMomentumSignal(lookback, dpm, volWindow, dpy, riskFreeId);
        var result = signal.ComputeScores(assets, returns, aux);

        // Expected scores: null => the asset must be omitted (absent key).
        var scoresEl = caseData.GetProperty("scores");
        var expectedScored = 0;
        var asset = 0;
        foreach (var scoreEl in scoresEl.EnumerateArray())
        {
            if (scoreEl.ValueKind == JsonValueKind.Null)
            {
                Assert.False(result.ContainsKey(assets[asset]),
                    $"TSMOM_{caseName}: ASSET{asset} must be omitted (insufficient history)");
            }
            else
            {
                Assert.True(result.ContainsKey(assets[asset]),
                    $"TSMOM_{caseName}: ASSET{asset} must be scored");
                var expected = (decimal)scoreEl.GetDouble();
                AssertWithinTolerance(result[assets[asset]], expected, PrecisionNumeric,
                    $"TSMOM_{caseName}[ASSET{asset}]: ");
                expectedScored++;
            }

            asset++;
        }

        Assert.Equal(expectedScored, result.Count);
    }
}
