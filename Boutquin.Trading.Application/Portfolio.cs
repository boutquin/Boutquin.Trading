// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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

using Domain.Data;

public class Portfolio : IPortfolio
{
    /// <summary>
    /// Indicates whether the portfolio is being run in live mode.
    /// </summary>
    /// <value>True if the portfolio is being run in live mode, false otherwise.</value>
    /// <remarks>
    /// This property is used to differentiate between a live trading environment and a backtest or simulation environment. 
    /// In live mode, the portfolio operates in real-time and is subject to live market data. In a backtest or simulation mode, the portfolio operates on historical data.
    /// </remarks>
    public bool IsLive { get; }

    /// <summary>
    /// The EventProcessor property represents a system to process events for the portfolio.
    /// </summary>
    public IEventProcessor EventProcessor { get; }

    /// <summary>
    /// The Broker property represents the brokerage that executes trades for the portfolio.
    /// </summary>
    private readonly IBrokerage _broker;

    /// <summary>
    /// Initializes a new instance of the <see cref="Portfolio"/> class.
    /// </summary>
    /// <param name="strategies">
    /// A dictionary containing the set of strategies to be employed by the portfolio.
    /// Each entry is a key-value pair where the key is a unique identifier for a strategy and the value is the strategy instance.
    /// </param>
    /// <param name="assetCurrencies">
    /// A dictionary containing the asset-currency associations.
    /// Each entry is a key-value pair where the key is a unique identifier for an asset and the value is the currency of the asset.
    /// </param>
    /// <param name="eventProcessor">
    /// An instance of the <see cref="IEventProcessor"/> interface to handle the processing of events.
    /// </param>
    /// <param name="broker">
    /// An instance of the <see cref="IBrokerage"/> interface to handle the execution of orders.
    /// </param>
    /// <param name="isLive">
    /// A Boolean flag indicating whether the portfolio is live. The default value is false.
    /// </param>
    /// <exception cref="EmptyOrNullDictionaryException">
    /// Throws this exception if either the 'strategies' or 'assetCurrencies' dictionary is null or empty.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Throws this exception if either the 'eventProcessor' or 'broker' argument is null.
    /// </exception>
    /// <remarks>
    /// When an instance of this class is created, it subscribes to the FillOccurred event of the provided brokerage. 
    /// This event is triggered when an order is filled by the brokerage.
    /// </remarks>
    public Portfolio(
        IReadOnlyDictionary<string, IStrategy> strategies,
        IReadOnlyDictionary<string, CurrencyCode> assetCurrencies,
        IEventProcessor eventProcessor,
        IBrokerage broker,
        bool isLive = false)
    {
        // Validate parameters
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => strategies); // Throws EmptyOrNullDictionaryException
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => assetCurrencies); // Throws EmptyOrNullDictionaryException
        Guard.AgainstNull(() => eventProcessor);
        Guard.AgainstNull(() => broker);

        EventProcessor = eventProcessor;
        Strategies = strategies;
        AssetCurrencies = assetCurrencies;
        IsLive = isLive;

        _broker = broker;
        _broker.FillOccurred += HandleFillEvent;
    }

    /// <summary>
    /// The Strategies property represents a read-only dictionary of strategies used in the portfolio.
    /// </summary>
    public IReadOnlyDictionary<string, IStrategy> Strategies { get; }

    /// <summary>
    /// The AssetCurrencies property represents a read-only dictionary of assets and their respective currencies used in the portfolio.
    /// </summary>
    public IReadOnlyDictionary<string, CurrencyCode> AssetCurrencies { get; }

    /// <summary>
    /// The HistoricalMarketData property represents a sorted dictionary of historical market data used by the portfolio.
    /// </summary>
    public SortedDictionary<DateOnly, SortedDictionary<string, MarketData>?> HistoricalMarketData { get; } 
        = [];

    /// <summary>
    /// The HistoricalFxConversionRates property represents a sorted dictionary of historical foreign exchange conversion rates used by the portfolio.
    /// </summary>
    public SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> HistoricalFxConversionRates { get; } =
        [];

    /// <summary>
    /// The EquityCurve property represents a sorted dictionary of equity values over time.
    /// </summary>
    public SortedDictionary<DateOnly, decimal> EquityCurve { get; } 
        = [];

    /// <summary>
    /// Asynchronously handles the specified event, processing it using the portfolio's event processor.
    /// </summary>
    /// <param name="event">The event to handle. This represents an occurrence in the system that may affect the state of the portfolio.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task result contains no value.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the provided event is null.</exception>
    /// <exception cref="NotSupportedException">Thrown if an error occurs while processing the event.</exception>
    /// <remarks>
    /// This method processes the given event using the portfolio's event processor. The event represents something that has happened in the system,
    /// such as a change in the market, a change in the portfolio's assets, or a change in the portfolio's strategy.
    /// The portfolio's event processor is responsible for updating the state of the portfolio based on the event.
    /// Note that the event is processed asynchronously, so the method may return before the event processing has completed.
    /// Any errors that occur during the event processing are thrown as exceptions.
    /// </remarks>
    public async Task HandleEventAsync(IFinancialEvent @event)
    {
        // Ensure that the @event is not null.
        Guard.AgainstNull(() => @event); // Throws ArgumentNullException when the @event parameter is null

        await EventProcessor.ProcessEventAsync(@event);
    }

    /// <summary>
    /// Updates historical data of the portfolio.
    /// </summary>
    /// <param name="marketEvent">Market event containing the updated historical data.</param>
    /// <exception cref="ArgumentNullException">Thrown when the marketEvent parameter is null.</exception>
    public void UpdateHistoricalData(MarketEvent marketEvent)
    {
        // Ensure that the marketEvent is not null.
        Guard.AgainstNull(() => marketEvent); // Throws ArgumentNullException when the marketEvent parameter is null

        // Update historical market data with new data from the MarketEvent
        HistoricalMarketData[marketEvent.Timestamp] = marketEvent.HistoricalMarketData;
        HistoricalFxConversionRates[marketEvent.Timestamp] = marketEvent.HistoricalFxConversionRates;
    }

    /// <summary>
    /// Updates the cash balance for each strategy that holds a given asset in response to a dividend event.
    /// </summary>
    /// <param name="asset">The asset symbol for which the dividend event occurred.</param>
    /// <param name="dividendPerShare">The amount of dividend received per share.</param>
    /// <exception cref="ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a dividend event occurs for an asset held by one or more strategies in the portfolio.
    /// The method implementation should ensure that the cash balance for each strategy that holds the asset is updated 
    /// by adding the total dividend amount (dividend per share * position quantity) to the current cash balance.
    /// </remarks>
    public void UpdateCashForDividend(string asset, decimal dividendPerShare)
    {
        Guard.AgainstNullOrWhiteSpace(() => asset); // Throws ArgumentException

        // Determine the currency of the asset
        var assetCurrency = GetAssetCurrency(asset);

        // Iterate through all the strategies
        foreach (var strategyEntry in Strategies)
        {
            var strategy = strategyEntry.Value;

            // Check if the strategy holds the asset in the dividend event
            var positionQuantity = strategy.GetPositionQuantity(asset);
            if (positionQuantity == 0)
            {
                continue;
            }

            // Calculate the dividend amount by multiplying the dividend per share with the position quantity
            var dividendAmount = dividendPerShare * positionQuantity;

            // Update the strategy's cash balance by adding the dividend amount in the native currency
            strategy.UpdateCash(assetCurrency, dividendAmount);
        }
    }

    /// <summary>
    /// Submits an order based on an OrderEvent.
    /// </summary>
    /// <param name="orderEvent">The OrderEvent containing the order information.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the order submission was successful.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the orderEvent parameter is null.</exception>
    /// <remarks>
    /// This method is called when an order needs to be submitted to the brokerage.
    /// The method implementation should ensure that the necessary checks and validations are performed on the order event data 
    /// before creating and submitting the order to the brokerage.
    /// </remarks>
    public async Task<bool> SubmitOrderAsync(OrderEvent orderEvent)
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
        return await _broker.SubmitOrderAsync(order);
    }

    /// <summary>
    /// Generates trading signals for each strategy in the portfolio based on updated market data.
    /// </summary>
    /// <param name="marketEvent">The MarketEvent containing the updated market data.</param>
    /// <param name="baseCurrency">The base currency used for calculations.</param>
    /// <returns>An enumerable of SignalEvent containing the generated signals for each strategy.</returns>
    /// <remarks>
    /// This method is called when new market data is available and trading signals need to be generated for the portfolio's strategies.
    /// The method implementation should ensure that the signal generation process for each strategy is performed 
    /// according to the strategy's signal generation rules and that the generated signals are correctly formatted and returned.
    /// </remarks>
    public IEnumerable<SignalEvent> GenerateSignals(
        MarketEvent marketEvent,
        CurrencyCode baseCurrency)
    {
        // Iterate through each strategy and generate signals based on the updated market data
        return Strategies
            .Select(strategyPair => strategyPair.Value)
            .Select(strategy => strategy.GenerateSignals(
                                            marketEvent.Timestamp,
                                            baseCurrency,
                                            HistoricalMarketData,
                                            HistoricalFxConversionRates));
    }

    /// <summary>
    /// Updates the position for a given asset in a specific strategy.
    /// </summary>
    /// <param name="strategyName">The name of the strategy.</param>
    /// <param name="asset">The asset symbol.</param>
    /// <param name="quantity">The quantity of the asset to be updated.</param>
    /// <exception cref="ArgumentException">Thrown when the strategyName or asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a position for a given asset in a specific strategy needs to be updated.
    /// The method implementation should ensure that the position is updated correctly 
    /// and that the new position does not lead to an inconsistent portfolio state.
    /// </remarks>
    public void UpdatePosition(string strategyName, string asset, int quantity)
    {
        Guard.AgainstNullOrWhiteSpace(() => strategyName); // Throws ArgumentException
        Guard.AgainstNullOrWhiteSpace(() => asset); // Throws ArgumentException

        var strategy = GetStrategy(strategyName);
        strategy.UpdatePositions(asset, quantity);
    }

    /// <summary>
    /// Updates the cash balance for a specific strategy and currency.
    /// </summary>
    /// <param name="strategyName">The name of the strategy.</param>
    /// <param name="currency">The currency of the cash balance to be updated.</param>
    /// <param name="amount">The amount to be updated.</param>
    /// <exception cref="ArgumentException">Thrown when the strategyName parameter is null, empty, or consists only of white-space characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the currency parameter is not a defined value of the CurrencyCode enumeration.</exception>
    /// <remarks>
    /// This method is called when the cash balance for a specific strategy and currency needs to be updated.
    /// The method implementation should ensure that the cash balance is updated correctly and that the new cash balance does not lead to an inconsistent portfolio state.
    /// </remarks>
    public void UpdateCash(string strategyName, CurrencyCode currency, decimal amount)
    {
        Guard.AgainstNullOrWhiteSpace(() => strategyName); // Throws ArgumentException
        Guard.AgainstUndefinedEnumValue(() => currency); // Throws ArgumentOutOfRangeException

        var strategy = GetStrategy(strategyName);
        strategy.UpdateCash(currency, amount);
    }

    /// <summary>
    /// Updates the equity curve for the portfolio.
    /// </summary>
    /// <param name="timestamp">The timestamp for the equity curve update.</param>
    /// <param name="baseCurrency">The base currency used for calculations.</param>
    /// <remarks>
    /// This method is called when the equity curve for the portfolio needs to be updated.
    /// The method implementation should ensure that the equity curve is updated correctly and that the updated equity curve accurately reflects the current state of the portfolio.
    /// </remarks>
    public void UpdateEquityCurve(
        DateOnly timestamp,
        CurrencyCode baseCurrency)
    {
        EquityCurve[timestamp] = CalculateTotalPortfolioValue(timestamp, baseCurrency);
    }

    /// <summary>
    /// Adjusts positions for a specific asset due to a stock split.
    /// </summary>
    /// <param name="asset">The asset that has been split.</param>
    /// <param name="splitRatio">The ratio of the split.</param>
    /// <exception cref="ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a stock split has occurred, and the portfolio's positions need to be adjusted.
    /// The method implementation should ensure that the positions are adjusted correctly and that the adjusted positions do not lead to an inconsistent portfolio state.
    /// </remarks>
    public void AdjustPositionForSplit(
        string asset,
        decimal splitRatio)
    {
        Guard.AgainstNullOrWhiteSpace(() => asset); // Throws ArgumentException

        // Adjust the positions for all strategies in the portfolio.
        foreach (var strategy in Strategies.Values)
        {
            if (strategy.Positions.TryGetValue(asset, out var position))
            {
                strategy.Positions[asset] = (int)(position * splitRatio);
            }
        }
    }

    /// <summary>
    /// Adjusts historical data for a specific asset due to a stock split.
    /// </summary>
    /// <param name="asset">The asset that has been split.</param>
    /// <param name="splitRatio">The ratio of the split.</param>
    /// <exception cref="ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a stock split has occurred, and the portfolio's historical data needs to be adjusted.
    /// The method implementation should ensure that the historical data is adjusted correctly.
    /// </remarks>
    public void AdjustHistoricalDataForSplit(
        string asset,
        decimal splitRatio)
    {
        Guard.AgainstNullOrWhiteSpace(() => asset); // Throws ArgumentException

        // Adjust the historical market data for the affected asset.
        foreach (var historicalData in HistoricalMarketData.Values)
        {
            if (historicalData.TryGetValue(asset, out var marketData))
            {
                historicalData[asset] = marketData.AdjustForSplit(splitRatio);
            }
        }
    }

    /// <summary>
    /// Retrieves a strategy based on its name.
    /// </summary>
    /// <param name="strategyName">The name of the strategy to retrieve.</param>
    /// <returns>The strategy associated with the provided name.</returns>
    /// <exception cref="ArgumentException">Thrown when the strategyName parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a strategy needs to be retrieved based on its name.
    /// The method implementation should ensure that the correct strategy is retrieved, or an appropriate error is thrown if the strategy cannot be found.
    /// </remarks>
    public IStrategy GetStrategy(string strategyName)
    {
        Guard.AgainstNullOrWhiteSpace(() => strategyName); // Throws ArgumentException

        if (!Strategies.TryGetValue(strategyName, out var strategy))
        {
            throw new ArgumentException($"Strategy '{strategyName}' not found in the portfolio.");
        }

        return strategy;
    }

    /// <summary>
    /// Retrieves the currency of a specific asset.
    /// </summary>
    /// <param name="asset">The asset for which the currency is to be retrieved.</param>
    /// <returns>The currency associated with the provided asset.</returns>
    /// <exception cref="ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when the currency of a specific asset needs to be retrieved.
    /// The method implementation should ensure that the correct currency is returned, or an appropriate error is thrown if the currency cannot be found.
    /// </remarks>
    public CurrencyCode GetAssetCurrency(string asset)
    {
        Guard.AgainstNullOrWhiteSpace(() => asset); // Throws ArgumentException

        if (!AssetCurrencies.TryGetValue(asset, out var currency))
        {
            throw new ArgumentException($"Asset '{asset}' not found in the portfolio.");
        }

        return currency;
    }

    /// <summary>
    /// Calculates the total value of the portfolio.
    /// </summary>
    /// <param name="timestamp">The timestamp for which the total value is calculated.</param>
    /// <param name="baseCurrency">The base currency used for the calculation.</param>
    /// <returns>The total value of the portfolio.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the baseCurrency parameter is not a defined value of the CurrencyCode enumeration.</exception>
    /// <remarks>
    /// This method is called when the total value of the portfolio needs to be calculated.
    /// The method implementation should ensure that the calculation is accurate and that it correctly reflects the current state of the portfolio.
    /// </remarks>
    public decimal CalculateTotalPortfolioValue(
        DateOnly timestamp,
        CurrencyCode baseCurrency)
    {
        Guard.AgainstUndefinedEnumValue(() => baseCurrency); // Throws ArgumentOutOfRangeException

        return Strategies
            .Select(strategyPair => strategyPair.Value)
            .Select(strategy => strategy.ComputeTotalValue(timestamp, baseCurrency, HistoricalMarketData, HistoricalFxConversionRates))
            .Sum();
    }

    private async void HandleFillEvent(object sender, FillEvent fillEvent)
    {
        // Ensure that the @event is not null.
        Guard.AgainstNull(() => fillEvent); // Throws ArgumentNullException when the fillEvent parameter is null

        await EventProcessor.ProcessEventAsync(fillEvent);
    }
}
