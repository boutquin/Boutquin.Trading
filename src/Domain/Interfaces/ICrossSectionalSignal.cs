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

namespace Boutquin.Trading.Domain.Interfaces;

/// <summary>
/// A cross-sectional premia signal: a stateless, pure function that scores a set of assets against
/// one another for a single rebalance, producing a per-asset ranking score that
/// <c>SignalTiltConstruction</c> consumes additively as a long-only weight tilt.
/// </summary>
/// <remarks>
/// <para><b>Role:</b> the shared contract for the factor-premia signal layer (cross-sectional momentum,
/// time-series momentum, value, carry, low-volatility). Each signal turns a return history (and,
/// where needed, non-price auxiliary series) into a per-asset score; higher score means "tilt toward
/// this asset". Signals only <i>rank</i> the cross-section — a score is not a total-return forecast.</para>
/// <para><b>Key space:</b> the returned dictionary is keyed by <see cref="Symbol"/> identifiers of two
/// kinds — per-asset symbols (an entry per scored asset) and, for signals that read them, well-known
/// market-wide series identifiers supplied through <c>auxiliarySeries</c> (for example the FRED yield
/// identifiers Recipes fetches). Each implementation's own <c>ComputeScores</c> docs enumerate exactly
/// which keys it reads and of which kind. Implementations return only per-asset scores; auxiliary
/// series are an input channel, never an output key.</para>
/// <para><b>As-of alignment (caller obligation):</b> auxiliary series are positional and carry no
/// dates; signals read them from the tail (a most-recent observation, a trailing-window cumulative
/// return). The caller must therefore supply arrays whose <b>last element is the most recent
/// observation at the decision date</b> being scored — supplying a full-run series at a historical
/// rebalance leaks post-decision observations into the tail (lookahead bias). For multi-rebalance
/// backtests, bind a <c>DatedAuxiliarySeries</c> to <c>SignalTiltConstruction</c> instead: its
/// date-aware weight computation slices the dated observations to the rebalance date before any
/// signal reads them, satisfying this obligation structurally.</para>
/// <para><b>Insufficient-history contract:</b> an asset whose return series is shorter than the signal's
/// lookback is <b>omitted</b> from the returned dictionary (its key is absent) — never given a
/// fabricated score and never a thrown exception. A three-year-old ETF inside a five-year-lookback
/// signal is a normal state, not a degenerate input. <c>SignalTiltConstruction</c> treats an absent
/// key as exactly zero tilt for that asset. This same rule governs a missing per-asset auxiliary
/// series (that asset is omitted); a missing market-wide auxiliary series follows the individual
/// signal's documented fallback.</para>
/// <para><b>Degenerate inputs (repo convention):</b> an empty asset list yields an empty dictionary
/// (never <see langword="null"/>). A per-asset degeneracy omits only the affected asset and is logged at
/// Warning by the signal; routine insufficient-history omissions remain silent within the signal. A
/// cross-sectional degeneracy, where every scored asset shares the same value and a required z-score is
/// undefined, throws <see cref="DegenerateCrossSectionException"/>. The subtype lets callers distinguish
/// the universe-wide failure structurally from an absent per-asset score.</para>
/// <para><b>Thread-safety:</b> implementations are stateless (no <c>Fit</c> phase) and therefore
/// thread-safe — the same instance may score independent cross-sections concurrently.</para>
/// </remarks>
public interface ICrossSectionalSignal
{
    /// <summary>
    /// Scores each asset in <paramref name="assets"/> against the others for a single rebalance,
    /// omitting any asset whose history is too short to score.
    /// </summary>
    /// <param name="assets">
    /// The ordered list of assets to score. <c>assets[i]</c> corresponds to return series
    /// <c>returns[i]</c>. May be empty (yields an empty result).
    /// </param>
    /// <param name="returns">
    /// A jagged array of per-asset return series where <c>returns[i]</c> is the series for
    /// <c>assets[i]</c>. Series lengths may differ across assets — a series shorter than the signal's
    /// lookback causes that asset to be omitted (see the insufficient-history contract in the type
    /// remarks), not an exception.
    /// </param>
    /// <param name="auxiliarySeries">
    /// Optional non-price inputs keyed by <see cref="Symbol"/> — per-asset series (for example an ETF's
    /// trailing income yield) and/or well-known market-wide series (for example FRED yield identifiers).
    /// Signals that do not need auxiliary data ignore this argument. Defaults to
    /// <see langword="null"/> (no auxiliary data supplied).
    /// </param>
    /// <returns>
    /// A dictionary mapping each <i>scored</i> asset to its cross-sectional score. Assets omitted for
    /// insufficient history are absent from the dictionary (never assigned a fabricated score). The
    /// result is empty (never <see langword="null"/>) when <paramref name="assets"/> is empty.
    /// </returns>
    /// <exception cref="DegenerateCrossSectionException">
    /// Thrown when a required z-score is undefined because every scored asset has the same input value.
    /// The historical phrase <c>zero cross-sectional variance</c> remains in shipped messages for
    /// compatibility with consumers on older package pins, but the exception subtype is the contract.
    /// Per-asset degeneracies do not throw: implementations omit those assets and aggregate them into one
    /// Warning record per call.
    /// </exception>
    IReadOnlyDictionary<Symbol, decimal> ComputeScores(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null);
}
