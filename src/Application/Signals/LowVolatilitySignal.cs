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
/// Low-volatility-anomaly signal: scores each asset by the <b>negative cross-sectional z-score of its own
/// trailing realized volatility</b> (<c>score = −z(realized σ)</c>), so that <c>SignalTiltConstruction</c>
/// tilts a long-only portfolio <b>toward individually low-volatility assets</b> (Baker, Bradley &amp;
/// Wurgler, 2011).
/// </summary>
/// <remarks>
/// <para><b>Reference:</b> Baker, M.P., Bradley, B. &amp; Wurgler, J. (2011), "Benchmarks as Limits to
/// Arbitrage: Understanding the Low-Volatility Anomaly," <i>Financial Analysts Journal</i>, 67(1), 40–54.
/// The paper sorts stocks each month into quintiles by their <b>trailing total volatility</b> (a five-year
/// trailing standard deviation of returns) and finds that the low-volatility quintiles delivered markedly
/// higher long-run risk-adjusted returns than the high-volatility quintiles over 1968–2008, contrary to
/// the CAPM. This signal is the continuous-score restatement of that <b>characteristic sort</b>: it
/// negates and cross-sectionally z-scores the very quantity the paper ranks on (each asset's own realized
/// σ), rather than bucketing into quintiles.</para>
/// <para><b>Score:</b> for a single rebalance, each scored asset's trailing realized volatility is measured
/// by the reused <see cref="RealizedVolatility"/> indicator (the variance/annualization arithmetic is
/// <b>not</b> duplicated here); the surviving vols are then cross-sectionally z-scored (population mean / σ
/// across exactly the assets scored that rebalance) and <b>negated</b>. A strictly higher realized σ
/// therefore yields a strictly <b>lower</b> score — the tilt toward low-vol assets — and two assets with
/// equal realized σ receive equal scores. Because a z-score is invariant to a positive scale factor, the
/// annualization constant baked into <see cref="RealizedVolatility"/> cancels and only the cross-sectional
/// ranking of raw σ survives.</para>
/// <para><b>NOT portfolio-variance minimization (material distinction):</b> this signal is a
/// <b>cross-sectional characteristic sort of each asset by its OWN realized risk</b>. It is categorically
/// <b>not</b> <see cref="Boutquin.Trading.Application.PortfolioConstruction.MinimumVarianceConstruction"/>,
/// which minimizes <i>portfolio</i> variance <c>wᵀΣw</c> via the joint covariance matrix Σ and can hold an
/// individually volatile-but-diversifying asset at positive weight. The paper contains no covariance
/// matrix, no Markowitz optimization, and no portfolio-variance objective anywhere — its entire apparatus
/// is a per-asset rank on trailing own volatility. The two mechanisms can disagree on any given asset: a
/// high-own-vol, low-correlation asset scores <i>low</i> here yet may receive a <i>positive</i>
/// minimum-variance weight. This signal only tilts toward assets that are low-vol <i>in isolation</i>.</para>
/// <para><b>Key space:</b> the returned dictionary is keyed only by per-asset <see cref="Symbol"/>
/// identifiers (one entry per scored asset). This signal reads no auxiliary series — the
/// <c>auxiliarySeries</c> argument is ignored.</para>
/// <para><b>Insufficient-history contract:</b> an asset whose return series is shorter than the volatility
/// window is <b>omitted</b> from the result (its key is absent) — never given a fabricated score and never
/// a thrown exception, per <see cref="ICrossSectionalSignal"/>. A three-year-old ETF inside a five-year
/// window is a normal state. An empty asset list yields an empty dictionary. Omitted assets are excluded
/// from the cross-sectional mean/σ, so a short-history asset never distorts the scored assets' z-scores.</para>
/// <para><b>Degenerate input:</b> when the scored cross-section has zero dispersion (every scored asset
/// shares the same realized σ, so the z-score is undefined) the signal throws
/// <see cref="Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException"/>, surfacing the
/// degeneracy rather
/// than propagating a meaningless value — consistent with the internally-z-scoring sibling
/// <see cref="ValueMomentumCompositeSignal"/> and the repository's degenerate-input policy. A single scored
/// asset is one instance of this (its own value is the mean, so its dispersion is zero).</para>
/// <para><b>Known intended omission:</b> the low-<b>beta</b> form
/// (Frazzini–Pedersen betting-against-beta) is out of scope — this ships the low-<b>volatility</b> form
/// only, matching the paper's own primary sort variable (trailing total volatility). The paper's short
/// (high-vol) leg is not implemented — a long-only tilt, clamped at zero downstream by
/// <c>SignalTiltConstruction</c>. The idiosyncratic-vs-total-volatility distinction is not modeled;
/// this signal fixes on total realized volatility.</para>
/// <para><b>Thread-safety:</b> stateless and immutable after construction; a single instance may score
/// independent cross-sections concurrently.</para>
/// </remarks>
public sealed class LowVolatilitySignal : ICrossSectionalSignal
{
    private const decimal SigmaFloor = 1e-12m;

