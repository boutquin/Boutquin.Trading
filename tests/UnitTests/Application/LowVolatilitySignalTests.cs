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

using Boutquin.Trading.Application.Signals;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Fidelity and contract tests for <see cref="LowVolatilitySignal"/> — the Baker, Bradley &amp; Wurgler
/// (2011) low-volatility anomaly, operationalized as <c>score = −z(realized σ over the lookback)</c>, a
/// cross-sectional characteristic sort that tilts a long-only portfolio <b>toward individually low-vol
/// assets</b>. Asserts the reference-planned §M-6 properties: the score is <b>strictly decreasing in
/// realized volatility</b> (a higher-σ asset scores strictly lower — the tilt-toward-low-vol direction),
/// <b>equal-vol assets score equally</b>, the insufficient-history omission contract, the empty-input
/// contract, and the zero-cross-sectional-dispersion degeneracy (⇒ <c>CalculationException</c>, consistent
/// with the internally-z-scoring sibling <see cref="ValueMomentumCompositeSignal"/>).
/// </summary>
public sealed class LowVolatilitySignalTests
{
    // Compact params: a 4-day volatility window keeps the fixtures small while exercising the identical
    // RealizedVolatility + cross-sectional z-score arithmetic the default 60/252 signal uses.
    private const int Window = 4;

    private static Symbol Sym(string ticker) => new(ticker);

    // ── Contract: insufficient history => omitted (absent key), never an exception ─────────────────
    [Fact]
    public void ComputeScores_InsufficientHistory_OmitsAssetWithoutThrowing()
    {
        var assets = new[] { Sym("LOW"), Sym("HIGH"), Sym("SHORT") };
        var returns = new[]
        {
            new[] { 0.00m, 0.01m, 0.00m, 0.01m }, // enough history (len 4) => scored
            new[] { 0.00m, 0.03m, 0.00m, 0.03m }, // enough history (len 4) => scored
            new[] { 0.00m, 0.02m, 0.00m },        // one day short (len 3 < 4) => omitted
        };

        var signal = new LowVolatilitySignal(Window);
        var scores = signal.ComputeScores(assets, returns);

        Assert.Equal(2, scores.Count);
        Assert.True(scores.ContainsKey(assets[0]), "LOW must be scored");
        Assert.True(scores.ContainsKey(assets[1]), "HIGH must be scored");
        Assert.False(scores.ContainsKey(assets[2]), "SHORT must be omitted (absent key)");
    }

    // ── Fidelity: score STRICTLY DECREASING in realized volatility (tilt TOWARD low-vol assets) ─────
    [Fact]
    public void ComputeScores_StrictlyDecreasingInRealizedVolatility()
    {
        // Same 4-day pattern at increasing amplitude => realized σ strictly increasing A < B < C.
        var assets = new[] { Sym("LOWVOL"), Sym("MIDVOL"), Sym("HIGHVOL") };
        var returns = new[]
        {
            new[] { 0.00m, 0.01m, 0.00m, 0.01m }, // lowest realized σ  => HIGHEST score
            new[] { 0.00m, 0.02m, 0.00m, 0.02m }, // middle
            new[] { 0.00m, 0.04m, 0.00m, 0.04m }, // highest realized σ => LOWEST score
        };

        var signal = new LowVolatilitySignal(Window);
        var scores = signal.ComputeScores(assets, returns);

        Assert.Equal(3, scores.Count);
        // A higher-realized-σ asset gets a STRICTLY LOWER score (the low-vol tilt direction).
        Assert.True(scores[assets[0]] > scores[assets[1]],
            "LOWVOL (lowest σ) must score strictly above MIDVOL");
        Assert.True(scores[assets[1]] > scores[assets[2]],
            "MIDVOL must score strictly above HIGHVOL (highest σ)");
    }

    // ── Fidelity: equal-vol assets score equally (two assets with identical realized σ tie) ─────────
    [Fact]
    public void ComputeScores_EqualVolatilityAssets_ScoreEqually()
    {
        // A and B share the same 4-day return multiset (identical realized σ); C differs, so the
        // cross-section has non-zero dispersion and the z-score is well-defined.
        var assets = new[] { Sym("A"), Sym("B"), Sym("C") };
        var returns = new[]
        {
            new[] { 0.00m, 0.02m, 0.00m, 0.02m }, // σ_A
            new[] { 0.02m, 0.00m, 0.02m, 0.00m }, // σ_B == σ_A (same values, reordered)
            new[] { 0.00m, 0.05m, 0.00m, 0.05m }, // σ_C != σ_A  => non-degenerate cross-section
        };

        var signal = new LowVolatilitySignal(Window);
        var scores = signal.ComputeScores(assets, returns);

        Assert.Equal(3, scores.Count);
        Assert.Equal(scores[assets[0]], scores[assets[1]]); // equal σ => equal score
        Assert.NotEqual(scores[assets[0]], scores[assets[2]]); // different σ => different score
    }

    // ── Degenerate: zero cross-sectional dispersion (every scored asset shares the same σ) ⇒ throw ──
    // Consistent with the internally-z-scoring sibling ValueMomentumCompositeSignal and the spec's
    // Design Contract: "zero-variance series where a z-score is required ⇒ CalculationException".
    [Fact]
    public void ComputeScores_ZeroCrossSectionalDispersion_ThrowsCalculationException()
    {
        // Three assets whose 4-day return multisets are identical up to order => identical realized σ =>
        // zero cross-sectional dispersion, so the −z score is undefined.
        var assets = new[] { Sym("A"), Sym("B"), Sym("C") };
        var returns = new[]
        {
            new[] { 0.00m, 0.02m, 0.00m, 0.02m },
            new[] { 0.02m, 0.00m, 0.02m, 0.00m },
            new[] { 0.00m, 0.02m, 0.00m, 0.02m },
        };

        var signal = new LowVolatilitySignal(Window);

        var compatible = Assert.ThrowsAny<Boutquin.Trading.Domain.Exceptions.CalculationException>(
            () => signal.ComputeScores(assets, returns));
        var discriminated = Assert.Throws<Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException>(
            () => signal.ComputeScores(assets, returns));

        Assert.Equal(
            "Cannot z-score realized volatility: its 3 scored assets have zero cross-sectional " +
            "variance (every scored asset shares the same realized σ), so the −z(σ) score is undefined.",
            compatible.Message);
        Assert.Contains("zero cross-sectional variance", discriminated.Message, StringComparison.Ordinal);
    }

    // ── Contract: empty asset list => empty dictionary (never null) ────────────────────────────────
    [Fact]
    public void ComputeScores_EmptyAssets_ReturnsEmptyDictionary()
    {
        var signal = new LowVolatilitySignal(Window);
        var scores = signal.ComputeScores([], []);

        Assert.NotNull(scores);
        Assert.Empty(scores);
    }
}
