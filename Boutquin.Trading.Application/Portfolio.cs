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

using Boutquin.Domain.Exceptions;

namespace Boutquin.Trading.Application;

using Boutquin.Domain.Helpers;
using Domain.Data;
using Domain.Enums;
using Domain.Events;
using Boutquin.Trading.Domain.Interfaces;
using System.Collections.Immutable;
using Boutquin.Trading.Domain.Helpers;

public sealed class Portfolio
{
    private readonly SortedDictionary<string, IStrategy> _strategies; // StrategyName-> Strategy
    private readonly ICapitalAllocationStrategy _capitalAllocationStrategy;
    private readonly IBrokerage _broker;
    private readonly CurrencyCode _baseCurrency;
    private readonly SortedDictionary<string, CurrencyCode> _assetCurrencies;
    private readonly SortedDictionary<DateOnly, SortedDictionary<string, MarketData>> _historicalMarketData;
    private readonly SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> _historicalFxConversionRates;
    private string _currentExecutingStrategyName;

    /// <summary>
    /// Initializes a new instance of the Portfolio class.
    /// </summary>
    /// <param name="strategies">A sorted dictionary of strategies keyed by their names.</param>
    /// <param name="capitalAllocationStrategy">The capital allocation strategy to be used by the portfolio.</param>
    /// <param name="broker">The brokerage to be used for executing orders and fetching market data.</param>
    /// <param name="baseCurrency">The base currency used for calculations and reporting within the portfolio.</param>
    /// <param name="assetCurrencies">A sorted dictionary of asset symbols and their associated currency codes.</param>
    /// <param name="historicalMarketData">A sorted dictionary containing historical market data for each asset.</param>
    /// <param name="historicalFxConversionRates">A sorted dictionary containing historical foreign exchange conversion rates for the required currencies.</param>
    /// <exception cref="EmptyOrNullDictionaryException">Thrown when any of the sorted dictionary parameters are empty or null.</exception>
    /// <exception cref="ArgumentNullException">Thrown when capitalAllocationStrategy or broker parameter is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when baseCurrency parameter has an undefined value.</exception>
    /// <remarks>
    /// The Portfolio constructor initializes a new portfolio object with the specified strategies, capital allocation strategy, brokerage,
    /// base currency, asset currencies, historical market data, and historical foreign exchange conversion rates.
    /// </remarks>
    public Portfolio(
        SortedDictionary<string, IStrategy> strategies,
        ICapitalAllocationStrategy capitalAllocationStrategy,
        IBrokerage broker,
        CurrencyCode baseCurrency,
        SortedDictionary<string, CurrencyCode> assetCurrencies,
        SortedDictionary<DateOnly, SortedDictionary<string, MarketData>> historicalMarketData,
        SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        // Validate parameters
        Guard.AgainstEmptyOrNullDictionary(() => strategies); // Throws EmptyOrNullDictionaryException
        Guard.AgainstNull(() => capitalAllocationStrategy); // Throws ArgumentNullException
        Guard.AgainstNull(() => broker); // Throws ArgumentNullException
        Guard.AgainstUndefinedEnumValue(() => baseCurrency); // Throws ArgumentOutOfRangeException
        Guard.AgainstEmptyOrNullDictionary(() => assetCurrencies); // Throws EmptyOrNullDictionaryException
        Guard.AgainstEmptyOrNullDictionary(() => historicalMarketData); // Throws EmptyOrNullDictionaryException
        Guard.AgainstEmptyOrNullDictionary(() => historicalFxConversionRates); // Throws EmptyOrNullDictionaryException

        _strategies = strategies;
        _capitalAllocationStrategy = capitalAllocationStrategy;        
        _broker = broker;
        _baseCurrency = baseCurrency;
        _assetCurrencies = assetCurrencies;
        _historicalMarketData = historicalMarketData;
        _historicalFxConversionRates = historicalFxConversionRates;
    }

