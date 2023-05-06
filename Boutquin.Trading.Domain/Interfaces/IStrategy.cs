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

using System.Collections.Immutable;

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
    /// Gets the currency code associated with each asset in the strategy,
    /// represented as a sorted dictionary where the key is the asset symbol
    /// and the value is the CurrencyCode.
    /// </summary>
    SortedDictionary<string, CurrencyCode> AssetCurrencies { get; }

    /// <summary>
    /// Gets an immutable list of asset symbols held by the strategy.
    /// </summary>
    ImmutableList<string> Assets { get; }

    /// <summary>
    /// Gets or sets the capital held in each currency by the strategy,
    /// represented as a sorted dictionary where the key is the CurrencyCode
    /// and the value is the amount of capital in that currency.
    /// </summary>
    SortedDictionary<CurrencyCode, decimal> Capital { get; set; }

    /// <summary>
    /// Gets the price calculation strategy associated with the trading strategy,
    /// which is responsible for determining the price used for trade execution
    /// based on the available market data.
    /// </summary>
    IPriceCalculationStrategy PriceCalculationStrategy { get; }

    /// <summary>
    /// Generates trading signals based on the provided market data, target capital, and historical foreign exchange conversion rates.
    /// </summary>
    /// <param name="targetCapital">A sorted dictionary containing the target capital for each currency.</param>
    /// <param name="historicalMarketData">A sorted dictionary containing historical market data for multiple assets.</param>
    /// <param name="historicalFxConversionRates">A sorted dictionary containing historical foreign exchange conversion rates for each currency.</param>
    /// <returns>An enumerable of SignalEvent objects representing the generated trading signals.</returns>
    /// <remarks>
    /// The GenerateSignals method should be implemented by the trading strategy to analyze
    /// the market data and generate appropriate trading signals (e.g., buy or sell signals)
    /// based on the strategy's specific rules and conditions, taking into account the target capital and foreign exchange conversion rates.
    /// </remarks>
    IEnumerable<SignalEvent> GenerateSignals(
        SortedDictionary<CurrencyCode, decimal> targetCapital,
        SortedDictionary<DateOnly, SortedDictionary<string, MarketData>> historicalMarketData,
        SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates);

    /// <summary>
    /// Calculates the total equity of the strategy, considering the capital
    /// held in each currency and the value of the assets held.
    /// </summary>
    /// <returns>The total equity of the strategy, represented as a decimal value.</returns>
    decimal CalculateEquity();

    /// <summary>
    /// Gets the rebalancing frequency of the strategy, which determines how
    /// often the strategy should adjust its asset allocations.
    /// </summary>
    RebalancingFrequency RebalancingFrequency { get; }

    /// <summary>
    /// Gets the position sizer associated with the strategy, which is
    /// responsible for determining the size of positions taken in each asset.
    /// </summary>
    IPositionSizer PositionSizer { get; }
}
