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
namespace Boutquin.Trading.Domain.Interfaces;

using ValueObjects;

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
    public bool IsLive { get; }

    /// <summary>
    /// Gets the base currency of the portfolio.
    /// </summary>
    /// <value>The base currency of the portfolio.</value>
    /// <remarks>
    /// The base currency is the currency in which the portfolio's value is calculated and reported.
    /// All cash balances, asset prices, and portfolio values are converted to the base currency for calculations and reporting.
    /// </remarks>
    CurrencyCode BaseCurrency { get; }

    /// <summary>
    /// The EventProcessor property represents a system to process events for the portfolio.
    /// </summary>
    public IEventProcessor EventProcessor { get; }

    /// <summary>
    /// The Strategies property represents a read-only dictionary of strategies used in the portfolio.
    /// </summary>
    IReadOnlyDictionary<string, IStrategy> Strategies { get; }

    /// <summary>
    /// The AssetCurrencies property represents a read-only dictionary of assets and their respective currencies used in the portfolio.
    /// </summary>
    IReadOnlyDictionary<Ticker, CurrencyCode> AssetCurrencies { get; }

    /// <summary>
    /// The HistoricalMarketData property represents a sorted dictionary of historical market data used by the portfolio.
    /// </summary>
    SortedDictionary<DateOnly, SortedDictionary<Ticker, MarketData>?> HistoricalMarketData { get; }

    /// <summary>
    /// The HistoricalFxConversionRates property represents a sorted dictionary of historical foreign exchange conversion rates used by the portfolio.
    /// </summary>
    SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> HistoricalFxConversionRates { get; }

    /// <summary>
    /// The EquityCurve property represents a sorted dictionary of equity values over time.
    /// </summary>
    SortedDictionary<DateOnly, decimal> EquityCurve { get; }

    /// <summary>
    /// Asynchronously handles the specified event, processing it using the portfolio's event processor.
    /// </summary>
    /// <param name="event">The event to handle. This represents an occurrence in the system that may affect the state of the portfolio.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task result contains no value.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the provided event is null.</exception>
    /// <exception cref="EventProcessingException">Thrown if an error occurs while processing the event.</exception>
    /// <remarks>
    /// This method processes the given event using the portfolio's event processor. The event represents something that has happened in the system,
    /// such as a change in the market, a change in the portfolio's assets, or a change in the portfolio's strategy.
    /// The portfolio's event processor is responsible for updating the state of the portfolio based on the event.
    /// Note that the event is processed asynchronously, so the method may return before the event processing has completed.
    /// Any errors that occur during the event processing are thrown as exceptions.
    /// </remarks>
    Task HandleEventAsync(IFinancialEvent @event);

    /// <summary>
    /// Updates historical data of the portfolio.
    /// </summary>
    /// <param name="marketEvent">Market event containing the updated historical data.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when the marketEvent parameter is null.</exception>
    void UpdateHistoricalData(MarketEvent marketEvent);

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
    void UpdateCashForDividend(
        Ticker asset,
        decimal dividendPerShare);

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
    Task<bool> SubmitOrderAsync(OrderEvent orderEvent);

    /// <summary>
    /// Generates trading signals for each strategy in the portfolio based on updated market data.
    /// </summary>
    /// <param name="marketEvent">The MarketEvent containing the updated market data.</param>
    /// <returns>An enumerable of SignalEvent containing the generated signals for each strategy.</returns>
    /// <remarks>
    /// This method is called when new market data is available and trading signals need to be generated for the portfolio's strategies.
    /// The method implementation should ensure that the signal generation process for each strategy is performed 
    /// according to the strategy's signal generation rules and that the generated signals are correctly formatted and returned.
    /// </remarks>
    IEnumerable<SignalEvent> GenerateSignals(
        MarketEvent marketEvent);

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
    void UpdatePosition(
        string strategyName,
        Ticker asset,
        int quantity);

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
    void UpdateCash(
        string strategyName,
        CurrencyCode currency,
        decimal amount);

    /// <summary>
    /// Updates the equity curve for the portfolio.
    /// </summary>
    /// <param name="timestamp">The timestamp for the equity curve update.</param>
    /// <remarks>
    /// This method is called when the equity curve for the portfolio needs to be updated.
    /// The method implementation should ensure that the equity curve is updated correctly and that the updated equity curve accurately reflects the current state of the portfolio.
    /// </remarks>
    void UpdateEquityCurve(
        DateOnly timestamp);

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
        Ticker asset,
        decimal splitRatio);

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
        Ticker asset,
        decimal splitRatio);

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
    IStrategy GetStrategy(string strategyName);

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
    CurrencyCode GetAssetCurrency(Ticker asset);

    /// <summary>
    /// Calculates the total value of the portfolio.
    /// </summary>
    /// <param name="timestamp">The timestamp for which the total value is calculated.</param>
    /// <returns>The total value of the portfolio.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the baseCurrency parameter is not a defined value of the CurrencyCode enumeration.</exception>
    /// <remarks>
    /// This method is called when the total value of the portfolio needs to be calculated.
    /// The method implementation should ensure that the calculation is accurate and that it correctly reflects the current state of the portfolio.
    /// </remarks>
    decimal CalculateTotalPortfolioValue(
        DateOnly timestamp);
}
