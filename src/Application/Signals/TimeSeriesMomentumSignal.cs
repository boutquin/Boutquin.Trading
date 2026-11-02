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
/// Time-series (trend) momentum signal: scores each asset by the <b>sign of its trailing 12-month
/// excess return</b> multiplied by the <b>inverse of its ex-ante annualized volatility</b>, so that
/// <c>SignalTiltConstruction</c> tilts a long-only portfolio toward trending, low-volatility assets
/// (Moskowitz, Ooi &amp; Pedersen, 2012).
/// </summary>
/// <remarks>
/// <para><b>Reference:</b> Moskowitz, T.J., Ooi, Y.H. &amp; Pedersen, L.H. (2012), "Time Series
/// Momentum," <i>Journal of Financial Economics</i>, 104(2), 228–250 — specifically the per-instrument
/// position-sizing rule of Eq. 5 (§3.2, p. 235):
/// <c>r^{TSMOM,s}_{t,t+1} = sign(r^s_{t−12,t}) · (40%/σ^s_t) · r^s_{t,t+1}</c>. Trend is detected on the
/// sign of the trailing 12-month <i>excess</i> return (excess over the Treasury-bill rate, §2, p. 232),
/// and position size is scaled by the inverse of an ex-ante annualized volatility (Eq. 1, §2.4, p. 233).
/// The 40% target-volatility constant is an arbitrary normalization the paper itself calls
/// "inconsequential" (p. 234); the invariant this signal preserves is the <b>inverse proportionality to
/// σ</b>, not the 40% level, so no normalization constant is applied.</para>
/// <para><b>Score:</b> for each asset <c>score = effectiveSign · (1 / σ)</c>, where
/// <c>effectiveSign</c> is <c>1</c> when the trailing 12-month excess return is strictly positive and
/// <c>0</c> otherwise (see the long-only adaptation below), and <c>σ</c> is the ex-ante annualized
/// volatility from the reused <see cref="RealizedVolatility"/> indicator (the momentum arithmetic and
/// the volatility arithmetic are <b>not</b> duplicated here). The magnitude is therefore
/// <b>inversely proportional to σ</b>: two up-trend assets whose volatilities differ by a factor of two
/// receive scores differing by exactly that factor.</para>
/// <para><b>Long-only adaptation (repo-specific):</b> the paper goes long <i>or short</i> — "go long the
/// contract if positive and short if negative" (§3.2, p. 234) — because futures support costless
/// shorting. This repo's ETF-tilt context is long-only, so a negative-sign (down-trend) asset tilts its
/// score toward <b>zero</b> (clamped at 0), never negative. An up-trend asset scores strictly positive;
/// the score therefore "flips" at exactly the trailing-excess-return zero boundary (positive ⇒ a
/// positive inverse-vol score; zero or negative ⇒ zero).</para>
/// <para><b>Excess return &amp; the risk-free key (repo missing-input contract):</b> the risk-free leg of
/// the excess return is sourced from <c>auxiliarySeries</c> under a well-known market-wide series key
/// (default <c>DGS3MO</c>, the FRED 3-month Treasury series Recipes' <c>EconomicSeriesIds</c> fetches).
/// The series is interpreted as a daily risk-free-return series; its cumulative return over the trailing
/// 12-month window is subtracted from the asset's cumulative trailing return to form the excess return
/// whose sign drives the position. <b>Deviation — when the risk-free key is absent (or its series is
/// shorter than the lookback), the signal falls back to the raw (non-excess) trailing return</b> and
/// documents that here; this is the signal's documented missing-market-wide-key contract, <i>not</i> the
/// insufficient-history omission (which applies only to a per-asset series that is too short).</para>
/// <para><b>Key space:</b> the returned dictionary is keyed only by per-asset <see cref="Symbol"/>
/// identifiers (one entry per scored asset). The only <c>auxiliarySeries</c> key this signal reads is the
/// market-wide risk-free series id (default <c>DGS3MO</c>); auxiliary series are an input channel, never
/// an output key.</para>
/// <para><b>Insufficient-history contract:</b> an asset whose return series is shorter than the required
/// window (the larger of the 12-month lookback and the volatility window, in trading days) is
/// <b>omitted</b> from the result (its key is absent) — never given a fabricated score and never a thrown
/// exception, per <see cref="ICrossSectionalSignal"/>. An empty asset list yields an empty dictionary.</para>
/// <para><b>Degenerate input:</b> an up-trend asset with zero realized volatility (σ = 0) makes the
/// inverse-vol size undefined. The signal omits that asset instead of fabricating an infinite score and
/// includes it in one aggregated Warning record for the call. Other assets remain scoreable. A
/// down-trend or flat asset scores exactly zero without requiring a volatility estimate.</para>
/// <para><b>Thread-safety:</b> stateless and immutable after construction; a single instance may score
/// independent cross-sections concurrently.</para>
/// </remarks>
public sealed class TimeSeriesMomentumSignal : ICrossSectionalSignal
{
    private readonly RealizedVolatility _realizedVolatility;
    private readonly Symbol _riskFreeSeriesId;
    private readonly int _lookbackDays;
    private readonly int _requiredDays;
    private readonly ILogger<TimeSeriesMomentumSignal> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSeriesMomentumSignal"/> class using a
    /// <see cref="NullLogger{T}"/> for diagnostics.
    /// </summary>
    /// <param name="lookbackMonths">
    /// The trailing lookback in months over which the trend (excess-return sign) is measured (the paper's
    /// 12-month horizon). Must be positive.
    /// </param>
    /// <param name="tradingDaysPerMonth">
    /// Trading days per month, used to convert the month-denominated lookback to a daily window. Must be
    /// positive.
    /// </param>
    /// <param name="volatilityWindow">
    /// The number of trailing daily returns used by <see cref="RealizedVolatility"/> for the ex-ante
    /// volatility estimate (the paper uses an EWMA with a ~60-day center of mass; a rolling window of the
    /// same order is the faithful substitute). Must be at least 2.
    /// </param>
    /// <param name="tradingDaysPerYear">
    /// Annualization factor for the volatility estimate. Must be positive. Defaults to 252.
    /// </param>
    /// <param name="riskFreeSeriesId">
    /// The market-wide <c>auxiliarySeries</c> key holding the daily risk-free-return series used to form
    /// the excess return (default <c>DGS3MO</c>, the FRED 3-month Treasury series). Must be non-empty.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="lookbackMonths"/> or <paramref name="tradingDaysPerMonth"/> is
    /// non-positive.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="volatilityWindow"/> is less than 2 or
    /// <paramref name="tradingDaysPerYear"/> is non-positive (validated by
    /// <see cref="RealizedVolatility"/>).
    /// </exception>
    public TimeSeriesMomentumSignal(
        int lookbackMonths = 12,
        int tradingDaysPerMonth = 21,
        int volatilityWindow = 60,
        int tradingDaysPerYear = 252,
        string riskFreeSeriesId = "DGS3MO")
        : this(
            NullLogger<TimeSeriesMomentumSignal>.Instance,
            lookbackMonths,
            tradingDaysPerMonth,
            volatilityWindow,
            tradingDaysPerYear,
            riskFreeSeriesId)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSeriesMomentumSignal"/> class with a logger for
    /// aggregated per-call degeneracy diagnostics.
    /// </summary>
    /// <param name="logger">The logger that receives aggregated degeneracy warnings.</param>
    /// <param name="lookbackMonths">The trailing momentum lookback in months.</param>
    /// <param name="tradingDaysPerMonth">The number of trading days used to represent one month.</param>
    /// <param name="volatilityWindow">The trailing return count used for realized volatility.</param>
    /// <param name="tradingDaysPerYear">The annualization factor for realized volatility.</param>
    /// <param name="riskFreeSeriesId">The market-wide risk-free auxiliary-series identifier.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="lookbackMonths"/> or <paramref name="tradingDaysPerMonth"/> is
    /// non-positive.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="volatilityWindow"/> is less than 2 or
    /// <paramref name="tradingDaysPerYear"/> is non-positive.
    /// </exception>
    public TimeSeriesMomentumSignal(
        ILogger<TimeSeriesMomentumSignal> logger,
        int lookbackMonths = 12,
        int tradingDaysPerMonth = 21,
        int volatilityWindow = 60,
        int tradingDaysPerYear = 252,
        string riskFreeSeriesId = "DGS3MO")
    {
        Guard.AgainstNegativeOrZero(() => lookbackMonths);
        Guard.AgainstNegativeOrZero(() => tradingDaysPerMonth);
        Guard.AgainstNull(() => logger);

        // RealizedVolatility validates volatilityWindow >= 2 and tradingDaysPerYear > 0 — reuse its
        // guards (and its variance/annualization arithmetic) rather than duplicating them.
        _realizedVolatility = new RealizedVolatility(volatilityWindow, tradingDaysPerYear);
        _riskFreeSeriesId = new Symbol(riskFreeSeriesId);
        _logger = logger;
        _lookbackDays = lookbackMonths * tradingDaysPerMonth;

        // A scored asset must have enough history for BOTH the trend window and the volatility window.
        _requiredDays = Math.Max(_lookbackDays, volatilityWindow);
    }

