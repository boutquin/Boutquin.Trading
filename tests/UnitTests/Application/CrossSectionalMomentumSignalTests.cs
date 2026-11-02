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
using Boutquin.Trading.Application.Signals;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Fidelity and contract tests for <see cref="CrossSectionalMomentumSignal"/> — the Jegadeesh &amp;
/// Titman (1993) cross-sectional (relative) momentum signal, operationalized as the standard 12-1
/// trailing return (12-month lookback, most-recent month excluded). Asserts the defining
/// reference-planned properties: rank-monotonicity in trailing past return, exclusion of the most
/// recent month (skip-month), the insufficient-history omission contract, and reuse of
/// <see cref="MomentumScore"/> (no duplicated momentum arithmetic).
/// </summary>
public sealed class CrossSectionalMomentumSignalTests
{
    // Default signal parameters: 12-1 momentum, 21 trading days/month => 252-day lookback,
    // scored window [0, 231) (the last 21 days — one month — excluded).
    private const int DaysPerMonth = 21;
    private const int TotalMonths = 12;
    private const int LookbackDays = TotalMonths * DaysPerMonth; // 252

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

    // ── Fidelity: rank-monotone (higher trailing 12-1 return => strictly higher score) ─────────
    [Fact]
    public void ComputeScores_RankMonotone_HigherPastReturnScoresStrictlyHigher()
    {
        var assets = new[] { Sym("HI"), Sym("MID"), Sym("LO") };
        var returns = new[]
        {
            Constant(0.010m, LookbackDays), // highest constant daily return
            Constant(0.005m, LookbackDays), // middle
            Constant(0.001m, LookbackDays), // lowest
        };

        var signal = new CrossSectionalMomentumSignal();
        var scores = signal.ComputeScores(assets, returns);

        Assert.Equal(3, scores.Count);
        // Strict rank-monotonicity: HI > MID > LO.
        Assert.True(scores[assets[0]] > scores[assets[1]], "HI score must strictly exceed MID score");
        Assert.True(scores[assets[1]] > scores[assets[2]], "MID score must strictly exceed LO score");
    }

    // ── Fidelity: skip-month (a spike in the most-recent month must NOT move the score) ────────
    // The test is deliberately non-vacuous: the identical spike placed INSIDE the scored window
    // DOES move the score, so the excluded-window equality is a real assertion about the skip.
    [Fact]
    public void ComputeScores_SkipMonth_LastMonthSpikeExcluded_InWindowSpikeMoves()
    {
        var asset = new[] { Sym("A") };

        var baseline = Constant(0.001m, LookbackDays);

        // Spike the entire most-recent month (indices [231, 252)) — the excluded window.
        var spikeLastMonth = Constant(0.001m, LookbackDays);
        for (var i = LookbackDays - DaysPerMonth; i < LookbackDays; i++)
        {
            spikeLastMonth[i] = 0.5m;
        }

        // Spike a day well inside the scored window (index 100) — must change the score.
        var spikeInWindow = Constant(0.001m, LookbackDays);
        spikeInWindow[100] = 0.5m;

        var signal = new CrossSectionalMomentumSignal();
        var baseScore = signal.ComputeScores(asset, new[] { baseline })[asset[0]];
        var lastMonthScore = signal.ComputeScores(asset, new[] { spikeLastMonth })[asset[0]];
        var inWindowScore = signal.ComputeScores(asset, new[] { spikeInWindow })[asset[0]];

        // Skip works: last-month spike leaves the score exactly unchanged.
        Assert.Equal(baseScore, lastMonthScore);
        // Non-vacuity: the same spike inside the scored window changes the score.
        Assert.NotEqual(baseScore, inWindowScore);
    }

    // ── Contract: insufficient history => omitted (absent key), never an exception ────────────
    [Fact]
    public void ComputeScores_InsufficientHistory_OmitsAssetWithoutThrowing()
    {
        var assets = new[] { Sym("LONG"), Sym("SHORT") };
        var returns = new[]
        {
            Constant(0.003m, LookbackDays),      // enough history => scored
            Constant(0.003m, LookbackDays - 1),  // one day short => omitted
        };

        var signal = new CrossSectionalMomentumSignal();
        var scores = signal.ComputeScores(assets, returns);

        Assert.Single(scores);
        Assert.True(scores.ContainsKey(assets[0]), "LONG must be scored");
        Assert.False(scores.ContainsKey(assets[1]), "SHORT must be omitted (absent key)");
    }

    // ── Contract: empty asset list => empty dictionary (never null) ───────────────────────────
    [Fact]
    public void ComputeScores_EmptyAssets_ReturnsEmptyDictionary()
    {
        var signal = new CrossSectionalMomentumSignal();
        var scores = signal.ComputeScores([], []);

        Assert.NotNull(scores);
        Assert.Empty(scores);
    }

    // ── Reuse: the signal's score equals MomentumScore.Compute (delegation, not duplication) ──
    [Fact]
    public void ComputeScores_ReusesMomentumScore_ScoreEqualsMomentumScoreCompute()
    {
        var asset = new[] { Sym("X") };
        var series = Constant(0.004m, LookbackDays);

        var signal = new CrossSectionalMomentumSignal();
        var score = signal.ComputeScores(asset, new[] { series })[asset[0]];

        var expected = new MomentumScore(TotalMonths, 1, DaysPerMonth).Compute(series);
        Assert.Equal(expected, score);
    }
}
