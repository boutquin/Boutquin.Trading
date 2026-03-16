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
            // Unknown asset class — cannot enforce rule, allow by default
            return RiskEvaluation.Allowed;
        }

        var latestMarketData = portfolio.HistoricalMarketData.Values.LastOrDefault();
        if (latestMarketData is null)
        {
            return RiskEvaluation.Allowed;
        }

        // Sum current exposure for this asset class across all strategies
        var sectorExposure = 0m;
        foreach (var strategy in portfolio.Strategies.Values)
        {
            foreach (var (asset, qty) in strategy.Positions)
            {
                if (_assetClassMap.TryGetValue(asset, out var assetClass) &&
                    assetClass == orderAssetClass &&
                    latestMarketData.TryGetValue(asset, out var md))
                {
                    sectorExposure += Math.Abs(qty * md.Close);
                }
            }
        }

        // Add the proposed order's value
        if (latestMarketData.TryGetValue(order.Asset, out var orderMarketData))
        {
            var orderValue = order.TradeAction == TradeAction.Buy
                ? order.Quantity * orderMarketData.Close
                : -(order.Quantity * orderMarketData.Close);
            sectorExposure += orderValue;
        }

        var exposurePercent = sectorExposure / totalPortfolioValue;

        if (exposurePercent > _maxExposurePercent)
        {
            return RiskEvaluation.Rejected(
                $"Exposure to {orderAssetClass} would be {exposurePercent:P2}, exceeding maximum {_maxExposurePercent:P2}.");
        }

        return RiskEvaluation.Allowed;
    }
}
