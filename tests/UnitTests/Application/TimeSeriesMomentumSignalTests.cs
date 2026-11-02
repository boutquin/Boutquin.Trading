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

using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Application.Signals;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Fidelity and contract tests for <see cref="TimeSeriesMomentumSignal"/> — the Moskowitz, Ooi &amp;
/// Pedersen (2012) time-series (trend) momentum signal, operationalized long-only as
/// <c>sign(trailing 12-month excess return) · (1 / σ)</c>. Asserts the defining reference-planned
/// properties: the sign rule (up-trend positive, down-trend clamped to zero), the exact sign flip at the
/// trailing-excess-return zero boundary, inverse-proportionality of the magnitude to σ (2× vol ⇒ ½
/// score), both risk-free paths (excess when the aux key is present, raw fallback when absent), the
/// insufficient-history omission contract, the zero-volatility degenerate guard, and that the signal is
/// wired live through <see cref="SignalTiltConstruction"/>.
/// </summary>
public sealed class TimeSeriesMomentumSignalTests
{
    // Default-shaped params kept explicit so the test series lengths are self-documenting.
    private const int LookbackMonths = 12;
    private const int DaysPerMonth = 21;
    private const int LookbackDays = LookbackMonths * DaysPerMonth; // 252
    private const int VolWindow = 60;
    private const int Dpy = 252;

    private static Symbol Sym(string ticker) => new(ticker);

    private static TimeSeriesMomentumSignal DefaultSignal()
        => new(LookbackMonths, DaysPerMonth, VolWindow, Dpy);

    /// <summary>
    /// Builds an alternating return series of <paramref name="length"/> days: even indices
    /// <c>mean + dispersion</c>, odd indices <c>mean − dispersion</c>. Over any even-length trailing
    /// window the sample mean is exactly <paramref name="mean"/> and the per-observation deviation is
    /// exactly ±<paramref name="dispersion"/>, so the realized volatility scales linearly in
    /// <paramref name="dispersion"/> — the lever the inverse-vol tests need. With <c>mean &gt; 0</c> the
    /// trailing cumulative return is positive (up-trend) for the small dispersions used here; with
    /// <c>mean &lt; 0</c> it is negative (down-trend).
    /// </summary>
    private static decimal[] Alternating(decimal mean, decimal dispersion, int length)
    {
        var series = new decimal[length];
        for (var i = 0; i < length; i++)
        {
            series[i] = mean + (i % 2 == 0 ? dispersion : -dispersion);
        }

        return series;
    }

    // ── Wiring (anti-inertness): the signal drives a real tilt through SignalTiltConstruction ──────
    // Composed into the production decorator over an equal-weight base: the up-trend asset (positive
    // score) must end HEAVIER than the down-trend asset (zero score). With an inert/empty signal the
    // base equal weights survive and the two weights are equal — that is the RED.
    [Fact]
    public void ComputeScores_WiredThroughSignalTiltConstruction_TiltsTowardUpTrendAsset()
    {
        var assets = new[] { Sym("UP"), Sym("DOWN") };
        var returns = new[]
        {
            Alternating(0.0010m, 0.004m, LookbackDays),  // up-trend
            Alternating(-0.0010m, 0.004m, LookbackDays), // down-trend
        };

        var baseModel = new EqualWeightConstruction();
        var tilted = new SignalTiltConstruction(baseModel, [(DefaultSignal(), 0.10m)]);

        var weights = tilted.ComputeTargetWeights(assets, returns);

        Assert.True(weights[assets[0]] > weights[assets[1]],
            "UP (positive TSMOM score) must be weighted strictly above DOWN (zero score) — the signal must be wired, not inert");
    }

