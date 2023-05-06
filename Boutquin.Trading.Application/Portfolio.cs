// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

namespace Boutquin.Trading.Application;

using Boutquin.Domain.Exceptions;
using Boutquin.Domain.Helpers;
using Domain.Data;
using Domain.Enums;
using Domain.Events;
using Boutquin.Trading.Domain.Interfaces;

/// <summary>
/// Represents a trading portfolio that consists of multiple strategies and assets.
/// The Portfolio class is responsible for managing the assets, positions, capital allocation,
/// and risk management for the strategies in the portfolio. It also handles various types
/// of events such as market, dividend, signal, order, and fill events and updates the portfolio
/// state accordingly. The Portfolio class maintains an equity curve that represents the value
/// of the portfolio over time.
/// </summary>
public sealed class Portfolio
{
    private readonly IBrokerage _broker;

    /// <summary>
    /// Stores the cash balance for each strategy in the portfolio.
    /// </summary>
    /// <remarks>
    /// The key is the strategy name, and the value is the cash balance.
    /// </remarks>
    private readonly Dictionary<string, decimal> _cash; // Strategy -> Cash

    /// <summary>
    /// Stores the positions for each asset and strategy in the portfolio.
    /// </summary>
    /// <remarks>
    /// The outer dictionary key represents the asset, and the inner dictionary key represents the strategy name.
    /// The inner dictionary value is the position (number of units held) for that asset and strategy.
    /// </remarks>
    private readonly Dictionary<string, Dictionary<string, int>> _positions; // Asset -> Strategy -> Position

    /// <summary>
    /// Stores the latest market data for each asset in the portfolio.
    /// </summary>
    /// <remarks>
    /// The key represents the asset, and the value represents the latest market data for that asset.
    /// </remarks>
    private readonly SortedDictionary<string, MarketData> _latestMarketData; // Asset -> MarketData

    /// <summary>
    /// Stores the current executing strategy for the portfolio.
    /// </summary>
    private IStrategy _currentExecutingStrategy;

    /// <summary>
    /// Retrieves the portfolio's equity curve, represented as a SortedDictionary with DateTime keys and decimal values.
    /// The equity curve represents the value of the portfolio over time, where the keys are the timestamps of events
    /// and the values are the total equity at each timestamp.
    /// </summary>
    public SortedDictionary<DateOnly, decimal> EquityCurve { get; } = new();

    /// <summary>
    /// Retrieves the list of trading strategies in the portfolio.
    /// </summary>
    public List<IStrategy> Strategies { get; }

    /// <summary>
    /// Initializes a new instance of the Portfolio class with a list of trading strategies.
    /// </summary>
    /// <param name="strategies">A list of objects implementing the IStrategy interface, representing the trading strategies in the portfolio.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided strategies list is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the provided strategies list is empty.</exception>
    public Portfolio(List<IStrategy> strategies)
    {
        Strategies = strategies ?? throw new ArgumentNullException(nameof(strategies), "The provided strategies list cannot be null.");
        if (Strategies.Count == 0)
        {
            throw new ArgumentException("The provided strategies list cannot be empty.", nameof(strategies));
        }

        _positions = new Dictionary<string, Dictionary<string, int>>();
    }

    /// <summary>
    /// Updates the equity curve of the portfolio at a specific timestamp. The method calculates the total equity
    /// at the given timestamp and adds an entry to the EquityCurve SortedDictionary.
    /// </summary>
    /// <param name="timestamp">The DateTime representing the timestamp at which the equity curve should be updated.</param>
    /// <exception cref="ArgumentException">Thrown when the provided timestamp is earlier than the last entry in the equity curve.</exception>
    public void UpdateEquityCurve(DateOnly timestamp)
    {
        if (EquityCurve.Count > 0 && timestamp < EquityCurve.Keys.Last())
        {
            throw new ArgumentException("Timestamp must be equal to or greater than the last entry in the equity curve.", nameof(timestamp));
        }

        var totalEquity = CalculateTotalEquity();
        EquityCurve[timestamp] = totalEquity;
    }

    /// <summary>
    /// Calculates the total equity of the portfolio by summing the equities of all the strategies in the portfolio.
    /// </summary>
    /// <returns>A decimal value representing the total equity of the portfolio.</returns>
    public decimal CalculateTotalEquity()
    {
        if (Strategies.Count == 0)
        {
            throw new InvalidOperationException("The portfolio must have at least one strategy.");
        }

        var totalEquity = Strategies.Sum(strategy => strategy.CalculateEquity());
        return totalEquity;
    }

    /// <summary>
    /// Adds a strategy to the portfolio.
    /// </summary>
    /// <param name="strategy">The strategy to be added to the portfolio.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="strategy"/> is null.</exception>
    public void AddStrategy(IStrategy strategy)
    {
        // Ensure that the strategy is not null.
        Guard.AgainstNull(() => strategy);

        Strategies.Add(strategy);
        
        var strategyPositions = new Dictionary<string, int>();
        foreach (var asset in strategy.Assets)
        {
            strategyPositions[asset] = 0;
        }
        _positions[strategy.Name] = strategyPositions;
    }


