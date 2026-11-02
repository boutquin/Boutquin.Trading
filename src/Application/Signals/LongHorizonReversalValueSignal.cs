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

namespace Boutquin.Trading.Application.Signals;

/// <summary>
/// Long-horizon-reversal value signal: scores each asset by the <b>negative of its trailing five-year
/// cumulative return</b> (months <c>t−60…t−0</c>, run through the current price with <b>no skip at the
/// recent end</b>), so that <c>SignalTiltConstruction</c> tilts a long-only portfolio toward
/// long-horizon losers — the asset-class value proxy of Asness, Moskowitz &amp; Pedersen (2013) for
/// markets where fundamental book-value data is unavailable.
/// </summary>
/// <remarks>
/// <para><b>Reference:</b> Asness, C.S., Moskowitz, T.J. &amp; Pedersen, L.H. (2013), "Value and Momentum
/// Everywhere," <i>Journal of Finance</i>, 68(3), 929–985. Where fundamentals (book-to-market) are
/// unavailable, the paper proxies value by the negative of the past five-year return: for commodities,
/// "the log of the spot price 5 years ago … divided by the most recent spot price, which is essentially
/// the negative of the spot return over the last 5 years" (p. 937); for currencies, the average rate
/// "from 4.5 to 5.5 years ago divided by the spot exchange rate today" (p. 937). Fama and French (1996)
/// show the negative of the past five-year return forms portfolios highly correlated with book-to-market
/// (p. 938), which licenses it as a long-horizon-reversal value proxy.</para>
/// <para><b>Value window — <c>t−60…t−0</c>, NO skip at the recent end (MATERIAL):</b> the five-year
/// window runs through the <b>current</b> price; the far endpoint is the average of 4.5–5.5 years ago and
/// the near endpoint is <b>today</b> — there is <b>no skip-month</b>. This is the paper's own
/// <c>−0.48</c>-correlation robustness proxy (pp. 937–938, 950) and is the deliberate contrast with
/// <see cref="CrossSectionalMomentumSignal"/>, whose 12-1 window <i>does</i> exclude the most recent
/// month: a spike in the most recent month therefore <b>does</b> move the value score (it raises the 5y
/// return, so it lowers the value score), whereas it does not move the momentum score. The 1-year lag the
/// paper mentions elsewhere is a separate robustness check on the equity BE/ME price denominator (p. 950),
/// not this return-based proxy.</para>
/// <para><b>Score:</b> for each asset with at least <c>lookbackMonths·tradingDaysPerMonth</c> trailing
/// daily returns, <c>score = −(∏(1 + r_k) − 1)</c> compounded over the whole trailing window. A higher
/// trailing five-year return yields a <b>strictly lower</b> (more negative) value score — the reversal
/// property.</para>
/// <para><b>Key space:</b> the returned dictionary is keyed only by per-asset <see cref="Symbol"/>
/// identifiers (one entry per scored asset). This signal reads no auxiliary series — the
/// <c>auxiliarySeries</c> argument is ignored.</para>
/// <para><b>Insufficient-history contract:</b> an asset whose return series is shorter than the window
/// (<c>lookbackMonths·tradingDaysPerMonth</c> days) is <b>omitted</b> from the result (its key is absent)
/// — never given a fabricated score and never a thrown exception, per <see cref="ICrossSectionalSignal"/>.
/// A three-year-old ETF inside a five-year-lookback signal is a normal state. An empty asset list yields
/// an empty dictionary.</para>
/// <para><b>Known intended-omission (long-only adaptation):</b> the paper's long–short market-neutral
/// construction (high-minus-low) is not implemented — this is a long-only per-asset <i>score</i>, no short
/// leg. Fundamental book-to-market value is out of scope; only the return-based long-horizon-reversal
/// proxy ships.</para>
/// <para><b>Thread-safety:</b> stateless and immutable after construction; a single instance may score
/// independent cross-sections concurrently.</para>
/// </remarks>
public sealed class LongHorizonReversalValueSignal : ICrossSectionalSignal
{
    private readonly int _requiredDays;

    /// <summary>
    /// Initializes a new instance of the <see cref="LongHorizonReversalValueSignal"/> class with the
    /// lookback that defines the value window (default: a 60-month — five-year — window, no skip).
    /// </summary>
    /// <param name="lookbackMonths">
    /// The trailing lookback in months over which the cumulative return is measured (the paper's
    /// five-year — 60-month — horizon). Must be positive.
    /// </param>
    /// <param name="tradingDaysPerMonth">
    /// Trading days per month, used to convert the month-denominated window to a daily count. Must be
    /// positive.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="lookbackMonths"/> or <paramref name="tradingDaysPerMonth"/> is
    /// non-positive.
    /// </exception>
    public LongHorizonReversalValueSignal(int lookbackMonths = 60, int tradingDaysPerMonth = 21)
    {
        Guard.AgainstNegativeOrZero(() => lookbackMonths);
        Guard.AgainstNegativeOrZero(() => tradingDaysPerMonth);

        _requiredDays = lookbackMonths * tradingDaysPerMonth;
    }

    /// <summary>
    /// Scores each asset by the negative of its trailing five-year cumulative return, omitting any asset
    /// whose return series is shorter than the value window.
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
    /// Ignored — the long-horizon-reversal value proxy reads only price returns.
    /// </param>
    /// <returns>
    /// A dictionary mapping each scored asset to the negative of its trailing five-year cumulative return.
    /// Assets with insufficient history are absent (never assigned a fabricated score). Empty (never
    /// <see langword="null"/>) when <paramref name="assets"/> is empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assets"/> or <paramref name="returns"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="returns"/> has a different length than <paramref name="assets"/> — a
    /// positional-alignment error surfaced rather than silently truncated.
    /// </exception>
    /// <remarks>
    /// <b>Complexity:</b> O(N·W) for N assets over the W-day value window (each asset's score is a single
    /// pass over its trailing window).
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

            // Value = negative of the trailing five-year cumulative return, run through the CURRENT price
            // (the window's near endpoint is today — NO skip at the recent end, per Asness et al. 2013).
            var cumulative = 1m;
            var start = series.Length - _requiredDays;
            for (var k = start; k < series.Length; k++)
            {
                cumulative *= 1m + series[k];
            }

            scores[assets[i]] = -(cumulative - 1m);
        }

        return scores;
    }
}
