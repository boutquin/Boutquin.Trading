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
/// Fidelity and contract tests for <see cref="LongHorizonReversalValueSignal"/> — the Asness,
/// Moskowitz &amp; Pedersen (2013) asset-class value proxy: the <b>negative of the cumulative return
/// over the trailing five years (months t−60…t−0)</b>, run through the current price with <b>no skip at
/// the recent end</b>. Asserts the defining reference-planned §M-4 properties: reversal-monotonicity
/// (higher past-5y return ⇒ lower value score), the <b>no-skip window</b> (a spike in the most recent
/// month DOES move the score — the anti-regression against the drafting-aid's wrong t−60…t−13), the
/// insufficient-history omission contract, and the empty-input contract.
/// </summary>
public sealed class LongHorizonReversalValueSignalTests
{
    // Default value-signal params: 60-month lookback, 21 trading days/month => 1260-day window,
    // NO skip (the entire trailing window runs through the current price).
    private const int DaysPerMonth = 21;
    private const int LookbackMonths = 60;
    private const int LookbackDays = LookbackMonths * DaysPerMonth; // 1260

    private static Symbol Sym(string ticker) => new(ticker);

    private static decimal[] Constant(decimal dailyReturn, int length)
    {
        var series = new decimal[length];
        for (var i = 0; i < length; i++)
        {
            series[i] = dailyReturn;
        }

        return series;
    }

    // ── Fidelity: reversal-monotone — higher past-5y return => strictly LOWER (more negative) score ──
    [Fact]
    public void ComputeScores_ReversalMonotone_HigherPastReturnScoresStrictlyLower()
    {
        var assets = new[] { Sym("HI"), Sym("MID"), Sym("LO") };
        var returns = new[]
        {
            Constant(0.010m, LookbackDays), // highest past-5y return => most negative value score
            Constant(0.005m, LookbackDays), // middle
            Constant(0.001m, LookbackDays), // lowest past-5y return => highest value score
        };

        var signal = new LongHorizonReversalValueSignal();
        var scores = signal.ComputeScores(assets, returns);

        Assert.Equal(3, scores.Count);
        // Reversal: value score STRICTLY DECREASING in trailing past return (HI < MID < LO).
        Assert.True(scores[assets[0]] < scores[assets[1]], "HI (highest past return) must score strictly below MID");
        Assert.True(scores[assets[1]] < scores[assets[2]], "MID must score strictly below LO");
    }

    // ── Fidelity: the value score is the NEGATIVE of the trailing 5-year cumulative return ──────────
    [Fact]
    public void ComputeScores_ValueEqualsNegativeCumulativeReturn()
    {
        var asset = new[] { Sym("X") };
        var series = Constant(0.001m, LookbackDays);

        // Independent expected: −(∏(1+r) − 1) over the full trailing window.
        var cumulative = 1m;
        for (var i = 0; i < LookbackDays; i++)
        {
            cumulative *= 1m + series[i];
        }
        var expected = -(cumulative - 1m);

        var signal = new LongHorizonReversalValueSignal();
        var score = signal.ComputeScores(asset, new[] { series })[asset[0]];

        Assert.Equal(expected, score);
        Assert.True(score < 0m, "a positive past-5y return must yield a negative value score");
    }

    // ── Fidelity: NO skip at the recent end — a spike in the MOST RECENT month DOES move the score. ─
    // This is the anti-regression against the drafting-aid's wrong t−60…t−13 (skip-month) window:
    // unlike M-2's momentum, the value window runs THROUGH the current price, so the last month counts.
    [Fact]
    public void ComputeScores_NoSkipWindow_LastMonthSpikeMovesScore()
    {
        var asset = new[] { Sym("A") };

        var baseline = Constant(0.001m, LookbackDays);

        // Spike the entire most-recent month (indices [1239, 1260)) — INCLUDED in the value window.
        var spikeLastMonth = Constant(0.001m, LookbackDays);
        for (var i = LookbackDays - DaysPerMonth; i < LookbackDays; i++)
        {
            spikeLastMonth[i] = 0.5m;
        }

        var signal = new LongHorizonReversalValueSignal();
        var baseScore = signal.ComputeScores(asset, new[] { baseline })[asset[0]];
        var lastMonthScore = signal.ComputeScores(asset, new[] { spikeLastMonth })[asset[0]];

        // No skip: the last-month spike changes the score (a bigger 5y return => a lower value score).
        Assert.NotEqual(baseScore, lastMonthScore);
        Assert.True(lastMonthScore < baseScore,
            "a spike in the most recent month raises the 5y return, so the value score must fall");
    }

    // ── Contract: insufficient history => omitted (absent key), never an exception ─────────────────
    [Fact]
    public void ComputeScores_InsufficientHistory_OmitsAssetWithoutThrowing()
    {
        var assets = new[] { Sym("LONG"), Sym("SHORT") };
        var returns = new[]
        {
            Constant(0.003m, LookbackDays),      // enough history => scored
            Constant(0.003m, LookbackDays - 1),  // one day short => omitted
        };

        var signal = new LongHorizonReversalValueSignal();
        var scores = signal.ComputeScores(assets, returns);

        Assert.Single(scores);
        Assert.True(scores.ContainsKey(assets[0]), "LONG must be scored");
        Assert.False(scores.ContainsKey(assets[1]), "SHORT must be omitted (absent key)");
    }

    // ── Contract: empty asset list => empty dictionary (never null) ────────────────────────────────
    [Fact]
    public void ComputeScores_EmptyAssets_ReturnsEmptyDictionary()
    {
        var signal = new LongHorizonReversalValueSignal();
        var scores = signal.ComputeScores([], []);

        Assert.NotNull(scores);
        Assert.Empty(scores);
    }

    // ── Contract: returns length must match assets count (positional-alignment guard) ──────────────
    [Fact]
    public void ComputeScores_MismatchedLengths_Throws()
    {
        var assets = new[] { Sym("A"), Sym("B") };
        var returns = new[] { Constant(0.001m, LookbackDays) }; // only one series for two assets

        var signal = new LongHorizonReversalValueSignal();
        Assert.Throws<ArgumentException>(() => signal.ComputeScores(assets, returns));
    }
}
