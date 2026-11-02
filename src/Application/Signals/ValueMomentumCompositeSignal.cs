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
/// Value-momentum composite signal: the 50/50 value-momentum COMBO of Asness, Moskowitz &amp; Pedersen
/// (2013, eq. 3), operationalized as <c>compositeScore(i) = valueWeight·z(value)(i) +
/// momentumWeight·z(momentum)(i)</c> — a per-rebalance cross-sectional z-score blend of a value leg and a
/// momentum leg — so that <c>SignalTiltConstruction</c> tilts a long-only portfolio toward assets that
/// are strong on the diversified combination the paper shows outperforms either premium alone.
/// </summary>
/// <remarks>
/// <para><b>Reference:</b> Asness, C.S., Moskowitz, T.J. &amp; Pedersen, L.H. (2013), "Value and Momentum
/// Everywhere," <i>Journal of Finance</i>, 68(3), 929–985. The paper's COMBO factor is "a 50/50 equal
/// combination … of value and momentum" (eq. 3, p. 939). Value and momentum are strongly negatively
/// correlated — "about −0.60" within markets (p. 945) and <c>−0.48</c> using the negative-of-past-5-year
/// return value proxy this signal's value leg implements (p. 950) — so the equal combination diversifies:
/// "In every market, the value/momentum combination outperforms either value or momentum by itself"
/// (p. 945).</para>
/// <para><b>Composition:</b> the momentum leg is the shipped <see cref="CrossSectionalMomentumSignal"/>
/// (the 12-1 Jegadeesh–Titman operationalization) and the value leg is
/// <see cref="LongHorizonReversalValueSignal"/> (the negative trailing five-year return, no skip). Both
/// are consumed as <see cref="ICrossSectionalSignal"/> collaborators; the default constructor constructs
/// exactly those two, and an explicit constructor accepts any two signals as the value and momentum legs
/// (so callers may substitute or test with alternatives).</para>
/// <para><b>Z-scoring &amp; the composite score:</b> for a single rebalance, each leg is scored, then the two
/// score sets are cross-sectionally z-scored (population mean / σ) over the <b>intersection</b> of assets
/// scored by both legs; the composite is <c>valueWeight·z(value) + momentumWeight·z(momentum)</c>. The
/// weights are configurable and default to the paper's <c>0.5 / 0.5</c>. Z-scoring puts the two
/// differently-scaled legs on a common footing before the blend, matching the additive z-score tilt
/// <c>SignalTiltConstruction</c> applies downstream.</para>
/// <para><b>Omission rule (either-leg-omits):</b> an asset is included in the composite <b>only if both
/// legs scored it</b>. An asset omitted by <i>either</i> leg (for example an ETF with enough history for
/// the 12-1 momentum window but not the five-year value window) is omitted from the composite — its
/// composite z-score is undefined because one of its two components does not exist. This is the coherent
/// reading of <see cref="ICrossSectionalSignal"/>'s insufficient-history contract for a two-leg signal:
/// the composite needs both legs, so a missing leg omits the asset (absent key), never a fabricated or
/// half-populated score.</para>
/// <para><b>Key space:</b> the returned dictionary is keyed only by per-asset <see cref="Symbol"/>
/// identifiers (one entry per asset scored by both legs). This signal reads no auxiliary series — the
/// <c>auxiliarySeries</c> argument is ignored (it is not forwarded to the legs, which likewise ignore it).</para>
/// <para><b>Degenerate input:</b> when the intersection is non-empty but a required leg has zero
/// cross-sectional variance across it (every scored asset shares the same value — or momentum — score, so
/// its z-score is undefined), the signal throws
/// <see cref="Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException"/>, surfacing the
/// degeneracy rather
/// than propagating a meaningless value — consistent with <see cref="ICrossSectionalSignal"/>. An empty
/// asset list (or an empty intersection) yields an empty dictionary.</para>
/// <para><b>Known intended-omission (long-only adaptation):</b> the paper's long–short market-neutral
/// COMBO is not implemented — this is a long-only per-asset composite <i>score</i>, no short leg. The
/// headline weighting is strictly 50/50; the configurable weights extend, not replace, that default.</para>
/// <para><b>Thread-safety:</b> stateless and immutable after construction (both legs are stateless); a
/// single instance may score independent cross-sections concurrently.</para>
/// </remarks>
public sealed class ValueMomentumCompositeSignal : ICrossSectionalSignal
{
    private const decimal SigmaFloor = 1e-12m;

