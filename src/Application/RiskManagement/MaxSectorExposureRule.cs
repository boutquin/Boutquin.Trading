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

namespace Boutquin.Trading.Application.RiskManagement;

using Domain.ValueObjects;

/// <summary>
/// Rejects orders that would cause total exposure to any asset class
/// to exceed a configured percentage of total portfolio value.
/// Asset classes are determined by a user-provided mapping.
/// </summary>
public sealed class MaxSectorExposureRule : IRiskRule
{
    /// <summary>
    /// Tolerance for exposure comparison (10 basis points = 0.10%).
    /// Sector exposure aggregates multiple positions, each with its own share-count
    /// rounding error (~0.5bp per position). With 2-3 assets per sector, combined
    /// error can reach 1-2bp — exceeding the 1bp tolerance that works for single
    /// positions. 10bp absorbs multi-position rounding while still catching real
    /// limit violations.
    /// </summary>
    private const decimal Tolerance = 0.001m;

    private readonly decimal _maxExposurePercent;
    private readonly IReadOnlyDictionary<Asset, AssetClassCode> _assetClassMap;

    /// <summary>
    /// Initializes a new instance of <see cref="MaxSectorExposureRule"/>.
    /// </summary>
    /// <param name="maxExposurePercent">
    /// The maximum allowable exposure per asset class as a fraction of portfolio value (e.g., 0.40 for 40%).
    /// </param>
    /// <param name="assetClassMap">
    /// A mapping from asset to its asset class code.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxExposurePercent"/> is not between 0 (exclusive) and 1 (inclusive).
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assetClassMap"/> is null.
    /// </exception>
    public MaxSectorExposureRule(
        decimal maxExposurePercent,
        IReadOnlyDictionary<Asset, AssetClassCode> assetClassMap)
    {
        if (maxExposurePercent is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExposurePercent),
                maxExposurePercent,
                "Max exposure percent must be between 0 (exclusive) and 1 (inclusive).");
        }

        _maxExposurePercent = maxExposurePercent;
        _assetClassMap = assetClassMap ?? throw new ArgumentNullException(nameof(assetClassMap));
    }

    /// <inheritdoc />
    public string Name => "MaxSectorExposure";

    /// <inheritdoc />
    public RiskEvaluation Evaluate(Order order, IPortfolio portfolio)
    {
        Guard.AgainstNull(() => order);
        Guard.AgainstNull(() => portfolio);

        var equityCurve = portfolio.EquityCurve;
        if (equityCurve.Count == 0)
        {
            return RiskEvaluation.Allowed;
        }

        var totalPortfolioValue = equityCurve.Values.Last();
        if (totalPortfolioValue <= 0)
        {
            return RiskEvaluation.Allowed;
        }

        // Determine the asset class of the order's asset
        if (!_assetClassMap.TryGetValue(order.Asset, out var orderAssetClass))
        {
            return RiskEvaluation.Rejected(
                $"Asset '{order.Asset}' has no asset class mapping; cannot evaluate sector exposure.");
        }

        var latestMarketData = portfolio.HistoricalMarketData.Values.LastOrDefault();
        if (latestMarketData is null)
        {
            return RiskEvaluation.Rejected(
                "No market data available to evaluate sector exposure.");
        }

        // Sum current exposure for this asset class across all strategies,
        // tracking the order's asset position separately for post-trade adjustment.
        var sectorExposure = 0m;
        var existingOrderAssetQty = 0;

        foreach (var strategy in portfolio.Strategies.Values)
        {
            foreach (var (asset, qty) in strategy.Positions)
            {
                if (_assetClassMap.TryGetValue(asset, out var assetClass) &&
                    assetClass == orderAssetClass &&
                    latestMarketData.TryGetValue(asset, out var md))
                {
                    sectorExposure += Math.Abs(qty * md.AdjustedClose);

                    if (asset == order.Asset)
                    {
                        existingOrderAssetQty += qty;
                    }
                }
            }
        }

        // Compute post-trade exposure: adjust the order's asset from existing to post-trade quantity.
        if (latestMarketData.TryGetValue(order.Asset, out var orderMarketData))
        {
            var postTradeQty = order.TradeAction == TradeAction.Buy
                ? existingOrderAssetQty + order.Quantity
                : existingOrderAssetQty - order.Quantity;

            sectorExposure = sectorExposure
                - Math.Abs(existingOrderAssetQty * orderMarketData.AdjustedClose)
                + Math.Abs(postTradeQty * orderMarketData.AdjustedClose);
        }

        var exposurePercent = sectorExposure / totalPortfolioValue;

        if (exposurePercent > _maxExposurePercent + Tolerance)
        {
            return RiskEvaluation.Rejected(
                $"Exposure to {orderAssetClass} would be {exposurePercent:P2}, exceeding maximum {_maxExposurePercent:P2}.");
        }

        return RiskEvaluation.Allowed;
    }

    /// <summary>
    /// Evaluates the projected sector exposures after ALL orders in the batch.
    /// Computes the net position deltas per asset, then sums sector-level exposure
    /// to check whether any sector exceeds the limit.
    /// </summary>
    public RiskEvaluation EvaluateBatch(IReadOnlyList<Order> orders, IPortfolio portfolio)
    {
        Guard.AgainstNull(() => orders);
        Guard.AgainstNull(() => portfolio);

        var equityCurve = portfolio.EquityCurve;
        if (equityCurve.Count == 0)
        {
            return RiskEvaluation.Allowed;
        }

        var totalPortfolioValue = equityCurve.Values.Last();
        if (totalPortfolioValue <= 0)
        {
            return RiskEvaluation.Allowed;
        }

        var latestMarketData = portfolio.HistoricalMarketData.Values.LastOrDefault();
        if (latestMarketData is null)
        {
            return RiskEvaluation.Rejected(
                "No market data available to evaluate sector exposure.");
        }

        // Verify all orders have asset class mappings
        foreach (var order in orders)
        {
            if (!_assetClassMap.ContainsKey(order.Asset))
            {
                return RiskEvaluation.Rejected(
                    $"Asset '{order.Asset}' has no asset class mapping; cannot evaluate sector exposure.");
            }
        }

        // 1. Collect current positions across all strategies
        var currentPositions = new Dictionary<Asset, int>();
        foreach (var strategy in portfolio.Strategies.Values)
        {
            foreach (var (asset, qty) in strategy.Positions)
            {
                currentPositions[asset] = currentPositions.GetValueOrDefault(asset) + qty;
            }
        }

        // 2. Compute net deltas from the entire batch
        var deltas = new Dictionary<Asset, int>();
        foreach (var order in orders)
        {
            var delta = order.TradeAction == TradeAction.Buy
                ? order.Quantity
                : -order.Quantity;
            deltas[order.Asset] = deltas.GetValueOrDefault(order.Asset) + delta;
        }

        // 3. Compute projected positions
        var projectedPositions = new Dictionary<Asset, int>(currentPositions);
        foreach (var (asset, delta) in deltas)
        {
            projectedPositions[asset] = projectedPositions.GetValueOrDefault(asset) + delta;
        }

        // 4. Sum exposure by sector using projected positions
        var sectorExposures = new Dictionary<AssetClassCode, decimal>();
        foreach (var (asset, qty) in projectedPositions)
        {
            if (qty == 0)
            {
                continue;
            }

            if (!_assetClassMap.TryGetValue(asset, out var assetClass))
            {
                continue;
            }

            if (!latestMarketData.TryGetValue(asset, out var md))
            {
                continue;
            }

            var value = Math.Abs(qty * md.AdjustedClose);
            sectorExposures[assetClass] = sectorExposures.GetValueOrDefault(assetClass) + value;
        }

        // 5. Check each sector against the limit
        foreach (var (sector, exposure) in sectorExposures)
        {
            var exposurePercent = exposure / totalPortfolioValue;
            if (exposurePercent > _maxExposurePercent + Tolerance)
            {
                return RiskEvaluation.Rejected(
                    $"Exposure to {sector} would be {exposurePercent:P2} after batch, exceeding maximum {_maxExposurePercent:P2}.");
            }
        }

        return RiskEvaluation.Allowed;
    }
}
