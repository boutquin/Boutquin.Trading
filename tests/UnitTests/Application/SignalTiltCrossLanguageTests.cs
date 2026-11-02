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

using Boutquin.Trading.Application.PortfolioConstruction;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language verification tests for the M-1 signal-tilt layer. Validates that
/// <see cref="SignalTiltConstruction"/> reproduces the Python own-formula reference vectors that
/// independently re-implement the additive cross-sectional z-score tilt + clamp + renormalize
/// transform (own-formula, not a library).
///
/// Vectors: tests/Verification/vectors/signal_tilt_basic.json (from generate_signal_vectors.py).
/// </summary>
public sealed class SignalTiltCrossLanguageTests : CrossLanguageVerificationBase
{
    /// <summary>
    /// Base model returning a fixed per-asset weight map (the "base weights" leg of each vector).
    /// </summary>
    private sealed class FixedBaseModel : IPortfolioConstructionModel
    {
        private readonly IReadOnlyDictionary<Symbol, decimal> _weights;

        public FixedBaseModel(IReadOnlyDictionary<Symbol, decimal> weights) => _weights = weights;

        public IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeights(
            IReadOnlyList<Symbol> assets, decimal[][] returns) => _weights;
    }

    /// <summary>
    /// Signal returning a fixed per-asset score map. Assets absent from the map are omitted (the
    /// insufficient-history / absent-key case represented as a JSON <c>null</c> score).
    /// </summary>
    private sealed class FixedScoreSignal : ICrossSectionalSignal
    {
        private readonly IReadOnlyDictionary<Symbol, decimal> _scores;

        public FixedScoreSignal(IReadOnlyDictionary<Symbol, decimal> scores) => _scores = scores;

        public IReadOnlyDictionary<Symbol, decimal> ComputeScores(
            IReadOnlyList<Symbol> assets,
            decimal[][] returns,
            IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null) => _scores;
    }

    private static IReadOnlyList<Symbol> MakeAssets(int n)
        => Enumerable.Range(0, n).Select(i => new Symbol($"ASSET{i}")).ToList();

    [Theory]
    [InlineData("identity_zero_strength")]
    [InlineData("single_signal_three_asset")]
    [InlineData("absent_key_three_asset")]
    [InlineData("clamp_negative_three_asset")]
    [InlineData("two_signals_five_asset")]
    public void SignalTilt_MatchesPython(string caseName)
    {
        using var doc = LoadVector("signal_tilt_basic");
        var caseData = doc.RootElement.GetProperty("cases").GetProperty(caseName);

        var n = caseData.GetProperty("n_assets").GetInt32();
        var assets = MakeAssets(n);

        var baseWeightsArr = GetDecimalArray(caseData.GetProperty("base_weights"));
        var baseWeights = new Dictionary<Symbol, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            baseWeights[assets[i]] = baseWeightsArr[i];
        }

        var baseModel = new FixedBaseModel(baseWeights);

        var signals = new List<(ICrossSectionalSignal, decimal)>();
        foreach (var signalEl in caseData.GetProperty("signals").EnumerateArray())
        {
            var strength = (decimal)signalEl.GetProperty("strength").GetDouble();
            var scoresEl = signalEl.GetProperty("scores");
            var scores = new Dictionary<Symbol, decimal>();
            var idx = 0;
            foreach (var scoreEl in scoresEl.EnumerateArray())
            {
                if (scoreEl.ValueKind != JsonValueKind.Null)
                {
                    scores[assets[idx]] = (decimal)scoreEl.GetDouble();
                }

                idx++;
            }

            signals.Add((new FixedScoreSignal(scores), strength));
        }

        var expected = GetDecimalArray(caseData.GetProperty("weights"));

        var decorator = new SignalTiltConstruction(baseModel, signals);
        var result = decorator.ComputeTargetWeights(assets, new decimal[n][]);

        Assert.Equal(expected.Length, result.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            AssertWithinTolerance(result[assets[i]], expected[i], PrecisionNumeric, $"SignalTilt_{caseName}[{i}]: ");
        }

        AssertWithinTolerance(result.Values.Sum(), 1m, PrecisionNumeric, $"SignalTilt_{caseName} sum: ");
    }
}
