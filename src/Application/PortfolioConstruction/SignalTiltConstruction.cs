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

namespace Boutquin.Trading.Application.PortfolioConstruction;

using Domain.ValueObjects;

/// <summary>
/// Decorator that tilts a base construction model's weights toward assets favored by one or more
/// cross-sectional premia signals (momentum, value, carry, low-volatility). Each signal's per-asset
/// scores are cross-sectionally z-scored and added to the base weights with a per-signal strength;
/// the tilted weights are floored at zero and re-normalized to sum to 1.0 (long-only, fully invested).
/// </summary>
/// <remarks>
/// <para><b>Objective:</b> for base weights <c>w</c>, per-signal strengths <c>strength_s</c>, and
/// cross-sectional z-scores <c>z_s(i)</c>, produce
/// <c>w_i ← w_i + Σ_s strength_s · z_s(i)</c>, then clamp negatives to 0 and re-normalize to
/// <c>Σ w = 1</c>. This is the same additive-tilt/renormalize contract as
/// <see cref="TacticalOverlayConstruction"/>; the two are independent decorators and compose by
/// nesting (regime tilt ⊕ signal tilt) without double-normalization drift — the outer decorator
/// re-normalizes the inner decorator's already-normalized weights, which is idempotent on the sum.</para>
/// <para><b>Cross-sectional z-score:</b> for each signal, the mean and (population) standard deviation
/// are computed across exactly the assets that signal scored this rebalance; <c>z_s(i) = (score_i −
/// mean) / σ</c>. When that cross-section has zero dispersion (<c>σ ≈ 0</c>, e.g. every scored asset
/// carries the same score, or only one asset was scored) the signal contributes no tilt — there is no
/// cross-sectional information to exploit — rather than throwing; a genuinely degenerate z-score input
/// is the individual signal's own contract (see <see cref="ICrossSectionalSignal"/>), surfaced before
/// it reaches this decorator.</para>
/// <para><b>Absent-key / insufficient-history semantics:</b> a signal omits any asset whose history is
/// too short to score (see <see cref="ICrossSectionalSignal"/>); an omitted asset receives exactly
/// zero tilt from that signal (its base weight is carried through unchanged by that signal). Omitted
/// assets are excluded from the z-score's mean/σ so a short-history asset cannot distort the scored
/// cross-section. The omission is silent in the weights but observable in logs (see below).</para>
/// <para><b>Diagnostics:</b> per rebalance, each signal's omitted-asset set is logged at
/// <see cref="LogLevel.Debug"/> through the optional <c>ILogger</c> (default
/// <see cref="NullLogger{T}"/>), so an all-assets-omitted signal — a data problem masquerading as
/// "no tilt" — is diagnosable without changing the weight contract.</para>
/// <para><b>Decorator-identity:</b> with every signal strength 0 the output equals the base model's
/// weights exactly (each tilt term is multiplied by 0), so wrapping a model in a zero-strength
/// signal-tilt is a no-op.</para>
/// <para><b>Auxiliary-series binding — positional vs. date-indexed:</b> the positional constructor
/// binds a caller-aligned <c>IReadOnlyDictionary&lt;Symbol, decimal[]&gt;</c> whose arrays must end at
/// the evaluation date (correct for single-date, as-of computation). For a multi-rebalance backtest,
/// bind a <see cref="DatedAuxiliarySeries"/> instead and invoke the date-aware
/// <c>ComputeTargetWeights(assets, returns, asOf)</c> overload (which the backtest rebalance loop
/// calls): the dated series are sliced per rebalance to the observations known at that date, so a
/// signal's tail-read can never see past the decision point (no lookahead from a single run-long
/// binding). A date-indexed instance refuses the date-free overload (throws
/// <see cref="InvalidOperationException"/>) rather than reading an unsliced tail.</para>
/// <para><b>Composition:</b> this is a manually composed decorator, not a DI-switch construction model
/// (like <see cref="TurnoverPenalizedConstruction"/>) — signals are constructor-supplied, not resolved
/// from configuration. Wrap a base model explicitly:
/// <c>new SignalTiltConstruction(inner, [(momentumSignal, 0.10m)])</c>.</para>
/// <para><b>Concurrency:</b> thread-safe — the decorator holds no mutable state after construction and
/// the signals it composes are stateless.</para>
/// </remarks>
public sealed class SignalTiltConstruction : IPortfolioConstructionModel
{
    private const decimal SigmaFloor = 1e-12m;

