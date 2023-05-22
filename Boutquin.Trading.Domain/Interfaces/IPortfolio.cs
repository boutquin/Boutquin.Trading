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
namespace Boutquin.Trading.Domain.Interfaces;

using System.Collections.Generic;

using Data;
using Enums;
using Events;

/// <summary>
/// The IPortfolio interface defines the structure and behavior of a portfolio in a trading system.
/// It provides methods and properties to manage the portfolio's strategies, assets, historical data, 
/// and to perform various portfolio management operations such as allocating capital, 
/// updating cash balances, submitting orders, generating trading signals, updating positions, 
/// updating historical data, updating the equity curve, and adjusting data and positions for stock splits.
/// It includes properties for an event processor, a brokerage, strategies, asset currencies, 
/// historical market data, historical foreign exchange conversion rates, and the equity curve.
/// </summary>
/// <remarks>
/// Implementations of this interface should ensure that the integrity and consistency of the portfolio state 
/// is maintained throughout all operations. They should also ensure that the necessary checks and validations 
/// are in place to prevent the execution of invalid operations that could lead to an inconsistent portfolio state.
/// </remarks>
public interface IPortfolio
{
    /// <summary>
    /// Indicates whether the portfolio is being run in live mode.
    /// </summary>
    /// <value>True if the portfolio is being run in live mode, false otherwise.</value>
    /// <remarks>
    /// This property is used to differentiate between a live trading environment and a backtest or simulation environment. 
    /// In live mode, the portfolio operates in real-time and is subject to live market data. In a backtest or simulation mode, the portfolio operates on historical data.
    /// </remarks>
    bool IsLive { get; }

    /// <summary>
    /// The EventProcessor property represents a system to process events for the portfolio.
    /// </summary>
    EventProcessor EventProcessor { get; }

    /// <summary>
    /// The Broker property represents the brokerage that executes trades for the portfolio.
    /// </summary>
    IBrokerage Broker { get; }

    /// <summary>
    /// The Strategies property represents a read-only dictionary of strategies used in the portfolio.
    /// </summary>
    IReadOnlyDictionary<string, IStrategy> Strategies { get; }

    /// <summary>
    /// The AssetCurrencies property represents a read-only dictionary of assets and their respective currencies used in the portfolio.
    /// </summary>
    IReadOnlyDictionary<string, CurrencyCode> AssetCurrencies { get; }

    /// <summary>
    /// The HistoricalMarketData property represents a sorted dictionary of historical market data used by the portfolio.
    /// </summary>
    SortedDictionary<DateOnly, SortedDictionary<string, MarketData>?> HistoricalMarketData { get; }

    /// <summary>
    /// The HistoricalFxConversionRates property represents a sorted dictionary of historical foreign exchange conversion rates used by the portfolio.
    /// </summary>
    SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> HistoricalFxConversionRates { get; }

    /// <summary>
    /// The EquityCurve property represents a sorted dictionary of equity values over time.
    /// </summary>
    SortedDictionary<DateOnly, decimal> EquityCurve { get; }

    /// <summary>
    /// Updates historical data of the portfolio.
    /// </summary>
    /// <param name="marketEvent">Market event containing the updated historical data.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when the marketEvent parameter is null.</exception>
    void UpdateHistoricalData(MarketEvent marketEvent)
    {
        // Ensure that the marketEvent is not null.
        Guard.AgainstNull(() => marketEvent); // Throws ArgumentNullException when the marketEvent parameter is null

        // Update historical market data with new data from the MarketEvent
        HistoricalMarketData[marketEvent.Timestamp] = marketEvent.HistoricalMarketData;
        HistoricalFxConversionRates[marketEvent.Timestamp] = marketEvent.HistoricalFxConversionRates;
    }

    /// <summary>
    /// Asynchronously allocates capital to the portfolio's strategies based on their defined allocation rules.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method is typically called at the start of each trading period (day, hour, minute, etc.) 
    /// to allocate the portfolio's available capital to the strategies based on their capital allocation rules. 
    /// The method implementation should ensure that the sum of the allocated capital does not exceed 
    /// the total available capital in the portfolio.
    /// </remarks>
    Task AllocateCapitalAsync();

