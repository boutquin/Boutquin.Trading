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

namespace Boutquin.Trading.Application.Signals;

/// <summary>
/// Cross-sectional (relative) momentum signal: scores each asset by its trailing "12-1" cumulative
/// return — a 12-month lookback with the most recent month excluded — so that
/// <c>SignalTiltConstruction</c> tilts a long-only portfolio toward past winners
/// (Jegadeesh &amp; Titman, 1993).
/// </summary>
/// <remarks>
/// <para><b>Reference:</b> Jegadeesh, N. &amp; Titman, S. (1993), "Returns to Buying Winners and Selling
/// Losers: Implications for Stock Market Efficiency," <i>The Journal of Finance</i>, 48(1), 65–91. The
/// paper ranks securities by trailing <c>J</c>-month cumulative return and buys the top decile / sells
/// the bottom decile, optionally skipping a short period between formation and holding to purge
/// short-term reversal and microstructure effects from the ranking.</para>
/// <para><b>Adaptation (this codebase):</b> the signal operationalizes the same relative-strength
/// ranking principle as the standard <b>12-1</b> convention — a 12-month lookback with the most recent
/// month excluded. This skip-month is the standard <i>modern</i> operationalization of J&amp;T's
/// reversal-avoidance principle (the Carhart 1997 / Fama–French UMD practitioner lineage); it is
/// <b>not</b> a literal J&amp;T parameter — their own skip was <b>one week</b>, not one month. The
/// signal inherits only J&amp;T's ranking property (higher trailing 12-1 return ⇒ higher score) and the
/// reversal rationale for excluding the most recent period; it is a long-only per-asset <i>score</i>,
/// not the paper's long/short decile portfolio.</para>
/// <para><b>Score:</b> for each asset the score is its trailing cumulative return over the window
/// <c>[len − lookbackMonths·dpm, len − skipMonths·dpm)</c> (in trading-day indices), computed by the
/// shared <see cref="MomentumScore"/> indicator — the momentum arithmetic is <b>not</b> duplicated
/// here. Scores are <b>rank-monotone</b>: a strictly higher trailing 12-1 return yields a strictly
/// higher score.</para>
/// <para><b>Key space:</b> the returned dictionary is keyed only by per-asset <see cref="Symbol"/>
/// identifiers (one entry per scored asset). This signal reads no auxiliary series — the
/// <c>auxiliarySeries</c> argument is ignored.</para>
/// <para><b>Insufficient-history contract:</b> an asset whose return series is shorter than the
/// lookback (<c>lookbackMonths·tradingDaysPerMonth</c> days) is <b>omitted</b> from the result (its key
/// is absent) — never given a fabricated score and never a thrown exception, per
/// <see cref="ICrossSectionalSignal"/>. An empty asset list yields an empty dictionary.</para>
/// <para><b>Thread-safety:</b> stateless and immutable after construction; a single instance may score
/// independent cross-sections concurrently.</para>
/// </remarks>
public sealed class CrossSectionalMomentumSignal : ICrossSectionalSignal
{
    private readonly MomentumScore _momentum;
    private readonly int _requiredDays;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrossSectionalMomentumSignal"/> class with the
    /// lookback and skip windows that parameterize the underlying <see cref="MomentumScore"/> (default
    /// 12-1 momentum: a 12-month lookback with the most recent month excluded).
    /// </summary>
    /// <param name="lookbackMonths">
    /// Total lookback in months (the <c>J</c> formation window). Must be positive.
    /// </param>
    /// <param name="skipMonths">
    /// The most recent months to exclude from the window (the modern skip-month operationalization of
    /// J&amp;T's reversal-avoidance principle). Must be positive and strictly less than
    /// <paramref name="lookbackMonths"/>.
    /// </param>
    /// <param name="tradingDaysPerMonth">
    /// Trading days per month, used to convert the month-denominated windows to daily indices. Must be
    /// positive.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when any argument is non-positive, or when <paramref name="skipMonths"/> is not strictly
    /// less than <paramref name="lookbackMonths"/> (validated by <see cref="MomentumScore"/>).
    /// </exception>
    public CrossSectionalMomentumSignal(int lookbackMonths = 12, int skipMonths = 1, int tradingDaysPerMonth = 21)
    {
        // MomentumScore validates lookbackMonths > 0, skipMonths > 0, skipMonths < lookbackMonths,
        // tradingDaysPerMonth > 0 — reuse its guards rather than duplicating them.
        _momentum = new MomentumScore(lookbackMonths, skipMonths, tradingDaysPerMonth);
        _requiredDays = lookbackMonths * tradingDaysPerMonth;
    }

    /// <summary>
    /// Scores each asset by its trailing 12-1 cumulative return, omitting any asset whose return series
    /// is shorter than the lookback window.
    /// </summary>
    /// <param name="assets">
    /// The ordered list of assets to score; <c>assets[i]</c> corresponds to <c>returns[i]</c>. May be
    /// empty (yields an empty result).
    /// </param>
    /// <param name="returns">
    /// A jagged array of per-asset daily-return series, positionally aligned with
    /// <paramref name="assets"/>. A <see langword="null"/> or too-short series causes that asset to be
    /// omitted (see the insufficient-history contract on <see cref="ICrossSectionalSignal"/>).
    /// </param>
    /// <param name="auxiliarySeries">
    /// Ignored — cross-sectional momentum reads only price returns.
    /// </param>
    /// <returns>
    /// A dictionary mapping each scored asset to its trailing 12-1 return. Assets with insufficient
    /// history are absent (never assigned a fabricated score). Empty (never <see langword="null"/>) when
    /// <paramref name="assets"/> is empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assets"/> or <paramref name="returns"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="returns"/> has a different length than <paramref name="assets"/> — a
    /// positional-alignment error surfaced rather than silently truncated.
    /// </exception>
    /// <remarks>
    /// <b>Complexity:</b> O(N·W) for N assets over the W-day scored window (each asset's score is a
    /// single pass over its window via <see cref="MomentumScore.Compute"/>).
    /// </remarks>
    public IReadOnlyDictionary<Symbol, decimal> ComputeScores(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null)
    {
        Guard.AgainstNull(() => assets);
        Guard.AgainstNull(() => returns);

        if (returns.Length != assets.Count)
        {
            throw new ArgumentException(
                $"returns length ({returns.Length}) must match assets count ({assets.Count}).",
                nameof(returns));
        }

        var scores = new Dictionary<Symbol, decimal>(assets.Count);
        for (var i = 0; i < assets.Count; i++)
        {
            var series = returns[i];

            // Insufficient-history contract: omit (absent key), never fabricate, never throw.
            if (series is null || series.Length < _requiredDays)
            {
                continue;
            }

            scores[assets[i]] = _momentum.Compute(series);
        }

        return scores;
    }
}