    private readonly IPortfolioConstructionModel _baseModel;
    private readonly IReadOnlyList<(ICrossSectionalSignal Signal, decimal Strength)> _signals;
    private readonly IReadOnlyDictionary<Symbol, decimal[]>? _auxiliarySeries;
    private readonly DatedAuxiliarySeries? _datedAuxiliarySeries;
    private readonly ILogger<SignalTiltConstruction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalTiltConstruction"/> class wrapping
    /// <paramref name="baseModel"/> with the given signal/strength pairs.
    /// </summary>
    /// <param name="baseModel">
    /// The base construction model whose weights are tilted. Validated non-null.
    /// </param>
    /// <param name="signals">
    /// The signal/strength pairs applied additively. Each <c>Strength</c> scales that signal's
    /// cross-sectional z-scores; a strength of 0 makes the signal a no-op (decorator-identity). Validated
    /// non-null; an empty list makes the decorator a pass-through re-normalization of the base weights.
    /// </param>
    /// <param name="auxiliarySeries">
    /// Optional non-price inputs (per-asset and/or market-wide series, e.g. yields) forwarded to every
    /// signal's <see cref="ICrossSectionalSignal.ComputeScores"/> call. Constructor-bound positional
    /// series carry no dates, so this binding is sound only for single-evaluation (as-of) computation
    /// where the caller guarantees each array ends at the evaluation date; for multi-rebalance
    /// backtests, bind a <see cref="DatedAuxiliarySeries"/> via the date-indexed constructor instead so
    /// the series are sliced per rebalance. Defaults to <see langword="null"/> (no auxiliary data).
    /// Signals that do not read auxiliary data ignore it.
    /// </param>
    /// <param name="logger">
    /// Optional logger used to record each signal's omitted-asset set at
    /// <see cref="LogLevel.Debug"/> per rebalance. Defaults to <see cref="NullLogger{T}"/> (no logging).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="baseModel"/> or <paramref name="signals"/> is <see langword="null"/>.
    /// </exception>
    public SignalTiltConstruction(
        IPortfolioConstructionModel baseModel,
        IReadOnlyList<(ICrossSectionalSignal Signal, decimal Strength)> signals,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null,
        ILogger<SignalTiltConstruction>? logger = null)
    {
        Guard.AgainstNull(() => baseModel);
        Guard.AgainstNull(() => signals);

        _baseModel = baseModel;
        _signals = signals;
        _auxiliarySeries = auxiliarySeries;
        _logger = logger ?? NullLogger<SignalTiltConstruction>.Instance;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalTiltConstruction"/> class wrapping
    /// <paramref name="baseModel"/> with the given signal/strength pairs and <b>date-indexed</b>
    /// auxiliary series, so that one decorator instance can serve every rebalance of a backtest
    /// without lookahead: at each date-aware
    /// <see cref="ComputeTargetWeights(IReadOnlyList{Symbol}, decimal[][], DateOnly)"/> call the
    /// dated series are sliced to the observations known at that rebalance's evaluation date before
    /// any signal reads them.
    /// </summary>
    /// <param name="baseModel">
    /// The base construction model whose weights are tilted. Validated non-null.
    /// </param>
    /// <param name="signals">
    /// The signal/strength pairs applied additively, exactly as in the positional-series constructor.
    /// Validated non-null.
    /// </param>
    /// <param name="datedAuxiliarySeries">
    /// Date-indexed non-price inputs (per-asset and/or market-wide series, e.g. yields) covering the
    /// full backtest window. Per rebalance, <see cref="DatedAuxiliarySeries.SliceUpTo"/> reduces them
    /// to the positional view a signal may know at the evaluation date — the tail of each sliced
    /// array is the most recent observation <i>at that date</i>, never the run-end value. Validated
    /// non-null (to bind positional series aligned by the caller, use the positional-series
    /// constructor).
    /// </param>
    /// <param name="logger">
    /// Optional logger used to record each signal's omitted-asset set at
    /// <see cref="LogLevel.Debug"/> per rebalance. Defaults to <see cref="NullLogger{T}"/> (no logging).
    /// </param>
    /// <remarks>
    /// <para><b>Requires the date-aware entry point:</b> an instance constructed this way must be
    /// invoked through
    /// <see cref="ComputeTargetWeights(IReadOnlyList{Symbol}, decimal[][], DateOnly)"/> (the overload
    /// the backtest rebalance loop calls) — the date-free overload has no evaluation date to slice by
    /// and throws <see cref="InvalidOperationException"/> rather than silently reading the run-end
    /// tail (the lookahead this constructor exists to prevent).</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="baseModel"/>, <paramref name="signals"/>, or
    /// <paramref name="datedAuxiliarySeries"/> is <see langword="null"/>.
    /// </exception>
    public SignalTiltConstruction(
        IPortfolioConstructionModel baseModel,
        IReadOnlyList<(ICrossSectionalSignal Signal, decimal Strength)> signals,
        DatedAuxiliarySeries datedAuxiliarySeries,
        ILogger<SignalTiltConstruction>? logger = null)
    {
        Guard.AgainstNull(() => baseModel);
        Guard.AgainstNull(() => signals);
        Guard.AgainstNull(() => datedAuxiliarySeries);

        _baseModel = baseModel;
        _signals = signals;
        _datedAuxiliarySeries = datedAuxiliarySeries;
        _logger = logger ?? NullLogger<SignalTiltConstruction>.Instance;
    }

    /// <summary>
    /// Computes tilted target weights: the base model's weights adjusted additively by each signal's
    /// strength-scaled cross-sectional z-scores, floored at zero and re-normalized to sum to 1.0.
    /// </summary>
    /// <param name="assets">The ordered list of assets to allocate across.</param>
    /// <param name="returns">
    /// Per-asset return series (<c>returns[i]</c> corresponds to <c>assets[i]</c>), passed through to the
    /// base model and to each signal.
    /// </param>
    /// <returns>
    /// A dictionary mapping each asset to its tilted weight in [0, 1]; weights sum to 1.0. Returns an
    /// empty dictionary (never <see langword="null"/>) when <paramref name="assets"/> is empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assets"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para><b>Complexity:</b> O(S·N) for S signals over N assets, plus the base model's own cost.</para>
    /// <para><b>Fallback:</b> if every tilted weight clamps to zero (base weights all zero and negative
    /// tilts dominate), the result is equal weight over <paramref name="assets"/>, matching
    /// <see cref="TacticalOverlayConstruction"/>'s degenerate-case behavior.</para>
    /// </remarks>
    public IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeights(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns)
    {
        if (_datedAuxiliarySeries is not null)
        {
            throw new InvalidOperationException(
                "This SignalTiltConstruction was constructed with date-indexed auxiliary series, which " +
                "must be sliced to an evaluation date before any signal reads them. Call the date-aware " +
                "overload ComputeTargetWeights(assets, returns, asOf) instead — the date-free overload " +
                "has no date to slice by and will not silently read the full (run-end) series tail.");
        }

        return ComputeTargetWeightsCore(assets, returns, _auxiliarySeries, asOf: null);
    }

    /// <summary>
    /// Computes tilted target weights as of an explicit evaluation date: the base model's weights
    /// (computed via its own date-aware overload) adjusted additively by each signal's strength-scaled
    /// cross-sectional z-scores, where date-indexed auxiliary series are first sliced to the
    /// observations known at <paramref name="asOf"/>.
    /// </summary>
    /// <param name="assets">The ordered list of assets to allocate across.</param>
    /// <param name="returns">
    /// Per-asset return series (<c>returns[i]</c> corresponds to <c>assets[i]</c>) ending at
    /// <paramref name="asOf"/>, passed through to the base model and to each signal.
    /// </param>
    /// <param name="asOf">
    /// The evaluation (decision) date, inclusive. When this instance holds date-indexed auxiliary
    /// series they are sliced to observations dated on or before this date, so every signal's
    /// tail-read is correctly dated; the date is also forwarded to the base model's own date-aware
    /// overload so a date-aware model deeper in a composed chain receives it.
    /// </param>
    /// <returns>
    /// The same contract as the date-free overload: each asset's tilted weight in [0, 1], summing to
    /// 1.0; empty (never <see langword="null"/>) when <paramref name="assets"/> is empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assets"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para><b>Positional-series instances:</b> when this instance was constructed with positional
    /// (caller-aligned) auxiliary series — or none — the auxiliary channel is passed to the signals
    /// unchanged (there are no dates to slice by; the caller owns the alignment) and the result is
    /// identical to the date-free overload's, with <paramref name="asOf"/> still forwarded to the
    /// base model.</para>
    /// </remarks>
    public IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeights(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        DateOnly asOf)
    {
        var auxiliarySeries = _datedAuxiliarySeries is not null
            ? _datedAuxiliarySeries.SliceUpTo(asOf)
            : _auxiliarySeries;

        return ComputeTargetWeightsCore(assets, returns, auxiliarySeries, asOf);
    }

    private IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeightsCore(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries,
        DateOnly? asOf)
    {
        Guard.AgainstNull(() => assets);

        if (assets.Count == 0)
        {
            return new Dictionary<Symbol, decimal>();
        }

        var baseWeights = _baseModel.ComputeTargetWeightsAsOf(assets, returns, asOf);

        var tilted = new Dictionary<Symbol, decimal>(assets.Count);
        foreach (var asset in assets)
        {
            tilted[asset] = baseWeights.GetValueOrDefault(asset, 0m);
        }

        foreach (var (signal, strength) in _signals)
        {
            // A strength-0 (disabled) signal contributes no tilt, so its scores are never needed —
            // short-circuit before invoking ComputeScores so a disabled signal that would throw over a
            // degenerate cross-section cannot break decorator-identity ("zero strength = no-op").
            if (strength == 0m)
            {
                continue;
            }

            var scores = signal.ComputeScores(assets, returns, auxiliarySeries);

            // Cross-sectional mean/σ across exactly the assets this signal scored.
            var scored = new List<Symbol>(assets.Count);
            var omitted = new List<Symbol>();
            foreach (var asset in assets)
            {
                if (scores.ContainsKey(asset))
                {
                    scored.Add(asset);
                }
                else
                {
                    omitted.Add(asset);
                }
            }

            if (omitted.Count > 0 && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "SignalTiltConstruction: signal {Signal} omitted {OmittedCount} asset(s) this rebalance: {OmittedAssets}",
                    signal.GetType().Name,
                    omitted.Count,
                    string.Join(", ", omitted.Select(a => a.ToString())));
            }

            if (scored.Count == 0)
            {
                continue;
            }

            var mean = scored.Average(a => scores[a]);
            var variance = scored.Sum(a => (scores[a] - mean) * (scores[a] - mean)) / scored.Count;
            var sigma = (decimal)Math.Sqrt((double)variance);

            if (sigma <= SigmaFloor)
            {
                // Zero cross-sectional dispersion — no ranking information, so no tilt.
                continue;
            }

            foreach (var asset in scored)
            {
                var zScore = (scores[asset] - mean) / sigma;
                tilted[asset] += strength * zScore;
            }
        }

        // Floor at zero (long-only).
        foreach (var asset in assets)
        {
            if (tilted[asset] < 0m)
            {
                tilted[asset] = 0m;
            }
        }

        // Re-normalize to sum to 1.0.
        var total = tilted.Values.Sum();
        if (total > 0m)
        {
            foreach (var asset in assets)
            {
                tilted[asset] /= total;
            }
        }
        else
        {
            var equalWeight = 1m / assets.Count;
            foreach (var asset in assets)
            {
                tilted[asset] = equalWeight;
            }
        }

        return tilted;
    }
}
