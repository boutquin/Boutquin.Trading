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

using Boutquin.Trading.Domain.Analytics;
using Boutquin.Trading.Domain.ValueObjects;

namespace Boutquin.Trading.Application.Analytics;

/// <summary>
/// Implements Brinson-Fachler performance attribution, decomposing active return
/// into allocation, selection, and interaction effects.
/// </summary>
/// <remarks>
/// Brinson-Fachler formula per sector i:
///   Allocation_i  = (Wp_i - Wb_i) × (Rb_i - Rb_total)
///   Selection_i   = Wb_i × (Rp_i - Rb_i)
///   Interaction_i = (Wp_i - Wb_i) × (Rp_i - Rb_i)
///
/// Total active return = Σ(Allocation_i) + Σ(Selection_i) + Σ(Interaction_i)
/// </remarks>
public static class BrinsonFachlerAttributor
{
    /// <summary>
    /// Computes Brinson-Fachler attribution for a single period.
    /// </summary>
    /// <param name="assetNames">The sector/asset class names.</param>
    /// <param name="portfolioWeights">Portfolio weight per sector.</param>
    /// <param name="benchmarkWeights">Benchmark weight per sector.</param>
    /// <param name="portfolioReturns">Portfolio return per sector.</param>
    /// <param name="benchmarkReturns">Benchmark return per sector.</param>
    /// <returns>A <see cref="BrinsonFachlerResult"/> with allocation, selection, and interaction effects.</returns>
    public static BrinsonFachlerResult Attribute(
        IReadOnlyList<Asset> assetNames,
        IReadOnlyDictionary<Asset, decimal> portfolioWeights,
        IReadOnlyDictionary<Asset, decimal> benchmarkWeights,
        IReadOnlyDictionary<Asset, decimal> portfolioReturns,
        IReadOnlyDictionary<Asset, decimal> benchmarkReturns)
    {
        Guard.AgainstNull(() => assetNames);
        Guard.AgainstNull(() => portfolioWeights);
        Guard.AgainstNull(() => benchmarkWeights);
        Guard.AgainstNull(() => portfolioReturns);
        Guard.AgainstNull(() => benchmarkReturns);

        // Validate all asset names exist in all dictionaries
        foreach (var asset in assetNames)
        {
            if (!portfolioWeights.ContainsKey(asset) ||
                !benchmarkWeights.ContainsKey(asset) ||
                !portfolioReturns.ContainsKey(asset) ||
                !benchmarkReturns.ContainsKey(asset))
            {
                throw new ArgumentException(
                    $"Asset '{asset}' is missing from one or more input dictionaries.",
                    nameof(assetNames));
            }
        }

        if (assetNames.Count == 0)
        {
            return new BrinsonFachlerResult(
                0m, 0m, 0m, 0m,
                new Dictionary<Asset, decimal>(),
                new Dictionary<Asset, decimal>(),
                new Dictionary<Asset, decimal>());
        }

        // Compute total benchmark return
        var benchmarkTotalReturn = assetNames.Sum(a => benchmarkWeights[a] * benchmarkReturns[a]);

        var allocationEffects = new Dictionary<Asset, decimal>();
        var selectionEffects = new Dictionary<Asset, decimal>();
        var interactionEffects = new Dictionary<Asset, decimal>();

        foreach (var asset in assetNames)
        {
            var wp = portfolioWeights[asset];
            var wb = benchmarkWeights[asset];
            var rp = portfolioReturns[asset];
            var rb = benchmarkReturns[asset];

            allocationEffects[asset] = (wp - wb) * (rb - benchmarkTotalReturn);
            selectionEffects[asset] = wb * (rp - rb);
            interactionEffects[asset] = (wp - wb) * (rp - rb);
        }

        var totalAllocation = allocationEffects.Values.Sum();
        var totalSelection = selectionEffects.Values.Sum();
        var totalInteraction = interactionEffects.Values.Sum();
        var totalActiveReturn = totalAllocation + totalSelection + totalInteraction;

        return new BrinsonFachlerResult(
            totalAllocation,
            totalSelection,
            totalInteraction,
            totalActiveReturn,
            allocationEffects,
            selectionEffects,
            interactionEffects);
    }
}
