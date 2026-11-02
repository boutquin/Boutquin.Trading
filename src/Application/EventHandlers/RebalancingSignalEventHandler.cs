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

/// <summary>
/// Signal event handler that uses <see cref="TargetPortfolioDiffer"/> for rebalance signals,
/// ensuring sells execute before buys to free cash. Falls back to <see cref="SignalEventHandler"/>
/// for non-rebalance signal types. Orders that settle in a currency the strategy holds too little
/// of are funded by the <see cref="FxOrderEvent"/>s <see cref="FxFundingPlanner"/> plans under the
/// handler's <see cref="FxFundingPolicy"/>, emitted ahead of the asset orders.
/// </summary>
public sealed class RebalancingSignalEventHandler : IEventHandler
{
    private readonly SignalEventHandler _fallbackHandler;
    private readonly Currency _baseCurrency;
    private readonly decimal _minimumTradeValue;
    private readonly FxFundingPolicy _fxFundingPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="RebalancingSignalEventHandler"/> class.
    /// </summary>
    /// <param name="baseCurrency">The base currency for portfolio value calculations.</param>
    /// <param name="minimumTradeValue">Minimum trade notional value; orders below this are suppressed. Default 0 (no suppression).</param>
    /// <param name="fxFundingPolicy">How orders in a currency the strategy holds too little of are funded, on both the differ path and the fallback path. Defaults to <see cref="FxFundingPolicy.FundShortfall"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minimumTradeValue"/> is negative or <paramref name="fxFundingPolicy"/> is not a defined value.</exception>
    public RebalancingSignalEventHandler(
        Currency baseCurrency,
        decimal minimumTradeValue = 0m,
        FxFundingPolicy fxFundingPolicy = FxFundingPolicy.FundShortfall)
    {
        Guard.AgainstUndefinedEnumValue(() => baseCurrency);
        Guard.AgainstUndefinedEnumValue(() => fxFundingPolicy);

        if (minimumTradeValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumTradeValue), minimumTradeValue, "Minimum trade value cannot be negative.");
        }

        _baseCurrency = baseCurrency;
        _minimumTradeValue = minimumTradeValue;
        _fxFundingPolicy = fxFundingPolicy;
        _fallbackHandler = new SignalEventHandler(fxFundingPolicy);
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
        IReadOnlyDictionary<Symbol, decimal>? targetWeights = null;
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

        // Build current-price map for every asset the strategy tracks AND every target-weight
        // asset, using forward-fill (same cross-calendar rationale as StrategyBase/ClosePrice:
        // a CA portfolio rebalancing on a TSX trading day should use VTI's last-known NYSE
        // close if NYSE is closed that day). Without this fallback, the rebalance aborts
        // with "Missing price for asset 'VTI' which has a non-zero target weight" — fatal
        // to every CA backtest that holds US-listed ETFs across a US holiday.
        //
        // Prices are converted into BASE currency here, so the differ's arithmetic
        // (totalValue × weight / price, minimumTradeValue) is single-currency: a USD asset in
        // a CAD portfolio is priced in CAD, and the share count comes out right.
        portfolio.HistoricalMarketData.TryGetValue(signalEvent.Timestamp, out var dayData);

        var allAssetsNeedingPrices = new HashSet<Symbol>(strategy.Assets.Keys);
        foreach (var target in targetWeights.Keys)
        {
            allAssetsNeedingPrices.Add(target);
        }

        var currentPrices = new Dictionary<Symbol, decimal>();
        foreach (var asset in allAssetsNeedingPrices)
        {
            Bar? bar = null;
            if (dayData is not null && dayData.TryGetValue(asset, out var exactBar))
            {
                bar = exactBar;
            }
            else
            {
                bar = Strategies.HistoricalMarketDataLookup.TryFindMostRecentBar(
                    portfolio.HistoricalMarketData, asset, signalEvent.Timestamp);
            }
            if (bar is not null)
            {
                // The strategy knows the currency of every asset it trades; a target-weight
                // asset it does not yet hold is resolved through the portfolio's asset map.
                var assetCurrency = strategy.Assets.TryGetValue(asset, out var tracked)
                    ? tracked
                    : portfolio.GetAssetCurrency(asset);

                currentPrices[asset] = FxConversion.ToBase(
                    bar.Value.AdjustedClose,
                    assetCurrency,
                    _baseCurrency,
                    portfolio.HistoricalFxConversionRates,
                    signalEvent.Timestamp);
            }
        }

        if (currentPrices.Count == 0)
        {
            return; // Truly no data for any asset on this date — nothing to rebalance.
        }

        // Compute total portfolio value for this strategy
        var totalValue = strategy.ComputeTotalValue(
            signalEvent.Timestamp, _baseCurrency,
            portfolio.HistoricalMarketData, portfolio.HistoricalFxConversionRates);

        if (totalValue <= 0m)
        {
            return;
        }

        // Get current positions as Dictionary<Symbol, int>
        var currentPositions = new Dictionary<Symbol, int>();
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

        var orderEvents = new List<OrderEvent>();
        foreach (var order in rebalanceOrders)
        {
            var (orderType, primaryPrice, secondaryPrice) =
                strategy.OrderPriceCalculationStrategy.CalculateOrderPrices(
                    signalEvent.Timestamp,
                    order.Symbol,
                    order.TradeAction,
                    portfolio.HistoricalMarketData);

            orderEvents.Add(new OrderEvent(
                signalEvent.Timestamp,
                signalEvent.StrategyName,
                order.Symbol,
                order.TradeAction,
                orderType,
                order.Quantity,
                primaryPrice,
                secondaryPrice));
        }

        // FX conversions first: the brokerage fills them between this bar's asset sells and asset
        // buys, so a foreign sale can fund a base buy and base cash can fund a foreign buy.
        var fxOrders = FxFundingPlanner.Plan(
            signalEvent.Timestamp,
            strategy,
            _baseCurrency,
            SignalEventHandler.ExpectedTradeCashFlows(portfolio, strategy, orderEvents),
            portfolio.HistoricalFxConversionRates,
            _fxFundingPolicy);

        foreach (var fxOrder in fxOrders)
        {
            await portfolio.EventProcessor.ProcessEventAsync(fxOrder, cancellationToken).ConfigureAwait(false);
        }

        // Emit OrderEvents in differ order (sells first)
        foreach (var orderEvent in orderEvents)
        {
            await portfolio.EventProcessor.ProcessEventAsync(orderEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
