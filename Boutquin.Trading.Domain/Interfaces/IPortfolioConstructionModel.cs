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

using ValueObjects;

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
    /// </returns>
    IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
        IReadOnlyList<Asset> assets,
        decimal[][] returns);
}