    /// <summary>
    /// Updates the cash balance for each strategy that holds a given asset in response to a dividend event.
    /// </summary>
    /// <param name="asset">The asset symbol for which the dividend event occurred.</param>
    /// <param name="dividendPerShare">The amount of dividend received per share.</param>
    /// <exception cref="System.ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a dividend event occurs for an asset held by one or more strategies in the portfolio.
    /// The method implementation should ensure that the cash balance for each strategy that holds the asset is updated 
    /// by adding the total dividend amount (dividend per share * position quantity) to the current cash balance.
    /// </remarks>
    void UpdateCashForDividend(string asset, decimal dividendPerShare)
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
    /// <exception cref="System.ArgumentNullException">Thrown when the orderEvent parameter is null.</exception>
    /// <remarks>
    /// This method is called when an order needs to be submitted to the brokerage.
    /// The method implementation should ensure that the necessary checks and validations are performed on the order event data 
    /// before creating and submitting the order to the brokerage.
    /// </remarks>
    async Task<bool> SubmitOrderAsync(OrderEvent orderEvent)
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
        return await Broker.SubmitOrderAsync(order);
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
    IEnumerable<SignalEvent> GenerateSignals(
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
    /// <exception cref="System.ArgumentException">Thrown when the strategyName or asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a position for a given asset in a specific strategy needs to be updated.
    /// The method implementation should ensure that the position is updated correctly 
    /// and that the new position does not lead to an inconsistent portfolio state.
    /// </remarks>
    void UpdatePosition(string strategyName, string asset, int quantity)
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
    /// <exception cref="System.ArgumentException">Thrown when the strategyName parameter is null, empty, or consists only of white-space characters.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the currency parameter is not a defined value of the CurrencyCode enumeration.</exception>
    /// <remarks>
    /// This method is called when the cash balance for a specific strategy and currency needs to be updated.
    /// The method implementation should ensure that the cash balance is updated correctly and that the new cash balance does not lead to an inconsistent portfolio state.
    /// </remarks>
    void UpdateCash(string strategyName, CurrencyCode currency, decimal amount)
    {
        Guard.AgainstNullOrWhiteSpace(() => strategyName); // Throws ArgumentException
        Guard.AgainstUndefinedEnumValue(() => currency); // Throws ArgumentOutOfRangeException

        var strategy = GetStrategy(strategyName);
        strategy.UpdateCash(currency, amount);
    }

    /// <summary>
    /// Updates the daily return for a given asset in a specific strategy.
    /// </summary>
    /// <param name="strategyName">The name of the strategy.</param>
    /// <param name="asset">The asset symbol.</param>
    /// <param name="timestamp">The timestamp of the return.</param>
    /// <param name="returnAmount">The amount of the return.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method is called when the daily return for a given asset in a specific strategy needs to be updated.
    /// The method implementation should ensure that the return is updated correctly and that the updated return does not lead to an inconsistent portfolio state.
    /// </remarks>
    Task UpdateDailyReturnAsync(
        string strategyName, 
        string asset, 
        DateOnly timestamp, 
        decimal returnAmount);

    /// <summary>
    /// Updates the equity curve for the portfolio.
    /// </summary>
    /// <param name="timestamp">The timestamp for the equity curve update.</param>
    /// <param name="baseCurrency">The base currency used for calculations.</param>
    /// <remarks>
    /// This method is called when the equity curve for the portfolio needs to be updated.
    /// The method implementation should ensure that the equity curve is updated correctly and that the updated equity curve accurately reflects the current state of the portfolio.
    /// </remarks>
    void UpdateEquityCurve(
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
    /// <exception cref="System.ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a stock split has occurred, and the portfolio's positions need to be adjusted.
    /// The method implementation should ensure that the positions are adjusted correctly and that the adjusted positions do not lead to an inconsistent portfolio state.
    /// </remarks>
    void AdjustPositionForSplit(
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
    /// <exception cref="System.ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a stock split has occurred, and the portfolio's historical data needs to be adjusted.
    /// The method implementation should ensure that the historical data is adjusted correctly.
    /// </remarks>
    void AdjustHistoricalDataForSplit(
        string asset,
        decimal splitRatio)
    {
        Guard.AgainstNullOrWhiteSpace(() => asset); // Throws ArgumentException

        // Adjust the historical market data for the affected asset.
        foreach (var historicalData in HistoricalMarketData.Values)
        {
            if (historicalData.TryGetValue(asset, out var marketData))
            {
                marketData.AdjustForSplit(splitRatio);
            }
        }
    }

    /// <summary>
    /// Retrieves a strategy based on its name.
    /// </summary>
    /// <param name="strategyName">The name of the strategy to retrieve.</param>
    /// <returns>The strategy associated with the provided name.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the strategyName parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a strategy needs to be retrieved based on its name.
    /// The method implementation should ensure that the correct strategy is retrieved, or an appropriate error is thrown if the strategy cannot be found.
    /// </remarks>
    IStrategy GetStrategy(string strategyName)
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
    /// <exception cref="System.ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when the currency of a specific asset needs to be retrieved.
    /// The method implementation should ensure that the correct currency is returned, or an appropriate error is thrown if the currency cannot be found.
    /// </remarks>
    CurrencyCode GetAssetCurrency(string asset)
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
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the baseCurrency parameter is not a defined value of the CurrencyCode enumeration.</exception>
    /// <remarks>
    /// This method is called when the total value of the portfolio needs to be calculated.
    /// The method implementation should ensure that the calculation is accurate and that it correctly reflects the current state of the portfolio.
    /// </remarks>
    decimal CalculateTotalPortfolioValue(
        DateOnly timestamp, 
        CurrencyCode baseCurrency)
    {
        Guard.AgainstUndefinedEnumValue(() => baseCurrency); // Throws ArgumentOutOfRangeException

        return Strategies
            .Select(strategyPair => strategyPair.Value)
            .Select(strategy => strategy.ComputeTotalValue(timestamp, baseCurrency, HistoricalMarketData, HistoricalFxConversionRates))
            .Sum();
    }
}