    /// <summary>
    /// Handles events based on the type of the event object.
    /// </summary>
    /// <param name="eventObj">The event object to be processed.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="eventObj"/> is null.</exception>
    /// <remarks>
    /// The method processes different types of events by calling appropriate handling methods.
    /// It supports MarketEvent, SignalEvent, OrderEvent, FillEvent, RebalancingEvent,
    /// and DividendEvent.
    /// </remarks>
    public async Task HandleEventAsync(IEvent eventObj)
    {
        // Ensure that the rebalancingEvent is not null.
        Guard.AgainstNull(() => eventObj); // Throws ArgumentNullException when the eventObj parameter is null

        switch (eventObj)
        {
            case MarketEvent marketEvent:
                await HandleMarketEventAsync(marketEvent);
                break;
            case SignalEvent signalEvent:
                await HandleSignalEventAsync(signalEvent);
                break;
            case OrderEvent orderEvent:
                await HandleOrderEventAsync(orderEvent);
                break;
            case FillEvent fillEvent:
                await HandleFillEventAsync(fillEvent);
                break;
            case RebalancingEvent rebalancingEvent:
                await HandleRebalancingEventAsync(rebalancingEvent);
                break;
            case SplitEvent splitEvent:
                await HandleSplitEventAsync(splitEvent);
                break;
            case DividendEvent dividendEvent:
                await HandleDividendEventAsync(dividendEvent);
                break;
            default:
                throw new NotSupportedException($"Unsupported event type: {eventObj.GetType()}");
        }
    }
    
