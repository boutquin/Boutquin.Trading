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

using Data;

using Enums;

using Events;

/// <summary>
/// The IStrategy interface defines the structure and behavior of a trading
/// strategy, providing methods for generating trading signals, managing
/// positions, and calculating equity.
/// </summary>
/// <remarks>
/// A custom trading strategy should implement this interface to define
/// its logic and interact with the trading framework.
/// </remarks>
public interface IStrategy
{
    /// <summary>
    /// The name of the strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the current positions of the strategy, represented as a sorted
    /// dictionary where the key is the asset symbol and the value is the
    /// quantity of the asset held.
    /// </summary>
    SortedDictionary<string, int> Positions { get; }

    /// <summary>
    /// Gets a read-only dictionary of assets and their associated currency codes.
    /// The key is the asset symbol and the value is the asset's currency code.
    /// </summary>
    IReadOnlyDictionary<string, CurrencyCode> Assets { get; }

    /// <summary>
    /// Gets the available cash for this strategy as a sorted dictionary, where the key
    /// is the currency code, and the value is the amount of cash available in that currency.
    /// </summary>
    /// <remarks>
    /// The Cash property is used to store the available cash for this strategy in different currencies.
    /// It is updated when the strategy executes trades, and it affects the position sizing decisions
    /// made by the IPositionSizer implementation used by the strategy.
    /// </remarks>
    SortedDictionary<CurrencyCode, decimal> Cash { get; }


    /// <summary>
    /// Gets the instance of the IOrderPriceCalculationStrategy associated with the strategy.
    /// </summary>
    /// <remarks>
    /// The IOrderPriceCalculationStrategy is responsible for determining the appropriate order prices
    /// and type (e.g., market, limit, stop, stop-limit) based on the historical market data and other
    /// relevant factors. It helps the strategy to decide how to execute orders when generating signals.
    /// </remarks>
    IOrderPriceCalculationStrategy OrderPriceCalculationStrategy { get; }

    /// <summary>
    /// Gets the position sizer associated with the strategy, which is
    /// responsible for determining the size of positions taken in each asset.
    /// </summary>
    IPositionSizer PositionSizer { get; }

    /// <summary>
    /// Generates trading signals for the strategy based on historical market data and foreign exchange conversion rates.
    /// The signals are used to drive trading decisions, such as entering or exiting positions.
    /// </summary>
    /// <param name="timestamp">The timestamp for the historical data to be used in generating the signals.</param>
    /// <param name="historicalMarketData">A dictionary containing historical market data, such as prices and volumes, indexed by date and asset symbols.</param>
    /// <param name="baseCurrency">The base currency used for the calculations and conversions within the strategy.</param>
    /// <param name="historicalFxConversionRates">A dictionary containing historical foreign exchange conversion rates, indexed by date and currency codes.</param>
    /// <returns>A SignalEvent instance containing the generated signals for the strategy.</returns>
    /// <remarks>
    /// This method is responsible for generating trading signals based on the historical market data and foreign exchange conversion rates.
    /// The implementation should consider various factors like price trends, market indicators, and other technical or fundamental analysis techniques.
    /// The generated signals are then used to drive trading decisions, such as entering or exiting positions.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when the historicalMarketData or historicalFxConversionRates are null.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when an undefined CurrencyCode is provided as the baseCurrency.</exception>
    SignalEvent GenerateSignals(
        DateOnly timestamp,
        CurrencyCode baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<string, MarketData>?> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates);

