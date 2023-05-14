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

namespace Boutquin.Trading.Domain.Interfaces;

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
    /// Gets a sorted dictionary containing the daily native returns for each asset managed by the strategy.
    /// The outer dictionary has asset symbols as keys, and the inner dictionary has DateOnly objects as keys
    /// and the corresponding daily native returns as decimal values.
    /// </summary>
    /// <remarks>
    /// The daily native returns represent the daily percentage return of an asset in its native currency.
    /// This can be useful for various calculations, such as risk assessment and performance evaluation.
    /// </remarks>
    SortedDictionary<string, SortedDictionary<DateOnly, decimal>> DailyNativeReturns { get; }

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
    SignalEvent GenerateSignals(
        DateOnly timestamp,
        IReadOnlyDictionary<DateOnly, SortedDictionary<string, MarketData>> historicalMarketData,
        CurrencyCode baseCurrency,
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
    decimal ComputeTotalValue(
        DateOnly timestamp,
        IReadOnlyDictionary<DateOnly, SortedDictionary<string, MarketData>> historicalMarketData,
        CurrencyCode baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(() => baseCurrency); // Throws ArgumentOutOfRangeException
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalMarketData); // Throws EmptyOrNullDictionaryException
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => historicalFxConversionRates); // Throws EmptyOrNullDictionaryException

        var totalValue = 0m;

        // Iterate through each asset in the strategy
        foreach (var asset in Assets)
        {
            // Get the asset's currency
            var assetCurrency = asset.Value;

            // Get the asset's position size
            Positions.TryGetValue(asset.Key, out var positionSize);

            // Get the historical market data for the asset
            if (historicalMarketData.TryGetValue(timestamp, out SortedDictionary<string, MarketData> assetMarketData) &&
                assetMarketData.TryGetValue(asset.Key, out var marketData))
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
                throw new InvalidOperationException($"Market data not found for asset {asset.Key} at timestamp {timestamp}");
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
}