    /// <inheritdoc />
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
        var degenerateAssets = new List<Symbol>();
        for (var i = 0; i < assets.Count; i++)
        {
            var series = returns[i];

            // Insufficient-history contract: omit (absent key), never fabricate, never throw.
            if (series is null || series.Length < _requiredDays)
            {
                continue;
            }

            // Trailing 12-month excess return whose SIGN drives the position (see TrailingExcessReturn).
            var trend = TrailingExcessReturn(series, auxiliarySeries);

            // Long-only adaptation: a down-trend or flat asset tilts toward zero (never negative), so it
            // gets a score of exactly 0 and no inverse-vol size is needed.
            if (trend <= 0m)
            {
                scores[assets[i]] = 0m;
                continue;
            }

            // Up-trend: position size ∝ 1 / σ, the paper's ex-ante inverse-volatility scaling. σ is the
            // annualized realized volatility from the reused RealizedVolatility indicator (its variance
            // and annualization arithmetic are NOT duplicated here).
            var sigma = _realizedVolatility.Compute(series);
            if (sigma <= 0m)
            {
                degenerateAssets.Add(assets[i]);
                continue;
            }

            scores[assets[i]] = 1m / sigma;
        }

        if (degenerateAssets.Count > 0)
        {
            _logger.LogWarning(
                "TimeSeriesMomentumSignal omitted {DegenerateCount} asset(s) with zero realized volatility: {DegenerateAssets}",
                degenerateAssets.Count,
                string.Join(", ", degenerateAssets));
        }

