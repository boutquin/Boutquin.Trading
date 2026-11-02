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
/// Cross-language verification tests for the M-5 <see cref="CarrySignal"/>. Validates that the C# signal
/// reproduces the Python own-formula reference vectors (from generate_signal_vectors.py) that
/// INDEPENDENTLY re-implement the per-asset-class carry arithmetic — bond yield + roll-down from two
/// market-wide curve points, equity/REIT trailing income yield, and the FX-hedged short-rate differential
/// — including the last-observation selection and the missing-series omission contract. A JSON
/// <c>null</c> expected score marks an asset the signal must omit.
///
/// Vectors: tests/Verification/vectors/signal_carry.json.
/// </summary>
public sealed class CarrySignalCrossLanguageTests : CrossLanguageVerificationBase
{
    private static IReadOnlyList<Symbol> MakeAssets(int n)
        => Enumerable.Range(0, n).Select(i => new Symbol($"ASSET{i}")).ToList();

    [Theory]
    [InlineData("multi_class_basic")]
    [InlineData("bond_upward_curve")]
    [InlineData("per_asset_omission")]
    [InlineData("market_key_omission_bond")]
    public void CarrySignal_MatchesPython(string caseName)
    {
        using var doc = LoadVector("signal_carry");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty(caseName);

        var rollDown = (decimal)caseData.GetProperty("roll_down_duration").GetDouble();
        var longId = caseData.GetProperty("long_curve_id").GetString()!;
        var shortId = caseData.GetProperty("short_curve_id").GetString()!;
        var localId = caseData.GetProperty("local_short_rate_id").GetString()!;
        var n = caseData.GetProperty("n_assets").GetInt32();
        var assets = MakeAssets(n);

        // Asset-class map, positionally aligned with the assets.
        var classes = new Dictionary<Symbol, CarryAssetClass>(n);
        var ci = 0;
        foreach (var classEl in caseData.GetProperty("asset_classes").EnumerateArray())
        {
            classes[assets[ci++]] = Enum.Parse<CarryAssetClass>(classEl.GetString()!);
        }

        // Auxiliary series: market-wide keys + per-asset (asset-symbol-keyed) series.
        var aux = new Dictionary<Symbol, decimal[]>();
        foreach (var marketEl in caseData.GetProperty("market_series").EnumerateObject())
        {
            aux[new Symbol(marketEl.Name)] = GetDecimalArray(marketEl.Value);
        }

        var ai = 0;
        foreach (var perAssetEl in caseData.GetProperty("per_asset_series").EnumerateArray())
        {
            if (perAssetEl.ValueKind != JsonValueKind.Null)
            {
                aux[assets[ai]] = GetDecimalArray(perAssetEl);
            }

            ai++;
        }

        // Per-asset return series (present but not consumed by the carry formulas).
        var returns = new decimal[n][];
        var ri = 0;
        foreach (var seriesEl in caseData.GetProperty("returns").EnumerateArray())
        {
            returns[ri++] = GetDecimalArray(seriesEl);
        }

        var signal = new CarrySignal(classes, rollDown, longId, shortId, localId);
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
                    $"Carry_{caseName}: ASSET{asset} must be omitted (missing series / unmapped class)");
            }
            else
            {
                Assert.True(result.ContainsKey(assets[asset]),
                    $"Carry_{caseName}: ASSET{asset} must be scored");
                var expected = (decimal)scoreEl.GetDouble();
                AssertWithinTolerance(result[assets[asset]], expected, PrecisionNumeric,
                    $"Carry_{caseName}[ASSET{asset}]: ");
                expectedScored++;
            }

            asset++;
        }

        Assert.Equal(expectedScored, result.Count);
    }
}
