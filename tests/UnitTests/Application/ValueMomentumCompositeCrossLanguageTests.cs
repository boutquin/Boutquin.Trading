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
/// Cross-language verification tests for the M-4 <see cref="ValueMomentumCompositeSignal"/>. Validates
/// that the C# composite reproduces the Python own-formula reference vectors (from
/// generate_signal_vectors.py) that INDEPENDENTLY re-implement the
/// <c>0.5·z(value) + 0.5·z(momentum)</c> blend over the intersection of assets scored by both legs —
/// catching cross-language drift in the value proxy, the 12-1 momentum window, the population z-score,
/// and the either-leg-omits intersection rule. A JSON <c>null</c> expected score marks an asset the
/// composite must omit.
///
/// Vectors: tests/Verification/vectors/signal_value_momentum_composite.json.
/// </summary>
public sealed class ValueMomentumCompositeCrossLanguageTests : CrossLanguageVerificationBase
{
    private static IReadOnlyList<Symbol> MakeAssets(int n)
        => Enumerable.Range(0, n).Select(i => new Symbol($"ASSET{i}")).ToList();

    [Theory]
    [InlineData("anti_correlated_panel")]
    [InlineData("either_leg_omits")]
    public void ValueMomentumComposite_MatchesPython(string caseName)
    {
        using var doc = LoadVector("signal_value_momentum_composite");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty(caseName);

        var valueLb = caseData.GetProperty("value_lookback_months").GetInt32();
        var momLb = caseData.GetProperty("momentum_lookback_months").GetInt32();
        var momSkip = caseData.GetProperty("momentum_skip_months").GetInt32();
        var dpm = caseData.GetProperty("trading_days_per_month").GetInt32();
        var valueWeight = GetDecimal(caseData, "value_weight");
        var momentumWeight = GetDecimal(caseData, "momentum_weight");
        var n = caseData.GetProperty("n_assets").GetInt32();
        var assets = MakeAssets(n);

        var returns = new decimal[n][];
        var idx = 0;
        foreach (var seriesEl in caseData.GetProperty("returns").EnumerateArray())
        {
            returns[idx++] = GetDecimalArray(seriesEl);
        }

        var composite = new ValueMomentumCompositeSignal(
            valueLb, momLb, momSkip, dpm, valueWeight, momentumWeight);
        var result = composite.ComputeScores(assets, returns);

        var scoresEl = caseData.GetProperty("scores");
        var expectedScored = 0;
        var asset = 0;
        foreach (var scoreEl in scoresEl.EnumerateArray())
        {
            if (scoreEl.ValueKind == JsonValueKind.Null)
            {
                Assert.False(result.ContainsKey(assets[asset]),
                    $"Composite_{caseName}: ASSET{asset} must be omitted (omitted by a leg)");
            }
            else
            {
                Assert.True(result.ContainsKey(assets[asset]),
                    $"Composite_{caseName}: ASSET{asset} must be scored");
                var expected = (decimal)scoreEl.GetDouble();
                AssertWithinTolerance(result[assets[asset]], expected, PrecisionNumeric,
                    $"Composite_{caseName}[ASSET{asset}]: ");
                expectedScored++;
            }

            asset++;
        }

        Assert.Equal(expectedScored, result.Count);
    }
}
