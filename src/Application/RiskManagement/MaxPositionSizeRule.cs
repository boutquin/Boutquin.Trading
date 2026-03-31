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
/// Rejects orders that would cause any single position to exceed a configured
/// percentage of total portfolio value.
/// </summary>
public sealed class MaxPositionSizeRule : IRiskRule
{
    /// <summary>
    /// Tolerance for percentage comparison (1 basis point = 0.01%).
    /// Discrete share quantities make exact percentage targets impossible;
    /// without tolerance, a computed 25.0000001% rejects against a 25% limit.
    /// </summary>
    private const decimal Tolerance = 0.0001m;

    private readonly decimal _maxPositionPercent;

    /// <summary>
    /// Initializes a new instance of <see cref="MaxPositionSizeRule"/>.
    /// </summary>
    /// <param name="maxPositionPercent">
    /// The maximum allowable position size as a fraction of portfolio value (e.g., 0.25 for 25%).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxPositionPercent"/> is not between 0 (exclusive) and 1 (inclusive).
    /// </exception>
    public MaxPositionSizeRule(decimal maxPositionPercent)
    {
        if (maxPositionPercent is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPositionPercent),
                maxPositionPercent,
                "Max position percent must be between 0 (exclusive) and 1 (inclusive).");
        }

        _maxPositionPercent = maxPositionPercent;
    }

    /// <inheritdoc />
    public string Name => "MaxPositionSize";

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

        // Estimate the value of the proposed order
        var latestMarketData = portfolio.HistoricalMarketData.Values.LastOrDefault();
        if (latestMarketData is null || !latestMarketData.TryGetValue(order.Asset, out var marketData))
        {
            return RiskEvaluation.Allowed;
        }

        // Get existing position value for this asset across all strategies
        var existingQuantity = 0;
        foreach (var strategy in portfolio.Strategies.Values)
        {
            if (strategy.Positions.TryGetValue(order.Asset, out var qty))
            {
                existingQuantity += qty;
            }
        }

        // Compute the post-order position value using AdjustedClose for consistency
        // with the rest of the engine (equity curve, position sizer, strategy all use AdjustedClose).
        // Using raw Close causes spurious rejections when dividend-adjusted data diverges.
        var orderQuantity = order.TradeAction == TradeAction.Buy
            ? order.Quantity
            : -order.Quantity;
        var postOrderQuantity = existingQuantity + orderQuantity;
        var postOrderValue = Math.Abs(postOrderQuantity * marketData.AdjustedClose);
        var positionPercent = postOrderValue / totalPortfolioValue;

        if (positionPercent > _maxPositionPercent + Tolerance)
        {
            return RiskEvaluation.Rejected(
                $"Position in {order.Asset} would be {positionPercent:P2} of portfolio, exceeding maximum {_maxPositionPercent:P2}.");
        }

        return RiskEvaluation.Allowed;
    }

    /// <summary>
    /// Evaluates the projected position sizes after ALL orders in the batch.
    /// Computes the net delta for each asset across the entire batch, then checks
    /// whether any resulting position exceeds the limit.
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
            return RiskEvaluation.Allowed;
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

        // 3. Check projected position for each affected asset
        foreach (var (asset, delta) in deltas)
        {
            if (!latestMarketData.TryGetValue(asset, out var marketData))
            {
                continue;
            }

            var currentQty = currentPositions.GetValueOrDefault(asset);
            var projectedQty = currentQty + delta;
            var projectedValue = Math.Abs(projectedQty * marketData.AdjustedClose);
            var positionPercent = projectedValue / totalPortfolioValue;

            if (positionPercent > _maxPositionPercent + Tolerance)
            {
                return RiskEvaluation.Rejected(
                    $"Position in {asset} would be {positionPercent:P2} of portfolio after batch, exceeding maximum {_maxPositionPercent:P2}.");
            }
        }

        return RiskEvaluation.Allowed;
    }
}