        return scores;
    }

    /// <summary>
    /// Computes the trailing excess return over the lookback window whose sign drives the position: the
    /// asset's cumulative return over the trailing <see cref="_lookbackDays"/> days, less the cumulative
    /// risk-free return over the same window when the risk-free auxiliary series is present and long
    /// enough. Falls back to the raw (non-excess) trailing return otherwise (the documented
    /// missing-market-wide-key deviation).
    /// </summary>
    private decimal TrailingExcessReturn(
        decimal[] series,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries)
    {
        var assetReturn = CumulativeReturn(series, _lookbackDays);

        // Excess over the risk-free rate when the market-wide risk-free series is supplied and long
        // enough to cover the trailing window; otherwise the documented raw-return fallback (a missing —
        // or too-short — risk-free series is NOT a per-asset omission, it degrades the whole signal to
        // raw trend for every asset consistently).
        if (auxiliarySeries is not null
            && auxiliarySeries.TryGetValue(_riskFreeSeriesId, out var riskFree)
            && riskFree is not null
            && riskFree.Length >= _lookbackDays)
        {
            var riskFreeReturn = CumulativeReturn(riskFree, _lookbackDays);
            return assetReturn - riskFreeReturn;
        }

        return assetReturn;
    }

    /// <summary>
    /// Cumulative compounded return over the trailing <paramref name="window"/> observations of
    /// <paramref name="series"/>: <c>∏(1 + r_k) − 1</c>. The caller guarantees
    /// <c>series.Length ≥ window</c>.
    /// </summary>
    private static decimal CumulativeReturn(decimal[] series, int window)
    {
        var start = series.Length - window;
        var cumulative = 1m;
        for (var k = start; k < series.Length; k++)
        {
            cumulative *= 1m + series[k];
        }

        return cumulative - 1m;
    }
}