    /// <summary>
    /// Handles a <see cref="MarketEvent"/> by updating the latest market data for the asset
    /// in the <see cref="_latestMarketData"/> dictionary.
    /// </summary>
    /// <param name="marketEvent">The market event containing the asset and its updated market data.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="marketEvent"/> is null.</exception>
    private Task HandleMarketEventAsync(MarketEvent marketEvent)
    {
        // Ensure that the market event is not null.
        Guard.AgainstNull(() => marketEvent);

        // Update the latest market data for the asset.
        _latestMarketData[marketEvent.Asset] = marketEvent.MarketData;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the SignalEvent by creating an OrderEvent based on the signal type.
    /// </summary>
    /// <param name="signalEvent">The SignalEvent to be processed.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="signalEvent"/> is null.</exception>
    private async Task HandleSignalEventAsync (SignalEvent signalEvent)
    {
        // Ensure that the signal event is not null.
        if (signalEvent == null) throw new ArgumentNullException(nameof(signalEvent));

        // Get the position for the current strategy and asset
        var currentPosition = _positions[signalEvent.Asset][_currentExecutingStrategy.Name];

        // Determine the order type and quantity based on the signal type
        TradeAction tradeAction;
        int quantity;

        // Calculate the total value of the portfolio
        var portfolioTotalValue = EquityCurve[signalEvent.Timestamp];

        switch (signalEvent.SignalType)
        {
            case SignalType.Long:
                tradeAction = TradeAction.Buy;
                quantity = _currentExecutingStrategy.PositionSizer.GetPositionSize(signalEvent.Asset, portfolioTotalValue);
                break;
            case SignalType.Short:
                tradeAction = TradeAction.Sell;
                quantity = _currentExecutingStrategy.PositionSizer.GetPositionSize(signalEvent.Asset, portfolioTotalValue);
                break;
            case SignalType.Exit:
                tradeAction = currentPosition > 0 ? TradeAction.Sell : TradeAction.Buy;
                quantity = Math.Abs(currentPosition);
                break;
            default:
                throw new InvalidOperationException("Unknown signal type.");
        }

        // Create an OrderEvent
        var orderEvent = new OrderEvent(
            signalEvent.Timestamp,
            signalEvent.StrategyName,
            signalEvent.Asset,
            tradeAction,
            OrderType.Market,
            quantity
        );

        // Handle the OrderEvent
        await HandleOrderEventAsync(orderEvent);
    }

    /// <summary>
    /// Handles an OrderEvent by submitting an order to the brokerage for execution.
    /// Ensures that the OrderEvent is not null, retrieves the latest market data for the asset,
    /// and creates an Order object that is submitted to the brokerage.
    /// </summary>
    /// <param name="orderEvent">The OrderEvent to handle, represented as an OrderEvent object.</param>
    private async Task HandleOrderEventAsync(OrderEvent orderEvent)
    {
        // Ensure that the OrderEvent is not null.
        if (orderEvent is null)
        {
            throw new ArgumentNullException(nameof(orderEvent), "OrderEvent cannot be null.");
        }

        // Get the latest market data for the asset
        if (!_latestMarketData.TryGetValue(orderEvent.Asset, out var marketData))
        {
            // Market data for the asset not found, handle the error as appropriate for your application.
            // For example, log the error or throw an exception.
            throw new InvalidOperationException($"Market data for asset {orderEvent.Asset} not found.");
        }

        // Create an Order object using the information from the OrderEvent and the latest market data.
        // Price is only set for Limit and StopLimit orders.
        var order = new Order(
            orderEvent.Timestamp,
            orderEvent.StrategyName,
            orderEvent.Asset,
            orderEvent.TradeAction,
            orderEvent.OrderType,
            orderEvent.Quantity,
            orderEvent.Price);

        // Submit the order to the brokerage for execution.
        var orderSubmitted = await _broker.SubmitOrderAsync(order);

        // Check if the order submission was successful and handle the result as appropriate for your application.
        // For example, log the result or throw an exception if the submission failed.
        if (!orderSubmitted)
        {
            throw new InvalidOperationException($"Order submission for asset {orderEvent.Asset} failed.");
        }
    }

    /// <summary>
    /// Handles a fill event by updating the position, calculating the transaction cost, and updating the cash balance.
    /// </summary>
    /// <param name="fillEvent">The fill event to be processed.</param>
    /// <remarks>
    /// This method is used to update the portfolio's positions and cash balance based on the fill event received.
    /// A positive fill event quantity represents a buy, while a negative quantity represents a sell.
    /// The transaction cost is calculated as the product of fill price and fill quantity, plus the commission.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="fillEvent"/> is null.</exception>
    private async Task HandleFillEventAsync(FillEvent fillEvent)
    {
        // Ensure that the FillEvent is not null.
        Guard.AgainstNull(() => fillEvent);

        // Update the position
        _positions[fillEvent.Asset][fillEvent.StrategyName] += fillEvent.Quantity;

        // Calculate the transaction cost (including commission)
        var transactionCost = fillEvent.FillPrice * fillEvent.Quantity + fillEvent.Commission;

        // Update the cash balance
        if (fillEvent.Quantity > 0)
        {
            _cash[fillEvent.StrategyName] -= transactionCost;
        }
        else
        {
            _cash[fillEvent.StrategyName] += transactionCost;
        }
    }

    /// <summary>
    /// Sets the currently executing strategy for the portfolio.
    /// </summary>
    /// <param name="strategy">The strategy to be set as the current executing strategy.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="strategy"/> is null.</exception>
    private void SetCurrentExecutingStrategy(IStrategy strategy)
    {
        // Ensure that the strategy is not null.
        Guard.AgainstNull(() => strategy);

        _currentExecutingStrategy = strategy;
    }

    /// <summary>
    /// Processes the dividend event for all strategies in the portfolio.
    /// </summary>
    /// <param name="dividendEvent">The dividend event to be processed.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="dividendEvent"/> is null.</exception>
    /// <remarks>
    /// The method iterates through all strategies and updates the cash balance
    /// based on the dividend amount for each strategy holding the asset.
    /// </remarks>
    private async Task HandleDividendEventAsync(DividendEvent dividendEvent)
    {
        // Ensure that the dividendEvent is not null.
        Guard.AgainstNull(() => dividendEvent);

        // Iterate through strategies to process the dividend event
        foreach (var strategy in Strategies)
        {
            var position = _positions[dividendEvent.Asset][strategy.Name];
            if (position > 0)
            {
                var dividendAmount = position * dividendEvent.DividendPerShare;
                _cash[strategy.Name] += dividendAmount;
            }
        }
    }

    /// <summary>
    /// Rebalances the portfolio for each asset in the strategy based on the given rebalancing event.
    /// </summary>
    /// <param name="rebalancingEvent">The event that triggers the portfolio rebalancing.</param>
    /// <exception cref="ArgumentNullException">Thrown when the rebalancingEvent parameter is null.</exception>
    /// <remarks>
    /// This method is responsible for calculating the required quantity adjustments for each asset in the
    /// strategy and executing order events to rebalance the portfolio accordingly. It first calculates
    /// the total value of the portfolio and the equity for the current executing strategy. Then, for each asset
    /// in the rebalancing event, it calculates the target weight, target value, current position value,
    /// and required quantity adjustment. If the quantity adjustment is zero, it continues to the next asset.
    /// Otherwise, it creates and executes an order event for the quantity adjustment using the current
    /// executing strategy's slippage and commission models.
    /// </remarks>
    private async Task HandleRebalancingEventAsync(RebalancingEvent rebalancingEvent)
    {
        // Ensure that the rebalancingEvent is not null.
        Guard.AgainstNull(() => rebalancingEvent); // Throws ArgumentNullException when the rebalancingEvent parameter is null

        //...
    }
    private async Task HandleSplitEventAsync(SplitEvent splitEvent)
    {
        // Ensure that the rebalancingEvent is not null.
        Guard.AgainstNull(() => splitEvent); // Throws ArgumentNullException when the splitEvent parameter is null

        //...
    }
}
