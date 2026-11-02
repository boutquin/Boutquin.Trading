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
/// Estimates the per-asset expected return vector <c>E[r]</c> consumed by return-aware portfolio
/// construction models (for example <c>MeanVarianceConstruction</c> and
/// <c>MeanDownsideRiskConstruction</c>) as the mean input to their optimization objective.
/// </summary>
/// <remarks>
/// <para><b>Role:</b> the abstraction over "what is each asset's expected return" that the mean-based
/// optimizers historically computed inline as the sample mean of each asset's realized returns. Extracting
/// it behind this interface lets a caller supply a forward-looking building-block anchor
/// (<c>YieldAnchoredExpectedReturnEstimator</c>) in place of the rear-view sample mean
/// (<c>HistoricalMeanExpectedReturnEstimator</c>) without changing the optimizer (Ilmanen, A. (2012),
/// <i>Expected Returns on Major Asset Classes</i>, CFA Institute Research Foundation).</para>
/// <para><b>Positional, fully-populated contract:</b> the returned <c>decimal[]</c> is positional —
/// element <c>i</c> is the expected return of <c>assets[i]</c> — and MUST be fully populated with exactly
/// <c>assets.Count</c> entries. Unlike a cross-sectional <see cref="ICrossSectionalSignal"/>, an estimator
/// cannot "omit" an asset: the optimizer needs a mean for every asset it holds. An estimator that cannot
/// produce a value for some asset (a missing required input) MUST surface that as an exception naming the
/// affected symbols — never silently substitute a fallback for a subset, which would be indistinguishable
/// downstream from a correct estimate.</para>
/// <para><b>Missing-input rule (per implementation):</b>
/// <c>HistoricalMeanExpectedReturnEstimator</c> ignores <c>auxiliarySeries</c> entirely
/// (it needs only the realized returns). <c>YieldAnchoredExpectedReturnEstimator</c> requires
/// per-symbol auxiliary series and <b>throws</b>
/// <see cref="Boutquin.Trading.Domain.Exceptions.CalculationException"/> naming every symbol whose required
/// series is absent or too short — it never falls back to the sample mean for a subset of assets.</para>
/// <para><b>Thread-safety:</b> implementations are stateless and immutable after construction; a single
/// instance may estimate independent cross-sections concurrently.</para>
/// </remarks>
public interface IExpectedReturnEstimator
{
    /// <summary>
    /// Estimates the expected-return vector for <paramref name="assets"/>.
    /// </summary>
    /// <param name="assets">
    /// The ordered list of assets. <c>assets[i]</c> corresponds to return series <c>returns[i]</c>. May be
    /// empty (yields an empty array).
    /// </param>
    /// <param name="returns">
    /// A jagged array of per-asset realized-return series where <c>returns[i]</c> is the series for
    /// <c>assets[i]</c>. Every series must be non-null; length requirements are implementation-specific.
    /// </param>
    /// <param name="auxiliarySeries">
    /// Optional non-price inputs keyed by <see cref="Symbol"/> — per-asset series (for example a bond's
    /// current-yield history or an equity's trailing income yield) and/or well-known market-wide series
    /// (for example a trend-real-growth or breakeven-inflation series). Implementations that do not need
    /// auxiliary data ignore this argument. When an implementation was constructed with a bound
    /// auxiliary-series map, a non-null value passed here overrides that bound map for this call; a
    /// <see langword="null"/> value falls back to the constructor-bound map. Defaults to
    /// <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A positional, fully-populated array of length <c>assets.Count</c> where element <c>i</c> is the
    /// expected return of <c>assets[i]</c>. Empty (never <see langword="null"/>) when
    /// <paramref name="assets"/> is empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assets"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="returns"/> is <see langword="null"/> or its length does not match
    /// <paramref name="assets"/>, or a required return series is null or too short.
    /// </exception>
    /// <exception cref="Boutquin.Trading.Domain.Exceptions.CalculationException">
    /// Thrown by estimators requiring auxiliary inputs (for example
    /// <c>YieldAnchoredExpectedReturnEstimator</c>) when the required series for one or more assets
    /// is absent or too short; the message names every affected symbol.
    /// </exception>
    // RS0027: the date-aware overload below has more parameters, but this member's optional
    // auxiliarySeries default predates it and removing the default would break every existing
    // two-argument call site; the larger overload's asOf is deliberately required (an evaluation
    // date must be explicit), so the optional-parameter guidance cannot be satisfied both ways.
#pragma warning disable RS0027
    decimal[] EstimateExpectedReturns(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null);
#pragma warning restore RS0027

    /// <summary>
    /// Estimates the expected-return vector for <paramref name="assets"/> as of an explicit evaluation
    /// date — the date-aware entry point a rebalance loop's construction model calls so that estimators
    /// holding date-indexed auxiliary series can restrict themselves to observations known at the
    /// decision point.
    /// </summary>
    /// <param name="assets">
    /// The ordered list of assets. <c>assets[i]</c> corresponds to return series <c>returns[i]</c>. May
    /// be empty (yields an empty array).
    /// </param>
    /// <param name="returns">
    /// A jagged array of per-asset realized-return series ending at <paramref name="asOf"/> (the caller
    /// windows the series to the evaluation date, as the backtest rebalance loop already does).
    /// </param>
    /// <param name="auxiliarySeries">
    /// Optional positional auxiliary-series override with the same semantics as on the date-free
    /// overload: a non-null value overrides any constructor-bound series for this call.
    /// </param>
    /// <param name="asOf">
    /// The evaluation (decision) date of this estimate, inclusive. Implementations holding
    /// date-indexed auxiliary series must not consume any observation dated after this date.
    /// </param>
    /// <returns>
    /// The same positional, fully-populated contract as the date-free overload: an array of length
    /// <c>assets.Count</c> where element <c>i</c> is the expected return of <c>assets[i]</c>.
    /// </returns>
    /// <remarks>
    /// <para><b>Default behavior:</b> forwards to the date-free overload, ignoring
    /// <paramref name="asOf"/> — byte-identical for every estimator whose inputs carry no dates
    /// (<c>HistoricalMeanExpectedReturnEstimator</c>, or a positional-bound
    /// <c>YieldAnchoredExpectedReturnEstimator</c>). Only estimators that hold date-indexed state
    /// override.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assets"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown under the same conditions as the date-free overload.
    /// </exception>
    /// <exception cref="Boutquin.Trading.Domain.Exceptions.CalculationException">
    /// Thrown under the same conditions as the date-free overload — including when the
    /// <paramref name="asOf"/> slice of a required date-indexed series is empty (no observation on or
    /// before the evaluation date), which leaves the affected symbols un-estimable.
    /// </exception>
    decimal[] EstimateExpectedReturns(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries,
        DateOnly asOf)
        => EstimateExpectedReturns(assets, returns, auxiliarySeries);
}
