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
/// Computes target portfolio weights for a set of assets based on their return history
/// and optional constraints.
/// </summary>
public interface IPortfolioConstructionModel
{
    /// <summary>
    /// Computes the target weight for each asset.
    /// </summary>
    /// <param name="assets">The ordered list of assets to allocate across.</param>
    /// <param name="returns">
    /// A jagged array where <c>returns[i]</c> is the return series for <c>assets[i]</c>.
    /// May be empty for models that do not require historical data (e.g., equal weight).
    /// </param>
    /// <returns>
    /// A dictionary mapping each asset to its target weight.
    /// Weights are non-negative for long-only portfolios and sum to 1.0.
    /// Models that intentionally produce leveraged weights (sum != 1.0) should implement
    /// <see cref="ILeveragedConstructionModel"/> instead, which relaxes this constraint.
    /// </returns>
    IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeights(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns);

    /// <summary>
    /// Computes the target weight for each asset as of an explicit evaluation date — the date-aware
    /// entry point a rebalance loop calls so that models (and decorators) holding date-indexed inputs
    /// can restrict themselves to observations known at the decision point.
    /// </summary>
    /// <param name="assets">The ordered list of assets to allocate across.</param>
    /// <param name="returns">
    /// A jagged array where <c>returns[i]</c> is the return series for <c>assets[i]</c>, ending at
    /// <paramref name="asOf"/> (the caller windows the series to the evaluation date, as the backtest
    /// rebalance loop already does for price returns).
    /// </param>
    /// <param name="asOf">
    /// The evaluation (decision) date of this computation, inclusive. Implementations that hold
    /// date-indexed inputs (for example a signal-tilt decorator bound to date-indexed auxiliary
    /// series) must not consume any observation dated after this date.
    /// </param>
    /// <returns>
    /// The same weight contract as
    /// <see cref="ComputeTargetWeights(IReadOnlyList{Symbol}, decimal[][])"/>: a dictionary mapping
    /// each asset to its target weight — non-negative and summing to 1.0 for long-only models.
    /// </returns>
    /// <remarks>
    /// <para><b>Default behavior:</b> forwards to the two-argument overload, ignoring
    /// <paramref name="asOf"/> — byte-identical for every model whose inputs are purely the
    /// caller-windowed <paramref name="returns"/> (all built-in base models). Only implementations
    /// that additionally hold date-indexed state need to override.</para>
    /// <para><b>Decorator obligation:</b> a decorator that wraps an inner
    /// <see cref="IPortfolioConstructionModel"/> should override this overload and forward
    /// <paramref name="asOf"/> to the inner model, so the evaluation date reaches a date-aware model
    /// anywhere in a composed chain.</para>
    /// </remarks>
    IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeights(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        DateOnly asOf)
        => ComputeTargetWeights(assets, returns);
}