    public SortedDictionary<DateOnly, decimal> EquityCurve { get; } = new();

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
    /// Handles the MarketEvent by updating the historical market data and generating signals for each strategy.
    /// </summary>
    /// <param name="marketEvent">The MarketEvent to be processed.</param>
    /// <returns>A Task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the marketEvent parameter is null.</exception>
    /// <remarks>
    /// The HandleMarketEventAsync method updates the historical market data with the new market data from the MarketEvent.
    /// It then iterates through each strategy and generates signals based on the updated market data.
    /// </remarks>
    private async Task HandleMarketEventAsync(MarketEvent marketEvent)
    {
        // Ensure that the marketEvent is not null.
        Guard.AgainstNull(() => marketEvent); // Throws ArgumentNullException when the marketEvent parameter is null

        // Update historical market data with new data from the MarketEvent
        _historicalMarketData[marketEvent.Timestamp] = marketEvent.HistoricalMarketData;
        _historicalFxConversionRates[marketEvent.Timestamp] = marketEvent.HistoricalFxConversionRates;

        // Detect and handle dividend events
        foreach (var assetMarketData in marketEvent.HistoricalMarketData)
        {
            var asset = assetMarketData.Key;
            var marketData = assetMarketData.Value;
            if (marketData.DividendPerShare > 0)
            {
                var dividendEvent = new DividendEvent(marketEvent.Timestamp, asset, marketData.DividendPerShare);
                await HandleDividendEventAsync(dividendEvent);
            }
        }

        // Detect and handle split events
        foreach (var assetMarketData in marketEvent.HistoricalMarketData)
        {
            var asset = assetMarketData.Key;
            var marketData = assetMarketData.Value;
            if (marketData.SplitCoefficient != 1)
            {
                var splitEvent = new SplitEvent(marketEvent.Timestamp, asset, marketData.SplitCoefficient);
                await HandleSplitEventAsync(splitEvent);
            }
        }

        // Allocate capital to strategies
        var targetCapital = _capitalAllocationStrategy.AllocateCapital(
            _strategies.Values.ToImmutableList(),
            _historicalMarketData,
            _historicalFxConversionRates);

        // Iterate through each strategy and generate signals based on the updated market data
        foreach (var strategyPair in _strategies)
        {
            _currentExecutingStrategyName = strategyPair.Key;
            var strategy = strategyPair.Value;

            // Update parameters for the position sizer with the current strategy
            strategy.PositionSizer.UpdateParameters(strategy);

            // Store the target capital for the current strategy
            strategy.TargetCapital = targetCapital[_currentExecutingStrategyName];

            // Generate signals for the current strategy
            var signals = strategy.GenerateSignals(
                strategy.TargetCapital,
                _historicalMarketData,
                _historicalFxConversionRates);

            // Process each signal
            foreach (var signal in signals)
            {
                await HandleSignalEventAsync(signal);
            }
        }

        // Reset the current executing strategy name to null
        _currentExecutingStrategyName = null;
    }

