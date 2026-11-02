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
/// Carry signal: scores each asset by its <b>carry</b> — the return it earns if prices and curves stay
/// unchanged — using a long-only ETF adaptation of the carry model appropriate to the asset's class, so
/// that <c>SignalTiltConstruction</c> tilts a long-only portfolio toward high-carry assets (Koijen,
/// Moskowitz, Pedersen &amp; Vrugt, 2018).
/// </summary>
/// <remarks>
/// <para><b>Reference:</b> Koijen, R.S.J., Moskowitz, T.J., Pedersen, L.H. &amp; Vrugt, E.B. (2018),
/// "Carry," <i>Journal of Financial Economics</i>, 127(2), 197–225. The paper defines an asset's carry
/// as "its expected return assuming that its price does not change" (§I, eq. 1:
/// <c>E(return) = carry + E(price appreciation)</c>) — a model-free, ex-ante observable component of the
/// expected return, not the whole. This signal only <b>ranks</b> the cross-section by carry; a score is
/// <b>not</b> a total-return forecast (the expected-price-appreciation term of eq. 1 is never estimated).</para>
/// <para><b>Per-asset-class carry model (long-only ETF adaptations).</b> Carry takes a different
/// observable form per asset class, so the signal is constructed with a per-symbol
/// <see cref="CarryAssetClass"/> map (see <b>Asset-class dispatch</b> below) selecting the model applied
/// to each asset:</para>
/// <list type="bullet">
///   <item><description><b>Bond</b> (Koijen et al. eq. 13–15): under a constant term structure,
///   <c>carry = yield + roll-down</c>. Approximated from two observable term-structure points supplied as
///   <b>market-wide</b> auxiliary series (default the FRED <c>DGS10</c>/<c>DGS2</c> curve points) in place
///   of the paper's continuous curve: <c>carry = y_long + rollDownDuration · (y_long − y_short)</c>, where
///   <c>y_long</c>/<c>y_short</c> are the most recent observations of the long/short curve-point series.
///   The first term is the current yield (eq. 13: under constant price, carry = current yield); the second
///   is the roll-down — as the bond ages down an upward-sloping curve its yield falls and its price rises,
///   a positive carry contribution scaled by the modified-duration coefficient <c>rollDownDuration</c>
///   (eq. 15). Under a <b>flat</b> curve (<c>y_long = y_short</c>) the roll-down vanishes and carry equals
///   the yield exactly. This is the raw-yield form of eq. 13/15, not the excess-carry
///   <c>(y_long − r^f)</c> variant — the fidelity property the signal preserves is "carry = the yield",
///   not "carry = the yield in excess of the short rate".</description></item>
///   <item><description><b>Equity / REIT</b> (Koijen et al. eq. 8): <c>carry = trailing income (dividend)
///   yield</c>, the dividend-yield component of expected return under Gordon growth (<c>E(R) = D/S + g</c>,
///   carry = <c>D/S</c>). Supplied <b>per-asset</b> as an auxiliary series keyed by the asset's own symbol;
///   the most recent observation is the current trailing income yield. This is a repo proxy for the paper's
///   forward-looking dividend-yield-from-futures measure.</description></item>
///   <item><description><b>FX-hedged</b> (Koijen et al. eq. 7): <c>carry = (r_foreign − r_local) /
///   (1 + r_local)</c>, the interest-rate differential — the foreign short rate in excess of the local
///   risk-free rate. The foreign leg is supplied <b>per-asset</b> (keyed by the asset's own symbol); the
///   local leg is a <b>market-wide</b> auxiliary series (default <c>DGS3MO</c>). Both use their most recent
///   observation.</description></item>
/// </list>
/// <para><b>Asset-class dispatch:</b> the signal is constructed with an
/// <c>IReadOnlyDictionary&lt;Symbol, CarryAssetClass&gt;</c> that maps each asset to its class (the
/// constructor-bound precedent of <c>TacticalOverlayConstruction.currentRegime</c> — the caller builds the
/// signal per run with the current universe's class assignments). This is explicit and testable: an asset
/// <b>absent from the map</b> has no determinable carry model and is <b>omitted</b> from the scored
/// cross-section (the absent-key rule of <see cref="ICrossSectionalSignal"/>), rather than guessed at.</para>
/// <para><b>Key space:</b> the returned dictionary is keyed only by per-asset <see cref="Symbol"/>
/// identifiers (one entry per scored asset). The <c>auxiliarySeries</c> keys this signal reads are of two
/// kinds: <b>market-wide</b> series identifiers — the bond long/short curve points
/// (<c>longCurveSeriesId</c>/<c>shortCurveSeriesId</c>, default <c>DGS10</c>/<c>DGS2</c>) and the FX local
/// short rate (<c>localShortRateSeriesId</c>, default <c>DGS3MO</c>) — and <b>per-asset</b> series keyed by
/// the scored asset's own symbol (an equity/REIT asset's trailing income yield, or an FX-hedged asset's
/// foreign short rate). Auxiliary series are an input channel, never an output key.</para>
/// <para><b>Missing-series contract:</b> an asset lacking the auxiliary series its carry model requires is
/// <b>omitted</b> (absent key), never fabricated and never an exception, per
/// <see cref="ICrossSectionalSignal"/>. A missing <b>per-asset</b> key (an equity income yield or an FX
/// foreign rate) omits that asset; a missing <b>market-wide</b> key (a bond curve point, or the FX local
/// rate) omits every asset that depends on it (the documented market-wide-key behavior for this signal —
/// omit the assets it can no longer score, not a raw-return fallback). An empty auxiliary series is treated
/// as absent.</para>
/// <para><b>Price returns not consumed:</b> carry is a forward-looking function of yields and curves, so
/// the per-asset <c>returns</c> series does <b>not</b> enter any carry formula. It is still validated for
/// positional alignment, and an asset with a <see langword="null"/> or empty return series is omitted
/// (insufficient history) — a basic presence check consistent with the signal-layer contract.</para>
/// <para><b>Degenerate input:</b> an FX-hedged asset whose local rate is <c>≤ −100%</c>
/// (<c>1 + r_local ≤ 0</c>) makes the eq. 7 denominator non-positive. The signal omits each affected
/// FX-hedged asset and names them in one aggregated Warning record for the call; bond, equity, and
/// otherwise scoreable FX-hedged assets remain in the result. An empty asset list yields an empty
/// dictionary.</para>
/// <para><b>Ranking only:</b> the score ranks assets by carry for a single rebalance; it is consumed
/// additively (after cross-sectional z-scoring) by <c>SignalTiltConstruction</c>. Following the paper's
/// long–short high-minus-low carry trade adapted long-only, there is no short leg — a low-carry asset
/// simply ranks low, never negative-weighted.</para>
/// <para><b>Thread-safety:</b> stateless and immutable after construction; a single instance may score
/// independent cross-sections concurrently.</para>
/// </remarks>
public sealed class CarrySignal : ICrossSectionalSignal
{
    private readonly IReadOnlyDictionary<Symbol, CarryAssetClass> _assetClasses;
    private readonly decimal _rollDownDuration;
    private readonly Symbol _longCurveKey;
    private readonly Symbol _shortCurveKey;
    private readonly Symbol _localShortRateKey;
    private readonly ILogger<CarrySignal> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CarrySignal"/> class using a
    /// <see cref="NullLogger{T}"/> for diagnostics.
    /// </summary>
    /// <param name="assetClasses">
    /// Per-symbol map from each asset to its <see cref="CarryAssetClass"/>, selecting the carry model
    /// applied to that asset. An asset absent from this map is omitted from the scored cross-section (no
    /// determinable carry model). Validated non-null.
    /// </param>
    /// <param name="rollDownDuration">
    /// The modified-duration multiplier on the bond roll-down (term-spread) component
    /// (<c>rollDownDuration · (y_long − y_short)</c>). The default of <c>1</c> makes the roll-down the raw
    /// term spread; pass the bond ETF's modified duration to scale it. Must be non-negative.
    /// </param>
    /// <param name="longCurveSeriesId">
    /// The market-wide <c>auxiliarySeries</c> key for the bond long-maturity curve point (default
    /// <c>DGS10</c>, the FRED 10-year Treasury yield). Must be non-empty.
    /// </param>
    /// <param name="shortCurveSeriesId">
    /// The market-wide <c>auxiliarySeries</c> key for the bond short-maturity curve point (default
    /// <c>DGS2</c>, the FRED 2-year Treasury yield). Must be non-empty.
    /// </param>
    /// <param name="localShortRateSeriesId">
    /// The market-wide <c>auxiliarySeries</c> key for the FX local risk-free short rate (default
    /// <c>DGS3MO</c>, the FRED 3-month Treasury yield). Must be non-empty.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assetClasses"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="rollDownDuration"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when any series-id argument is <see langword="null"/> or empty.
    /// </exception>
    public CarrySignal(
        IReadOnlyDictionary<Symbol, CarryAssetClass> assetClasses,
        decimal rollDownDuration = 1m,
        string longCurveSeriesId = "DGS10",
        string shortCurveSeriesId = "DGS2",
        string localShortRateSeriesId = "DGS3MO")
        : this(
            assetClasses,
            NullLogger<CarrySignal>.Instance,
            rollDownDuration,
            longCurveSeriesId,
            shortCurveSeriesId,
            localShortRateSeriesId)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CarrySignal"/> class with a logger for aggregated
    /// per-call degeneracy diagnostics.
    /// </summary>
    /// <param name="assetClasses">Per-symbol map selecting the carry model applied to each asset.</param>
    /// <param name="logger">The logger that receives aggregated degeneracy warnings.</param>
    /// <param name="rollDownDuration">The modified-duration multiplier on bond roll-down.</param>
    /// <param name="longCurveSeriesId">The market-wide long-maturity curve-series identifier.</param>
    /// <param name="shortCurveSeriesId">The market-wide short-maturity curve-series identifier.</param>
    /// <param name="localShortRateSeriesId">The market-wide local short-rate series identifier.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assetClasses"/> or <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="rollDownDuration"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when any series-id argument is <see langword="null"/> or empty.
    /// </exception>
    public CarrySignal(
        IReadOnlyDictionary<Symbol, CarryAssetClass> assetClasses,
        ILogger<CarrySignal> logger,
        decimal rollDownDuration = 1m,
        string longCurveSeriesId = "DGS10",
        string shortCurveSeriesId = "DGS2",
        string localShortRateSeriesId = "DGS3MO")
    {
        Guard.AgainstNull(() => assetClasses);
        Guard.AgainstNull(() => logger);
        Guard.AgainstNegative(() => rollDownDuration);
        Guard.AgainstNullOrWhiteSpace(() => longCurveSeriesId);
        Guard.AgainstNullOrWhiteSpace(() => shortCurveSeriesId);
        Guard.AgainstNullOrWhiteSpace(() => localShortRateSeriesId);

        _assetClasses = assetClasses;
        _rollDownDuration = rollDownDuration;
        _longCurveKey = new Symbol(longCurveSeriesId);
        _shortCurveKey = new Symbol(shortCurveSeriesId);
        _localShortRateKey = new Symbol(localShortRateSeriesId);
        _logger = logger;
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
            var asset = assets[i];

            // Price returns are not consumed by any carry formula, but a null/empty series is treated as
            // insufficient history (omit) — the basic per-asset presence check of the signal-layer contract.
            var series = returns[i];
            if (series is null || series.Length == 0)
            {
                continue;
            }

            // Asset-class dispatch: an asset with no class assignment has no determinable carry model.
            if (!_assetClasses.TryGetValue(asset, out var assetClass))
            {
                continue; // omitted (absent key)
            }

            switch (assetClass)
            {
                case CarryAssetClass.Bond:
                    if (TryComputeBondCarry(auxiliarySeries, out var bondCarry))
                    {
                        scores[asset] = bondCarry;
                    }

                    break;

                case CarryAssetClass.Equity:
                    // Equity/REIT carry = the asset's trailing income (dividend) yield (eq. 8), keyed
                    // per-asset. A missing per-asset key omits that asset.
                    if (TryLatest(auxiliarySeries, asset, out var incomeYield))
                    {
                        scores[asset] = incomeYield;
                    }

                    break;

                case CarryAssetClass.FxHedged:
                    // FX-hedged carry = (r_foreign − r_local) / (1 + r_local) (eq. 7). Foreign leg is
                    // per-asset; local leg is market-wide. A missing leg omits the asset.
                    if (TryComputeFxCarry(asset, auxiliarySeries, out var fxCarry, out var isDegenerate))
                    {
                        scores[asset] = fxCarry;
                    }
                    else if (isDegenerate)
                    {
                        degenerateAssets.Add(asset);
                    }

                    break;

                default:
                    break;
            }
        }