    // ── Fidelity: sign rule — up-trend scores strictly positive, down-trend clamps to exactly zero ──
    [Fact]
    public void ComputeScores_SignRule_UpTrendPositive_DownTrendZero()
    {
        var assets = new[] { Sym("UP"), Sym("DOWN") };
        var returns = new[]
        {
            Alternating(0.0010m, 0.004m, LookbackDays),
            Alternating(-0.0010m, 0.004m, LookbackDays),
        };

        var scores = DefaultSignal().ComputeScores(assets, returns);

        Assert.True(scores[assets[0]] > 0m, "up-trend asset must score strictly positive");
        Assert.Equal(0m, scores[assets[1]]); // down-trend clamped to zero (long-only)
    }

    // ── Fidelity: inverse-vol magnitude — same trend, 2× volatility ⇒ exactly half the score ───────
    // Both assets are up-trend (same positive mean), so both carry the same sign (+); the ONLY
    // difference is dispersion. Asset HIVOL's per-day deviations are exactly 2× LOVOL's, so its
    // RealizedVolatility is exactly 2× — and the inverse-vol score must therefore be exactly half.
    [Fact]
    public void ComputeScores_InverseVolMagnitude_DoubleVolatilityHalvesScore()
    {
        var assets = new[] { Sym("LOVOL"), Sym("HIVOL") };
        var returns = new[]
        {
            Alternating(0.0010m, 0.003m, LookbackDays), // dispersion d
            Alternating(0.0010m, 0.006m, LookbackDays), // dispersion 2d  ⇒ σ_HIVOL = 2·σ_LOVOL
        };

        var scores = DefaultSignal().ComputeScores(assets, returns);

        var lo = scores[assets[0]];
        var hi = scores[assets[1]];

        Assert.True(lo > 0m, "LOVOL is up-trend and must score positive");
        Assert.True(hi > 0m, "HIVOL is up-trend and must score positive");
        // Inverse proportionality to σ: 2× volatility ⇒ ½ the score.
        Assert.True(Math.Abs(hi - lo / 2m) < 1e-9m,
            $"HIVOL score ({hi}) must be exactly half LOVOL score ({lo}) — magnitude ∝ 1/σ");
    }

    // ── Fidelity: risk-free excess (aux key present) vs raw fallback (aux key absent) ───────────────
    // A single asset whose RAW trailing return is positive (would score long) but whose EXCESS return
    // over the risk-free rate is negative (the risk-free cumulative exceeds the asset's). With the
    // DGS3MO risk-free series present the excess sign wins ⇒ score clamps to 0; with the aux key absent
    // the signal falls back to the raw return ⇒ the same asset scores positive. Both paths in one test.
    [Fact]
    public void ComputeScores_RiskFreeExcess_PresentClampsToZero_AbsentFallsBackToRaw()
    {
        var assets = new[] { Sym("A") };
        var returns = new[] { Alternating(0.0001m, 0.002m, LookbackDays) }; // small positive raw trend

        // Risk-free daily return whose cumulative over the window strictly exceeds the asset's raw
        // trailing return, so the EXCESS return is negative.
        var rf = new decimal[LookbackDays];
        for (var i = 0; i < rf.Length; i++)
        {
            rf[i] = 0.0003m;
        }

        var aux = new Dictionary<Symbol, decimal[]> { [Sym("DGS3MO")] = rf };

        var signal = DefaultSignal();

        // Aux key present ⇒ excess return is negative ⇒ long-only clamp to exactly zero.
        var withRf = signal.ComputeScores(assets, returns, aux);
        Assert.Equal(0m, withRf[assets[0]]);

        // Aux key absent ⇒ documented raw-return fallback ⇒ the same asset scores strictly positive.
        var withoutRf = signal.ComputeScores(assets, returns);
        Assert.True(withoutRf[assets[0]] > 0m,
            "with the risk-free key absent the signal must fall back to the raw trailing return (positive here)");
    }

    // ── Contract: empty asset list yields an empty dictionary ──────────────────────────────────────
    [Fact]
    public void ComputeScores_EmptyAssetList_ReturnsEmptyDictionary()
    {
        var scores = DefaultSignal().ComputeScores([], []);
        Assert.Empty(scores);
    }

