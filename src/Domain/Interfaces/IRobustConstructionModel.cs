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
/// Extends <see cref="IPortfolioConstructionModel"/> to accept multiple covariance scenarios
/// for robust (minimax) optimization. The model optimizes for the worst-case scenario,
/// producing weights that are resilient to regime shifts.
/// </summary>
public interface IRobustConstructionModel : IPortfolioConstructionModel
{
    /// <summary>
    /// Computes target weights that maximize the worst-case risk-adjusted return
    /// across a set of covariance scenarios.
    /// </summary>
    /// <param name="assets">The ordered list of assets to allocate across.</param>
    /// <param name="returns">Return series per asset (used for mean return estimation).</param>
    /// <param name="covarianceScenarios">
    /// A list of NxN covariance matrices representing different market regimes
    /// (e.g., normal conditions, GFC 2008, rate shock 2022).
    /// </param>
    /// <returns>Target weight dictionary optimized for worst-case scenario.</returns>
    IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
        IReadOnlyList<Asset> assets,
        decimal[][] returns,
        IReadOnlyList<decimal[,]> covarianceScenarios);
}