        if (degenerateAssets.Count > 0)
        {
            _logger.LogWarning(
                "CarrySignal omitted {DegenerateCount} FX-hedged asset(s) because the local short rate implies a non-positive (1 + r_local) denominator: {DegenerateAssets}",
                degenerateAssets.Count,
                string.Join(", ", degenerateAssets));
        }

        return scores;
    }

    /// <summary>
    /// Bond carry (Koijen et al. eq. 13–15): <c>y_long + rollDownDuration · (y_long − y_short)</c> from the
    /// most recent observations of the two market-wide curve-point series. Returns <see langword="false"/>
    /// (the asset is omitted) when either curve point is absent or its series is empty.
    /// </summary>
    private bool TryComputeBondCarry(
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries,
        out decimal carry)
    {
        carry = 0m;

        if (!TryLatest(auxiliarySeries, _longCurveKey, out var yLong)
            || !TryLatest(auxiliarySeries, _shortCurveKey, out var yShort))
        {
            return false; // a missing market-wide curve point omits every bond
        }

        // Yield (eq. 13: under constant price, carry = current yield) + roll-down (eq. 15). Under a flat
        // curve (yLong == yShort) the roll-down vanishes and carry == the yield exactly.
        carry = yLong + (_rollDownDuration * (yLong - yShort));
        return true;
    }

    /// <summary>
    /// FX-hedged carry (Koijen et al. eq. 7): <c>(r_foreign − r_local) / (1 + r_local)</c> from the most
    /// recent observations of the per-asset foreign short rate (keyed by <paramref name="asset"/>) and the
    /// market-wide local short rate. Returns <see langword="false"/> (the asset is omitted) when either leg
    /// is absent or empty, or when the local rate is <c>≤ −100%</c> (a non-positive denominator). The
    /// latter case sets <c>isDegenerate</c> so the caller can aggregate its Warning record.
    /// </summary>
    private bool TryComputeFxCarry(
        Symbol asset,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries,
        out decimal carry,
        out bool isDegenerate)
    {
        carry = 0m;
        isDegenerate = false;

        if (!TryLatest(auxiliarySeries, asset, out var rForeign)
            || !TryLatest(auxiliarySeries, _localShortRateKey, out var rLocal))
        {
            return false; // a missing per-asset foreign rate or market-wide local rate omits the asset
        }

        var denominator = 1m + rLocal;
        if (denominator <= 0m)
        {
            isDegenerate = true;
            return false;
        }

        carry = (rForeign - rLocal) / denominator;
        return true;
    }

    /// <summary>
    /// Reads the most recent (last) observation of the auxiliary series under <paramref name="key"/>.
    /// Returns <see langword="false"/> when the series is absent or empty (treated as an absent key).
    /// </summary>
    private static bool TryLatest(
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries,
        Symbol key,
        out decimal latest)
    {
        latest = 0m;
        if (auxiliarySeries is not null
            && auxiliarySeries.TryGetValue(key, out var series)
            && series is not null
            && series.Length > 0)
        {
            latest = series[^1];
            return true;
        }

        return false;
    }
}