    private readonly ICrossSectionalSignal _valueSignal;
    private readonly ICrossSectionalSignal _momentumSignal;
    private readonly decimal _valueWeight;
    private readonly decimal _momentumWeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueMomentumCompositeSignal"/> class from an explicit
    /// value leg and momentum leg (any two <see cref="ICrossSectionalSignal"/> collaborators), with
    /// configurable blend weights.
    /// </summary>
    /// <param name="valueSignal">The value leg (typically <see cref="LongHorizonReversalValueSignal"/>).</param>
    /// <param name="momentumSignal">
    /// The momentum leg (typically the shipped <see cref="CrossSectionalMomentumSignal"/>).
    /// </param>
    /// <param name="valueWeight">
    /// The weight on the value leg's z-score (the paper's 0.5). Must be non-negative.
    /// </param>
    /// <param name="momentumWeight">
    /// The weight on the momentum leg's z-score (the paper's 0.5). Must be non-negative.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="valueSignal"/> or <paramref name="momentumSignal"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="valueWeight"/> or <paramref name="momentumWeight"/> is negative.
    /// </exception>
    public ValueMomentumCompositeSignal(
        ICrossSectionalSignal valueSignal,
        ICrossSectionalSignal momentumSignal,
        decimal valueWeight = 0.5m,
        decimal momentumWeight = 0.5m)
    {
        Guard.AgainstNull(() => valueSignal);
        Guard.AgainstNull(() => momentumSignal);
        Guard.AgainstNegative(() => valueWeight);
        Guard.AgainstNegative(() => momentumWeight);

        _valueSignal = valueSignal;
        _momentumSignal = momentumSignal;
        _valueWeight = valueWeight;
        _momentumWeight = momentumWeight;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueMomentumCompositeSignal"/> class with default
    /// legs: a <see cref="LongHorizonReversalValueSignal"/> value leg and the shipped
    /// <see cref="CrossSectionalMomentumSignal"/> momentum leg, both parameterized from the supplied
    /// window arguments.
    /// </summary>
    /// <param name="valueLookbackMonths">The value leg's lookback in months (default 60 — five years).</param>
    /// <param name="momentumLookbackMonths">The momentum leg's total lookback in months (default 12).</param>
    /// <param name="momentumSkipMonths">
    /// The momentum leg's skip window in months (default 1 — the 12-1 convention).
    /// </param>
    /// <param name="tradingDaysPerMonth">Trading days per month for both legs' windows (default 21).</param>
    /// <param name="valueWeight">The weight on the value leg's z-score (default 0.5). Must be non-negative.</param>
    /// <param name="momentumWeight">
    /// The weight on the momentum leg's z-score (default 0.5). Must be non-negative.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when a window argument is non-positive, or when <paramref name="momentumSkipMonths"/> is not
    /// strictly less than <paramref name="momentumLookbackMonths"/> (validated by the constructed legs).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="valueWeight"/> or <paramref name="momentumWeight"/> is negative.
    /// </exception>
    public ValueMomentumCompositeSignal(
        int valueLookbackMonths = 60,
        int momentumLookbackMonths = 12,
        int momentumSkipMonths = 1,
        int tradingDaysPerMonth = 21,
        decimal valueWeight = 0.5m,
        decimal momentumWeight = 0.5m)
        : this(
            new LongHorizonReversalValueSignal(valueLookbackMonths, tradingDaysPerMonth),
            new CrossSectionalMomentumSignal(momentumLookbackMonths, momentumSkipMonths, tradingDaysPerMonth),
            valueWeight,
            momentumWeight)
    {
    }

    /// <summary>
    /// Scores each asset by the weighted blend of the cross-sectional z-scores of its value and momentum
    /// legs, over the assets scored by both legs.
    /// </summary>
    /// <param name="assets">
    /// The ordered list of assets to score; <c>assets[i]</c> corresponds to <c>returns[i]</c>. May be
    /// empty (yields an empty result).
    /// </param>
    /// <param name="returns">
    /// A jagged array of per-asset daily-return series, positionally aligned with
    /// <paramref name="assets"/>. Forwarded to both legs; a series too short for a leg omits the asset from
    /// that leg (and therefore from the composite).
    /// </param>
    /// <param name="auxiliarySeries">
    /// Ignored — neither the value leg nor the momentum leg reads auxiliary series.
    /// </param>
    /// <returns>
    /// A dictionary mapping each asset scored by both legs to its composite score. Assets omitted by either
    /// leg are absent (never assigned a fabricated score). Empty (never <see langword="null"/>) when
    /// <paramref name="assets"/> is empty or no asset is scored by both legs.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assets"/> or <paramref name="returns"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="returns"/> has a different length than <paramref name="assets"/> —
    /// surfaced by the legs rather than silently truncated.
    /// </exception>
    /// <exception cref="Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException">
    /// Thrown when the intersection of both legs' scored assets is non-empty but a required leg has zero
    /// cross-sectional variance across it (its z-score is undefined).
    /// </exception>
    /// <remarks>
    /// <b>Complexity:</b> O(N·W) for N assets over the larger leg window W (the two legs each make one pass;
    /// the z-score/blend adds an O(N) pass over the intersection).
    /// </remarks>
    public IReadOnlyDictionary<Symbol, decimal> ComputeScores(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null)
    {
        Guard.AgainstNull(() => assets);
        Guard.AgainstNull(() => returns);

        var valueScores = _valueSignal.ComputeScores(assets, returns, auxiliarySeries);
        var momentumScores = _momentumSignal.ComputeScores(assets, returns, auxiliarySeries);

        // Intersection: an asset omitted by EITHER leg is omitted from the composite (both z-scores must
        // be defined). Iterate `assets` in order for a deterministic cross-section.
        var common = new List<Symbol>(assets.Count);
        var valueVals = new List<decimal>(assets.Count);
        var momentumVals = new List<decimal>(assets.Count);
        for (var i = 0; i < assets.Count; i++)
        {
            var asset = assets[i];
            if (valueScores.TryGetValue(asset, out var v) && momentumScores.TryGetValue(asset, out var m))
            {
                common.Add(asset);
                valueVals.Add(v);
                momentumVals.Add(m);
            }
        }

        if (common.Count == 0)
        {
            return new Dictionary<Symbol, decimal>();
        }

        var zValue = ZScore(valueVals, "value");
        var zMomentum = ZScore(momentumVals, "momentum");

        var scores = new Dictionary<Symbol, decimal>(common.Count);
        for (var i = 0; i < common.Count; i++)
        {
            scores[common[i]] = (_valueWeight * zValue[i]) + (_momentumWeight * zMomentum[i]);
        }

        return scores;
    }

    /// <summary>
    /// Population (divide-by-N) cross-sectional z-scores of <paramref name="values"/>, matching the
    /// z-score <c>SignalTiltConstruction</c> applies. Throws
    /// <see cref="Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException"/> when the
    /// cross-section has zero
    /// variance (the z-score is undefined), naming the offending leg.
    /// </summary>
    private static decimal[] ZScore(IReadOnlyList<decimal> values, string legName)
    {
        var n = values.Count;
        var mean = 0m;
        for (var i = 0; i < n; i++)
        {
            mean += values[i];
        }
        mean /= n;

        var variance = 0m;
        for (var i = 0; i < n; i++)
        {
            var d = values[i] - mean;
            variance += d * d;
        }
        variance /= n;

        var sigma = (decimal)Math.Sqrt((double)variance);
        if (sigma <= SigmaFloor)
        {
            throw new Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException(
                $"Cannot z-score the {legName} leg: its {n} scored assets have zero cross-sectional " +
                "variance, so the composite z-score is undefined.");
        }

        var z = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            z[i] = (values[i] - mean) / sigma;
        }

        return z;
    }
}
