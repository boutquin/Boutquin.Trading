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

namespace Boutquin.Trading.Application.Helpers;

using Domain.ValueObjects;

/// <summary>
/// Computes rebalance orders by diffing target weights against current holdings.
/// Returns sells first, then buys (frees cash before allocating).
/// </summary>
public static class TargetPortfolioDiffer
{
    /// <summary>
    /// Computes rebalance orders by diffing target weights against current holdings.
    /// Returns sells first, then buys (frees cash before allocating).
    /// </summary>
    /// <param name="targetWeights">Target portfolio weights (asset → weight). May be empty for full liquidation.</param>
    /// <param name="currentPositions">Current share holdings (asset → quantity). May be empty for fresh portfolio.</param>
    /// <param name="currentPrices">Current prices per asset. Must contain prices for all assets in targetWeights.</param>
    /// <param name="totalPortfolioValue">Total portfolio value for computing target share counts.</param>
    /// <param name="minimumTradeValue">Minimum notional trade value; orders below this are suppressed. Default 0 (no suppression).</param>
    /// <returns>Rebalance orders sorted sells-first, then buys.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="totalPortfolioValue"/> is &lt;= 0 or <paramref name="minimumTradeValue"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a target asset has no price entry.</exception>
    public static IReadOnlyList<RebalanceOrder> ComputeRebalanceOrders(
        IReadOnlyDictionary<Asset, decimal> targetWeights,
        IReadOnlyDictionary<Asset, int> currentPositions,
        IReadOnlyDictionary<Asset, decimal> currentPrices,
        decimal totalPortfolioValue,
        decimal minimumTradeValue = 0m)
    {
        if (totalPortfolioValue <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPortfolioValue), totalPortfolioValue, "Total portfolio value must be positive.");
        }

        if (minimumTradeValue < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumTradeValue), minimumTradeValue, "Minimum trade value cannot be negative.");
        }

        // Union of all assets in target and current
        var allAssets = new HashSet<Asset>(targetWeights.Keys);
        foreach (var asset in currentPositions.Keys)
        {
            allAssets.Add(asset);
        }

        var orders = new List<RebalanceOrder>();

        foreach (var asset in allAssets)
        {
            var targetWeight = targetWeights.TryGetValue(asset, out var tw) ? tw : 0m;
            var currentShares = currentPositions.TryGetValue(asset, out var cs) ? cs : 0;

            // Price is required for assets with non-zero target weight
            if (targetWeight != 0m && !currentPrices.ContainsKey(asset))
            {
                throw new InvalidOperationException($"Missing price for asset '{asset}' which has a non-zero target weight.");
            }

            // For exit-only positions, price is needed to compute current weight
            if (!currentPrices.TryGetValue(asset, out var price))
            {
                // Asset being exited with no price — use 0 for current weight, target is 0
                // We still need to generate the sell order based on current shares
                if (currentShares > 0)
                {
                    orders.Add(new RebalanceOrder(asset, TradeAction.Sell, currentShares, 0m, 0m));
                }
                continue;
            }

            var targetShares = (int)Math.Round(totalPortfolioValue * targetWeight / price, MidpointRounding.AwayFromZero);
            var delta = targetShares - currentShares;
            var currentWeight = totalPortfolioValue != 0m ? currentShares * price / totalPortfolioValue : 0m;

            // Suppress orders below minimum trade value threshold
            if (delta != 0 && minimumTradeValue > 0m && Math.Abs(delta) * price < minimumTradeValue)
            {
                continue;
            }

            if (delta > 0)
            {
                orders.Add(new RebalanceOrder(asset, TradeAction.Buy, delta, targetWeight, currentWeight));
            }
            else if (delta < 0)
            {
                orders.Add(new RebalanceOrder(asset, TradeAction.Sell, Math.Abs(delta), targetWeight, currentWeight));
            }
            // delta == 0 → no order needed
        }

        // Sort: sells first, then buys (reverse enum order since Buy=0, Sell=1)
        orders.Sort((a, b) => b.TradeAction.CompareTo(a.TradeAction) is var cmp && cmp != 0
            ? cmp
            : a.Asset.CompareTo(b.Asset));

        return orders;
    }
}
