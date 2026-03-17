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

namespace Boutquin.Trading.Domain.Interfaces;

using ValueObjects;

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
    /// Gets the current positions of the strategy, represented as a read-only
    /// dictionary where the key is the asset symbol and the value is the
    /// quantity of the asset held.
    /// </summary>
    IReadOnlyDictionary<Asset, int> Positions { get; }

    /// <summary>
    /// Gets a read-only dictionary of assets and their associated currency codes.
    /// The key is the asset symbol and the value is the asset's currency code.
    /// </summary>
    IReadOnlyDictionary<Asset, CurrencyCode> Assets { get; }

    /// <summary>
    /// Gets the available cash for this strategy as a read-only dictionary, where the key
    /// is the currency code, and the value is the amount of cash available in that currency.
    /// </summary>
    IReadOnlyDictionary<CurrencyCode, decimal> Cash { get; }

    /// <summary>
    /// Gets the instance of the IOrderPriceCalculationStrategy associated with the strategy.
    /// </summary>
    IOrderPriceCalculationStrategy OrderPriceCalculationStrategy { get; }

    /// <summary>
    /// Gets the position sizer associated with the strategy, which is
    /// responsible for determining the size of positions taken in each asset.
    /// </summary>
    IPositionSizer PositionSizer { get; }

    /// <summary>
    /// Generates trading signals for the strategy based on historical market data and foreign exchange conversion rates.
    /// </summary>
    /// <param name="timestamp">The timestamp for the historical data to be used in generating the signals.</param>
    /// <param name="baseCurrency">The base currency used for the calculations and conversions within the strategy.</param>
    /// <param name="historicalMarketData">A dictionary containing historical market data, indexed by date and asset symbols.</param>
    /// <param name="historicalFxConversionRates">A dictionary containing historical foreign exchange conversion rates, indexed by date and currency codes.</param>
    /// <returns>A SignalEvent instance containing the generated signals for the strategy.</returns>
    SignalEvent GenerateSignals(
        DateOnly timestamp,
        CurrencyCode baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates);

    /// <summary>
    /// Computes the total value of the strategy in the base currency using historical market data and foreign exchange conversion rates.
    /// </summary>
    /// <param name="timestamp">The timestamp for the historical data to be used in calculating the total value.</param>
    /// <param name="baseCurrency">The base currency used for the calculations and conversions within the strategy.</param>
    /// <param name="historicalMarketData">A dictionary containing historical market data, indexed by date and asset symbols.</param>
    /// <param name="historicalFxConversionRates">A dictionary containing historical foreign exchange conversion rates, indexed by date and currency codes.</param>
    /// <returns>The total value of the strategy in the base currency.</returns>
    decimal ComputeTotalValue(
        DateOnly timestamp,
        CurrencyCode baseCurrency,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates);

    /// <summary>
    /// Updates the cash holdings in a particular currency for this strategy.
    /// If the strategy already has a cash balance in the specified currency, it adds the amount to the existing balance.
    /// If the strategy does not have a cash balance in the specified currency, it creates a new balance with the specified amount.
    /// </summary>
    /// <param name="currency">The currency of the cash holdings to be updated.</param>
    /// <param name="amount">The amount by which the cash holdings in the specified currency should be updated.</param>
    void UpdateCash(CurrencyCode currency, decimal amount);

    /// <summary>
    /// Updates the position quantity for a particular asset in this strategy.
    /// If the strategy already has a position in the specified asset, it adds the quantity to the existing position.
    /// If the strategy does not have a position in the specified asset, it creates a new position with the specified quantity.
    /// </summary>
    /// <param name="asset">The asset whose position quantity needs to be updated.</param>
    /// <param name="quantity">The quantity by which the position in the specified asset should be updated.</param>
    void UpdatePositions(Asset asset, int quantity);

    /// <summary>
    /// Sets the position quantity for a particular asset to an absolute value.
    /// Used for stock split adjustments where the position must be set rather than incremented.
    /// </summary>
    /// <param name="asset">The asset whose position should be set.</param>
    /// <param name="quantity">The absolute quantity to set.</param>
    void SetPosition(Asset asset, int quantity);

    /// <summary>
    /// Gets the current position quantity for a specific asset.
    /// Returns zero if no position exists.
    /// </summary>
    /// <param name="asset">The asset to query.</param>
    /// <returns>The current position quantity, or zero if no position exists.</returns>
    int GetPositionQuantity(Asset asset);
}