    // ── Degenerate: an up-trend asset with zero realized volatility is omitted ──────────────────────
    [Fact]
    public void ComputeScores_ZeroVolatilityUpTrend_OmitsAsset()
    {
        var assets = new[] { Sym("FLATUP"), Sym("GOOD1"), Sym("GOOD2") };
        var constant = new decimal[LookbackDays];
        for (var i = 0; i < constant.Length; i++)
        {
            constant[i] = 0.001m; // constant positive ⇒ up-trend, zero variance ⇒ σ = 0
        }

        var returns = new[]
        {
            constant,
            Alternating(0.0010m, 0.004m, LookbackDays),
            Alternating(0.0015m, 0.006m, LookbackDays),
        };

        var scores = DefaultSignal().ComputeScores(assets, returns);

        Assert.DoesNotContain(assets[0], scores.Keys);
        Assert.Contains(assets[1], scores.Keys);
        Assert.Contains(assets[2], scores.Keys);
        Assert.Equal(2, scores.Count);
    }

    [Fact]
    public void Constructors_LoggerlessAndNullLoggerInjected_ProduceIdenticalScores()
    {
        var assets = new[] { Sym("A"), Sym("B") };
        var returns = new[]
        {
            Alternating(0.0010m, 0.004m, LookbackDays),
            Alternating(-0.0010m, 0.004m, LookbackDays),
        };

        var loggerless = new TimeSeriesMomentumSignal(2, 5, 4, 252, "DGS3MO");
        var injected = new TimeSeriesMomentumSignal(
            NullLogger<TimeSeriesMomentumSignal>.Instance, 2, 5, 4, 252, "DGS3MO");

        Assert.Equal(loggerless.ComputeScores(assets, returns), injected.ComputeScores(assets, returns));
    }

    [Fact]
    public void ComputeScores_ThreeDegenerateAssets_LogsOneWarningNamingAllAssets()
    {
        var assets = new[] { Sym("FLAT1"), Sym("FLAT2"), Sym("FLAT3") };
        var returns = assets.Select(_ => Enumerable.Repeat(0.001m, LookbackDays).ToArray()).ToArray();
        var logger = new CapturingLogger<TimeSeriesMomentumSignal>();
        var signal = new TimeSeriesMomentumSignal(logger, 2, 5, 4, 252, "DGS3MO");

        _ = signal.ComputeScores(assets, returns);

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.All(assets, asset => Assert.Contains(asset.ToString(), warning.Message, StringComparison.Ordinal));
        Assert.Contains("zero realized volatility", warning.Message, StringComparison.Ordinal);
    }

    // ── Degenerate short-circuit: a zero-vol DOWN-trend asset scores 0 without needing σ (no throw) ──
    [Fact]
    public void ComputeScores_ZeroVolatilityDownTrend_ScoresZeroWithoutThrowing()
    {
        var assets = new[] { Sym("FLATDOWN") };
        var constant = new decimal[LookbackDays];
        for (var i = 0; i < constant.Length; i++)
        {
            constant[i] = -0.001m; // constant negative ⇒ down-trend, σ = 0 but never divided
        }

        var scores = DefaultSignal().ComputeScores(assets, [constant]);

        Assert.Equal(0m, scores[assets[0]]);
    }

    // ── Contract: insufficient-history — a series shorter than the required window is omitted ───────
    [Fact]
    public void ComputeScores_InsufficientHistory_OmitsShortSeries()
    {
        var assets = new[] { Sym("LONG"), Sym("SHORT") };
        var returns = new[]
        {
            Alternating(0.0010m, 0.004m, LookbackDays),  // enough history
            Alternating(0.0010m, 0.004m, LookbackDays - 1), // one day short of the lookback window
        };

        var scores = DefaultSignal().ComputeScores(assets, returns);

        Assert.True(scores.ContainsKey(assets[0]), "LONG has enough history and must be scored");
        Assert.False(scores.ContainsKey(assets[1]), "SHORT is one day short and must be omitted (absent key)");
        Assert.Single(scores);
    }
}
