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

namespace Boutquin.Trading.Application.EventHandlers;

using Boutquin.Trading.Application.Helpers;
using Boutquin.Trading.Application.Strategies;
using Domain.ValueObjects;

/// <summary>
/// Signal event handler that uses <see cref="TargetPortfolioDiffer"/> for rebalance signals,
/// ensuring sells execute before buys to free cash. Falls back to <see cref="SignalEventHandler"/>
/// for non-rebalance signal types.
/// </summary>
public sealed class RebalancingSignalEventHandler : IEventHandler
{
    private readonly SignalEventHandler _fallbackHandler = new();
    private readonly CurrencyCode _baseCurrency;
    private readonly decimal _minimumTradeValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="RebalancingSignalEventHandler"/> class.
    /// </summary>
    /// <param name="baseCurrency">The base currency for portfolio value calculations.</param>
    /// <param name="minimumTradeValue">Minimum trade notional value; orders below this are suppressed. Default 0 (no suppression).</param>
    public RebalancingSignalEventHandler(CurrencyCode baseCurrency, decimal minimumTradeValue = 0m)
    {
        Guard.AgainstUndefinedEnumValue(() => baseCurrency);

        if (minimumTradeValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumTradeValue), minimumTradeValue, "Minimum trade value cannot be negative.");
        }

        _baseCurrency = baseCurrency;
        _minimumTradeValue = minimumTradeValue;
    }

    /// <inheritdoc />
    public async Task HandleEventAsync(IPortfolio portfolio, IFinancialEvent eventObj, CancellationToken cancellationToken)
    {
        Guard.AgainstNull(() => portfolio);

        var signalEvent = eventObj as SignalEvent
            ?? throw new ArgumentException("Event must be of type SignalEvent.", nameof(eventObj));

        cancellationToken.ThrowIfCancellationRequested();

        // Only use differ path when ALL signals are Rebalance type
        var allRebalance = signalEvent.Signals.Count > 0 &&
                           signalEvent.Signals.Values.All(s => s == SignalType.Rebalance);

        if (!allRebalance)
        {
            await _fallbackHandler.HandleEventAsync(portfolio, eventObj, cancellationToken).ConfigureAwait(false);
            return;
        }

        var strategy = portfolio.GetStrategy(signalEvent.StrategyName);

        // Get target weights from ConstructionModelStrategy
        IReadOnlyDictionary<Asset, decimal>? targetWeights = null;
        if (strategy is ConstructionModelStrategy cms)
        {
            targetWeights = cms.LastComputedWeights;
        }

        if (targetWeights is null)
        {
            // Not a ConstructionModelStrategy or no weights computed yet — fall back
            await _fallbackHandler.HandleEventAsync(portfolio, eventObj, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Get current prices from historical market data
        if (!portfolio.HistoricalMarketData.TryGetValue(signalEvent.Timestamp, out var dayData) || dayData is null)
        {
            return; // No market data for this date
        }

        var currentPrices = new Dictionary<Asset, decimal>();
        foreach (var (asset, md) in dayData)
        {
            currentPrices[asset] = md.AdjustedClose;
        }

        // Compute total portfolio value for this strategy
        var totalValue = strategy.ComputeTotalValue(
            signalEvent.Timestamp, _baseCurrency,
            portfolio.HistoricalMarketData, portfolio.HistoricalFxConversionRates);

        if (totalValue <= 0m)
        {
            return;
        }

        // Get current positions as Dictionary<Asset, int>
        var currentPositions = new Dictionary<Asset, int>();
        foreach (var asset in strategy.Assets.Keys)
        {
            var qty = strategy.Positions.GetValueOrDefault(asset, 0);
            if (qty != 0)
            {
                currentPositions[asset] = qty;
            }
        }

        // Compute rebalance orders (sells first, then buys)
        var rebalanceOrders = TargetPortfolioDiffer.ComputeRebalanceOrders(
            targetWeights, currentPositions, currentPrices, totalValue, _minimumTradeValue);

        // Emit OrderEvents in differ order (sells first)
        foreach (var order in rebalanceOrders)
        {
            var (orderType, primaryPrice, secondaryPrice) =
                strategy.OrderPriceCalculationStrategy.CalculateOrderPrices(
                    signalEvent.Timestamp,
                    order.Asset,
                    order.TradeAction,
                    portfolio.HistoricalMarketData);

            var orderEvent = new OrderEvent(
                signalEvent.Timestamp,
                signalEvent.StrategyName,
                order.Asset,
                order.TradeAction,
                orderType,
                order.Quantity,
                primaryPrice,
                secondaryPrice);

            await portfolio.EventProcessor.ProcessEventAsync(orderEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
