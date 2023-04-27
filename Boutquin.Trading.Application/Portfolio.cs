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

using Boutquin.Domain.Helpers;
using Boutquin.Trading.Domain.Data;
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Events;
using Boutquin.Trading.Domain.Interfaces;

namespace Boutquin.Trading.Application;

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
    /// <summary>
    /// Stores the cash balance for each strategy in the portfolio.
    /// </summary>
    /// <remarks>
    /// The key is the strategy name, and the value is the cash balance.
    /// </remarks>
    private readonly Dictionary<string, decimal> _cash;

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
    private readonly Dictionary<string, MarketData> _latestMarketData;

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
    public void HandleEvent(IEvent eventObj)
    {
        // Ensure that the event object is not null.
        if (eventObj == null) throw new ArgumentNullException(nameof(eventObj));

        switch (eventObj)
        {
            case MarketEvent marketEvent:
                HandleMarketEvent(marketEvent);
                break;
            case SignalEvent signalEvent:
                HandleSignalEvent(signalEvent);
                break;
            case OrderEvent orderEvent:
                HandleOrderEvent(orderEvent);
                break;
            case FillEvent fillEvent:
                HandleFillEvent(fillEvent);
                break;
            case DividendEvent dividendEvent:
                HandleDividendEvent(dividendEvent);
                break;
            case RebalancingEvent rebalancingEvent:
                HandleRebalancingEvent(rebalancingEvent);
                break;
        }
    }
    
    /// <summary>
    /// Handles a <see cref="MarketEvent"/> by updating the latest market data for the asset
    /// in the <see cref="_latestMarketData"/> dictionary.
    /// </summary>
    /// <param name="marketEvent">The market event containing the asset and its updated market data.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="marketEvent"/> is null.</exception>
    private void HandleMarketEvent(MarketEvent marketEvent)
    {
        // Ensure that the market event is not null.
        if (marketEvent == null)
        {
            throw new ArgumentNullException(nameof(marketEvent));
        }

        // Update the latest market data for the asset.
        var marketData = new MarketData(
            marketEvent.Timestamp,
            marketEvent.Asset,
            marketEvent.Open,
            marketEvent.High,
            marketEvent.Low,
            marketEvent.Close,
            marketEvent.Volume
        );

        _latestMarketData[marketEvent.Asset] = marketData;
    }

    /// <summary>
    /// Handles the SignalEvent by creating an OrderEvent based on the signal type.
    /// </summary>
    /// <param name="signalEvent">The SignalEvent to be processed.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="signalEvent"/> is null.</exception>
    private void HandleSignalEvent(SignalEvent signalEvent)
    {
        // Ensure that the signal event is not null.
        if (signalEvent == null) throw new ArgumentNullException(nameof(signalEvent));

        // Get the position for the current strategy and asset
        var currentPosition = _positions[signalEvent.Asset][_currentExecutingStrategy.Name];

        // Determine the order type and quantity based on the signal type
        OrderType orderType;
        int quantity;

        // Calculate the total value of the portfolio
        var portfolioTotalValue = EquityCurve[signalEvent.Timestamp];

        switch (signalEvent.SignalType)
        {
            case SignalType.Long:
                orderType = OrderType.Buy;
                quantity = _currentExecutingStrategy.PositionSizer.GetPositionSize(signalEvent.Asset, portfolioTotalValue);
                break;
            case SignalType.Short:
                orderType = OrderType.Sell;
                quantity = _currentExecutingStrategy.PositionSizer.GetPositionSize(signalEvent.Asset, portfolioTotalValue);
                break;
            case SignalType.Exit:
                orderType = currentPosition > 0 ? OrderType.Sell : OrderType.Buy;
                quantity = Math.Abs(currentPosition);
                break;
            default:
                throw new InvalidOperationException("Unknown signal type.");
        }

        // Create an OrderEvent
        var orderEvent = new OrderEvent(
            signalEvent.Timestamp,
            signalEvent.Asset,
            orderType,
            quantity,
            _currentExecutingStrategy.Slippage,
            _currentExecutingStrategy.Commission
        );

        // Handle the OrderEvent
        HandleOrderEvent(orderEvent);
    }

    /// <summary>
    /// Handles an order event by processing the order, calculating the fill price, and creating a fill event.
    /// </summary>
    /// <param name="orderEvent">The order event to be processed.</param>
    /// <remarks>
    /// This method retrieves the latest market data for the asset associated with the order event,
    /// calculates the fill price based on the slippage, creates a fill event, and then handles the fill event.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="orderEvent"/> is null.</exception>
    private void HandleOrderEvent(OrderEvent orderEvent)
    {
        // Ensure that the OrderEvent is not null.
        Guard.AgainstNull(() => orderEvent);

        // Get the latest market data for the asset
        var marketData = _latestMarketData[orderEvent.Asset];

        // Calculate the fill price based on the slippage
        var fillPrice = (orderEvent.OrderType == OrderType.Buy) ?
            marketData.Close * (1 + orderEvent.Slippage) :
            marketData.Close * (1 - orderEvent.Slippage);

        // Create a fill event
        var fillEvent = new FillEvent(
            orderEvent.Timestamp,
            orderEvent.Asset,
            orderEvent.Quantity,
            fillPrice,
            orderEvent.Commission,
            _currentExecutingStrategy.Name
        );

        // Handle the fill event
        HandleFillEvent(fillEvent);
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
    private void HandleFillEvent(FillEvent fillEvent)
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
    /// Updates the latest market data for a given asset in the portfolio.
    /// </summary>
    /// <param name="asset">The asset for which the market data should be updated.</param>
    /// <param name="marketData">The new market data for the asset.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="asset"/> or <paramref name="marketData"/> is null.</exception>
    private void UpdateMarketData(string asset, MarketData marketData)
    {
        // Ensure that the asset and marketData are not null.
        Guard.AgainstNull(() => asset);
        Guard.AgainstNull(() => marketData);

        _latestMarketData[asset] = marketData;
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
    private void HandleDividendEvent(DividendEvent dividendEvent)
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
    private void HandleRebalancingEvent(RebalancingEvent rebalancingEvent)
    {
        // Ensure that the rebalancingEvent is not null.
        Guard.AgainstNull(() => rebalancingEvent);

        // Calculate the total value of the portfolio
        var portfolioTotalValue = EquityCurve[rebalancingEvent.Timestamp];

        // Calculate the equity for the strategy
        var equity = _currentExecutingStrategy.CalculateEquity();

        // Rebalance the portfolio for each asset in the strategy
        foreach (var asset in rebalancingEvent.Assets)
        {
            decimal targetWeight = _currentExecutingStrategy.PositionSizer.GetPositionSize(asset, portfolioTotalValue);
            var targetValue = equity * targetWeight;
            var currentPositionValue = _positions[_currentExecutingStrategy.Name][asset] * _latestMarketData[asset].Close;

            // Calculate the required quantity adjustment
            var quantityAdjustment = (int)((targetValue - currentPositionValue) / _latestMarketData[asset].Close);

            // Create and execute an order event for the quantity adjustment
            if (quantityAdjustment == 0)
            {
                continue;
            }

            var orderEvent = new OrderEvent(
                rebalancingEvent.Timestamp, 
                asset,
                quantityAdjustment > 0 ? OrderType.Buy : OrderType.Sell,
                quantityAdjustment,
                _currentExecutingStrategy.Slippage,
                _currentExecutingStrategy.Commission
            );
            HandleOrderEvent(orderEvent);
        }
    }
}