    /// <summary>
    /// Handles the SignalEvent by generating an OrderEvent based on the signal type, position size, and order prices.
    /// </summary>
    /// <param name="signalEvent">The SignalEvent to handle.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method uses the IPositionSizer and IOrderPriceCalculationStrategy instances associated with the strategy
    /// to determine the position size and order prices for the given signal. It then generates an OrderEvent and
    /// passes it to the HandleEventAsync method for further processing.
    /// </remarks>
    private async Task HandleSignalEventAsync(SignalEvent signalEvent)
    {
        // Ensure that the signalEvent is not null.
        Guard.AgainstNull(() => signalEvent); // Throws ArgumentNullException when the signalEvent parameter is null

        // Get the corresponding strategy.
        var strategy = _strategies[signalEvent.StrategyName];

        // Calculate the position size based on the signal type.
        var positionSize = strategy.PositionSizer.GetPositionSize(
            signalEvent.Timestamp, 
            signalEvent.Asset, 
            signalEvent.SignalType,
            strategy.TargetCapital[_assetCurrencies[signalEvent.Asset]]);

        // Calculate order prices using the strategy's order price calculation strategy.
        var (orderType, primaryPrice, secondaryPrice) = strategy.OrderPriceCalculationStrategy.CalculateOrderPrices(
            signalEvent.Asset, 
            signalEvent.Timestamp, 
            _historicalMarketData);

        // Determine the trade action based on the signal type.
        var tradeAction = signalEvent.SignalType switch
        {
            SignalType.Long => TradeAction.Buy,
            SignalType.Short => TradeAction.Sell,
            SignalType.Exit => TradeAction.Sell,
            _ => throw new NotSupportedException($"Unsupported signal type: {signalEvent.SignalType}")
        };

        // Create an OrderEvent based on the SignalEvent.
        var orderEvent = new OrderEvent(
            signalEvent.Timestamp,
            signalEvent.StrategyName,
            signalEvent.Asset,
            tradeAction,
            orderType,
            positionSize,
            primaryPrice,
            secondaryPrice
        );

        // Pass the OrderEvent to the HandleEventAsync method for further processing.
        await HandleEventAsync(orderEvent);
    }

    /// <summary>
    /// Handles an OrderEvent by submitting the order to the brokerage.
    /// </summary>
    /// <param name="orderEvent">The OrderEvent to handle.</param>
    /// <returns>A Task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the orderEvent parameter is null.</exception>
    /// <remarks>
    /// The method ensures that the orderEvent is not null before proceeding. It then submits
    /// the order to the brokerage using the SubmitOrderAsync method. If the submission is
    /// successful, the method returns a completed task; otherwise, it handles the failure
    /// as appropriate, such as logging the failure or raising an event.
    /// </remarks>
    private async Task HandleOrderEventAsync(OrderEvent orderEvent)
    {
        // Ensure that the orderEvent is not null.
        Guard.AgainstNull(() => orderEvent); // Throws ArgumentNullException when the orderEvent parameter is null

        // Create an Order object from the OrderEvent data.
        var order = new Order(
            orderEvent.Timestamp,
            orderEvent.StrategyName,
            orderEvent.Asset,
            orderEvent.TradeAction,
            orderEvent.OrderType,
            orderEvent.Quantity,
            orderEvent.PrimaryPrice,
            orderEvent.SecondaryPrice);

        // Submit the order to the brokerage.
        var isOrderSubmitted = await _broker.SubmitOrderAsync(order);

        // Handle the result of the order submission.
        if (isOrderSubmitted)
        {
            // The order was successfully submitted.
            // Additional actions can be performed here, such as logging the order submission.
        }
        else
        {
            // The order submission failed.
            // Handle the failure as appropriate, such as logging the failure or raising an event.
        }

        return;
    }

    /// <summary>
    /// Handles a FillEvent by updating the strategy's position, cash, and daily native returns.
    /// </summary>
    /// <param name="fillEvent">The FillEvent to handle.</param>
    /// <returns>A Task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the fillEvent parameter is null.</exception>
    /// <remarks>
    /// The method ensures that the fillEvent is not null before proceeding. It then retrieves
    /// the strategy associated with the FillEvent and updates the strategy's position, cash,
    /// and daily native returns based on the fill information.
    /// </remarks>
    private Task HandleFillEventAsync(FillEvent fillEvent)
    {
        // Ensure that the fillEvent is not null.
        Guard.AgainstNull(() => fillEvent); // Throws ArgumentNullException when the fillEvent parameter is null

        // Retrieve the strategy associated with the FillEvent.
        if (!_strategies.TryGetValue(fillEvent.StrategyName, out var strategy))
        {
            throw new InvalidOperationException($"Strategy not found: {fillEvent.StrategyName}");
        }

        // Update the strategy's position.
        if (strategy.Positions.ContainsKey(fillEvent.Asset))
        {
            strategy.Positions[fillEvent.Asset] += fillEvent.Quantity;
        }
        else
        {
            strategy.Positions[fillEvent.Asset] = fillEvent.Quantity;
        }

        // Update the strategy's cash and daily native returns.
        var tradeValue = fillEvent.FillPrice * fillEvent.Quantity;
        var tradeCost = tradeValue + fillEvent.Commission;
        strategy.Cash[_assetCurrencies[fillEvent.Asset]] -= tradeCost;
        strategy.DailyNativeReturns[fillEvent.Asset][fillEvent.Timestamp] = tradeValue;

        // Additional actions can be performed here, such as logging the fill or updating the portfolio's metrics.

        return Task.CompletedTask;
    }