    private readonly RealizedVolatility _realizedVolatility;
    private readonly int _requiredDays;

    /// <summary>
    /// Initializes a new instance of the <see cref="LowVolatilitySignal"/> class with the trailing window
    /// and annualization factor of the reused <see cref="RealizedVolatility"/> estimate.
    /// </summary>
    /// <param name="volatilityWindow">
    /// The number of trailing daily returns used by <see cref="RealizedVolatility"/> for each asset's own
    /// realized-σ estimate (the paper's primary sort is a five-year trailing standard deviation; a rolling
    /// window is the faithful substitute). Also the minimum history an asset must have to be scored. Must
    /// be at least 2. Defaults to 60.
    /// </param>
    /// <param name="tradingDaysPerYear">
    /// Annualization factor for the volatility estimate. Must be positive. Defaults to 252. Because the
    /// score is a scale-invariant z-score, this factor does not change the ranking — it is threaded only to
    /// reuse <see cref="RealizedVolatility"/> unchanged.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="volatilityWindow"/> is less than 2 or
    /// <paramref name="tradingDaysPerYear"/> is non-positive (validated by
    /// <see cref="RealizedVolatility"/>).
    /// </exception>
    public LowVolatilitySignal(int volatilityWindow = 60, int tradingDaysPerYear = 252)
    {
        // RealizedVolatility validates volatilityWindow >= 2 and tradingDaysPerYear > 0 — reuse its guards
        // (and its variance/annualization arithmetic) rather than duplicating them.
        _realizedVolatility = new RealizedVolatility(volatilityWindow, tradingDaysPerYear);
        _requiredDays = volatilityWindow;
    }

    /// <summary>
    /// Scores each asset by the negative cross-sectional z-score of its trailing realized volatility,
    /// omitting any asset whose return series is shorter than the volatility window.
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
    /// Ignored — the low-volatility characteristic sort reads only price returns.
    /// </param>
    /// <returns>
    /// A dictionary mapping each scored asset to <c>−z(realized σ)</c> across the scored cross-section.
    /// Assets with insufficient history are absent (never assigned a fabricated score). Empty (never
    /// <see langword="null"/>) when <paramref name="assets"/> is empty or no asset has enough history.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assets"/> or <paramref name="returns"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="returns"/> has a different length than <paramref name="assets"/> — a
    /// positional-alignment error surfaced rather than silently truncated.
    /// </exception>
    /// <exception cref="Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException">
    /// Thrown when the scored cross-section has zero dispersion (every scored asset shares the same realized
    /// σ), so the z-score is undefined.
    /// </exception>
    /// <remarks>
    /// <b>Complexity:</b> O(N·W) for N assets over the W-day volatility window (each asset's realized σ is a
    /// single pass via <see cref="RealizedVolatility.Compute"/>; the z-score adds an O(N) pass over the
    /// scored assets).
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

        var scored = new List<Symbol>(assets.Count);
        var vols = new List<decimal>(assets.Count);
        for (var i = 0; i < assets.Count; i++)
        {
            var series = returns[i];

            // Insufficient-history contract: omit (absent key), never fabricate, never throw.
            if (series is null || series.Length < _requiredDays)
            {
                continue;
            }

            scored.Add(assets[i]);

            // Each asset's OWN trailing realized σ, from the reused RealizedVolatility indicator (its
            // variance/annualization arithmetic is NOT duplicated here).
            vols.Add(_realizedVolatility.Compute(series));
        }

        var scores = new Dictionary<Symbol, decimal>(scored.Count);
        var n = scored.Count;
        if (n == 0)
        {
            return scores;
        }

        // Cross-sectional (population) mean and standard deviation of the realized volatilities across the
        // assets scored this rebalance — the same population z-score SignalTiltConstruction and the
        // ValueMomentumCompositeSignal use.
        var mean = 0m;
        for (var i = 0; i < n; i++)
        {
            mean += vols[i];
        }

        mean /= n;

        var variance = 0m;
        for (var i = 0; i < n; i++)
        {
            var d = vols[i] - mean;
            variance += d * d;
        }

        variance /= n;

        var dispersion = (decimal)Math.Sqrt((double)variance);
        if (dispersion <= SigmaFloor)
        {
            throw new Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException(
                $"Cannot z-score realized volatility: its {n} scored assets have zero cross-sectional " +
                "variance (every scored asset shares the same realized σ), so the −z(σ) score is undefined.");
        }

        // score = −z(σ): a higher realized σ maps to a lower (more negative) score — the tilt TOWARD
        // individually low-vol assets, and equal-σ assets tie. This is a per-asset characteristic sort, NOT
        // portfolio-variance minimization over a covariance matrix (see the type remarks).
        for (var i = 0; i < n; i++)
        {
            scores[scored[i]] = -((vols[i] - mean) / dispersion);
        }

        return scores;
    }
}