    /// <summary>
    /// Computes the total value of the strategy in the base currency using historical market data and foreign exchange conversion rates.
    /// The total value is calculated as the sum of the values of all assets and cash holdings in the strategy.
    /// </summary>
    /// <param name="timestamp">The timestamp for the historical data to be used in calculating the total value.</param>
    /// <param name="historicalMarketData">A dictionary containing historical market data, such as prices and volumes, indexed by date and asset symbols.</param>
    /// <param name="baseCurrency">The base currency used for the calculations and conversions within the strategy.</param>
    /// <param name="historicalFxConversionRates">A dictionary containing historical foreign exchange conversion rates, indexed by date and currency codes.</param>
    /// <returns>The total value of the strategy in the base currency.</returns>
    /// <remarks>
    /// This method is responsible for computing the total value of the strategy in the base currency.
    /// The total value is calculated as the sum of the values of all assets and cash holdings in the strategy, considering historical market data and foreign exchange conversion rates.
    /// The computed total value can be used for various purposes, such as risk management, portfolio rebalancing, or performance evaluation.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">Thrown when the historicalMarketData or historicalFxConversionRates are null.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when an undefined CurrencyCode is provided as the baseCurrency.</exception>
    decimal ComputeTotalValue(
        DateOnly timestamp,
        CurrencyCode baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<string, MarketData>?> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(() => baseCurrency); // Throws ArgumentOutOfRangeException
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalMarketData); // Throws EmptyOrNullDictionaryException
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalFxConversionRates); // Throws EmptyOrNullDictionaryException

        var totalValue = 0m;

        // Iterate through each asset in the strategy
        foreach (var (asset, assetCurrency) in Assets)
        {
            // Get the asset's position size
            Positions.TryGetValue(asset, out var positionSize);

            // Get the historical market data for the asset
            if (historicalMarketData.TryGetValue(timestamp, out var assetMarketData) &&
                assetMarketData.TryGetValue(asset, out var marketData))
            {
                // Calculate the asset's value in its native currency
                var assetValue = positionSize * marketData.AdjustedClose;

                // If the asset's currency is not the base currency, convert the asset value to the base currency
                if (assetCurrency != baseCurrency)
                {
                    if (historicalFxConversionRates.TryGetValue(timestamp, out var fxRates) &&
                        fxRates.TryGetValue(assetCurrency, out var conversionRate))
                    {
                        assetValue *= conversionRate;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Conversion rate not found for currency {assetCurrency} at timestamp {timestamp}");
                    }
                }

                // Add the asset's value to the total value
                totalValue += assetValue;
            }
            else
            {
                throw new InvalidOperationException($"Market data not found for asset {asset} at timestamp {timestamp}");
            }
        }

        // Iterate through each cash entry and convert to the base currency if necessary
        foreach (var cashEntry in Cash)
        {
            var cashCurrency = cashEntry.Key;
            var cashAmount = cashEntry.Value;

            if (cashCurrency != baseCurrency)
            {
                if (historicalFxConversionRates.TryGetValue(timestamp, out var fxRates) &&
                    fxRates.TryGetValue(cashCurrency, out var conversionRate))
                {
                    cashAmount *= conversionRate;
                }
                else
                {
                    throw new InvalidOperationException($"Conversion rate not found for currency {cashCurrency} at timestamp {timestamp}");
                }
            }

            totalValue += cashAmount;
        }

        return totalValue;
    }

    /// <summary>
    /// Updates the cash holdings in a particular currency for this strategy.
    /// If the strategy already has a cash balance in the specified currency, it adds the amount to the existing balance.
    /// If the strategy does not have a cash balance in the specified currency, it creates a new balance with the specified amount.
    /// </summary>
    /// <param name="currency">The currency of the cash holdings to be updated. Must be a valid member of the CurrencyCode enumeration.</param>
    /// <param name="amount">The amount by which the cash holdings in the specified currency should be updated.</param>
    /// <remarks>
    /// This method should be used whenever the strategy executes a trade and needs to update its cash balance accordingly.
    /// The currency parameter must be a valid CurrencyCode, and the amount should reflect the net change in the strategy's cash balance in the specified currency as a result of the trade.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when an undefined CurrencyCode is provided.</exception>
    void UpdateCash(CurrencyCode currency, decimal amount)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(() => currency); // Throws ArgumentOutOfRangeException

        if (!Cash.TryAdd(currency, amount))
        {
            Cash[currency] += amount;
        }
    }

    /// <summary>
    /// Updates the position quantity for a particular asset in this strategy.
    /// If the strategy already has a position in the specified asset, it adds the quantity to the existing position.
    /// If the strategy does not have a position in the specified asset, it creates a new position with the specified quantity.
    /// </summary>
    /// <param name="asset">The asset whose position quantity needs to be updated. Must be a non-null, non-empty, non-whitespace string.</param>
    /// <param name="quantity">The quantity by which the position in the specified asset should be updated.</param>
    /// <remarks>
    /// This method should be used whenever the strategy executes a trade and needs to update its position quantity accordingly.
    /// The asset parameter must be a valid asset symbol, and the quantity should reflect the net change in the strategy's position quantity as a result of the trade.
    /// </remarks>
    /// <exception cref="System.ArgumentException">Thrown when a null, empty, or whitespace string is provided for the <paramref name="asset"/>.</exception>
    void UpdatePositions(string asset, int quantity)
    {
        // Validate parameters
        Guard.AgainstNullOrWhiteSpace(() => asset); // Throws ArgumentException

        if (!Positions.TryAdd(asset, quantity))
        {
            Positions[asset] += quantity;
        }
    }

    /// <summary>
    /// Updates the position quantity for a particular asset in this strategy.
    /// If the strategy already has a position in the specified asset, it adds the quantity to the existing position.
    /// If the strategy does not have a position in the specified asset, it creates a new position with the specified quantity.
    /// </summary>
    /// <param name="asset">The asset whose position quantity needs to be updated. Must be a non-null, non-empty, non-whitespace string.</param>
    /// <param name="quantity">The quantity by which the position in the specified asset should be updated.</param>
    /// <remarks>
    /// This method should be used whenever the strategy executes a trade and needs to update its position quantity accordingly.
    /// The asset parameter must be a valid asset symbol, and the quantity should reflect the net change in the strategy's position quantity as a result of the trade.
    /// </remarks>
    /// <exception cref="System.ArgumentException">Thrown when a null, empty, or whitespace string is provided for the <paramref name="asset"/>.</exception>
    int GetPositionQuantity(string asset)
    {
        // Validate parameters
        Guard.AgainstNullOrWhiteSpace(() => asset); // Throws ArgumentException

        return Positions.GetValueOrDefault(asset, 0);
    }
}