    private Task HandleRebalancingEventAsync(RebalancingEvent rebalancingEvent)
    {
        // Ensure that the rebalancingEvent is not null.
        Guard.AgainstNull(() => rebalancingEvent); // Throws ArgumentNullException when the rebalancingEvent parameter is null

        //...
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a SplitEvent by updating the positions and historical market data.
    /// </summary>
    /// <param name="splitEvent">The SplitEvent to handle.</param>
    /// <returns>A Task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the splitEvent parameter is null.</exception>
    /// <remarks>
    /// The method ensures that the splitEvent is not null before proceeding. It then adjusts
    /// the positions and historical market data of the affected asset for all strategies in the
    /// portfolio, taking the split ratio into account.
    /// </remarks>
    private Task HandleSplitEventAsync(SplitEvent splitEvent)
    {
        // Ensure that the splitEvent is not null.
        Guard.AgainstNull(() => splitEvent); // Throws ArgumentNullException when the splitEvent parameter is null

        // Adjust the positions for all strategies in the portfolio.
        foreach (IStrategy strategy in _strategies.Values)
        {
            if (strategy.Positions.TryGetValue(splitEvent.Asset, out int position))
            {
                strategy.Positions[splitEvent.Asset] = (int)(position * splitEvent.SplitRatio);
            }
        }

        // Adjust the historical market data for the affected asset.
        foreach (var historicalData in _historicalMarketData.Values)
        {
            if (historicalData.TryGetValue(splitEvent.Asset, out MarketData marketData))
            {
                marketData.AdjustForSplit(splitEvent.SplitRatio);
            }
        }

        // Additional actions can be performed here, such as logging the split event or updating the portfolio's metrics.

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a dividend event by updating the strategy's cash and asset positions.
    /// </summary>
    /// <param name="dividendEvent">The dividend event to handle.</param>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// This method updates the strategy's cash and asset positions based on the dividend event. It ensures that
    /// the dividendEvent is not null and throws an ArgumentNullException if it is.
    /// </remarks>
    private async Task HandleDividendEventAsync(DividendEvent dividendEvent)
    {
        // Ensure that the dividendEvent is not null.
        Guard.AgainstNull(() => dividendEvent); // Throws ArgumentNullException when the dividendEvent parameter is null

        // Determine the currency of the asset
        if (!_assetCurrencies.TryGetValue(dividendEvent.Asset, out var assetCurrency))
        {
            throw new InvalidOperationException($"No currency found for asset '{dividendEvent.Asset}'");
        }

        // Iterate through all the strategies
        foreach (var strategyEntry in _strategies)
        {
            var strategy = strategyEntry.Value;

            // Check if the strategy holds the asset in the dividend event
            if (strategy.Positions.TryGetValue(dividendEvent.Asset, out var positionQuantity))
            {
                // Calculate the dividend amount by multiplying the dividend per share with the position quantity
                var dividendAmount = dividendEvent.DividendPerShare * positionQuantity;

                // Update the strategy's cash balance by adding the dividend amount in the native currency
                if (strategy.Cash.TryGetValue(_baseCurrency, out var currentCashBalance))
                {
                    strategy.Cash[assetCurrency] = currentCashBalance + dividendAmount;
                }
                else
                {
                    strategy.Cash[assetCurrency] = dividendAmount;
                }
            }
        }
    }
}
