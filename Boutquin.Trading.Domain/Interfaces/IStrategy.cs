﻿// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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
    /// Gets a sorted dictionary of assets and their associated currency codes.
    /// The key is the asset symbol and the value is the asset's currency code.
    /// </summary>
    SortedDictionary<string, CurrencyCode> Assets { get; }

    /// <summary>
    /// Gets or sets the target capital allocated to this strategy as a sorted dictionary, where the key
    /// is the currency code, and the value is the amount of capital allocated in that currency.
    /// </summary>
    /// <remarks>
    /// The TargetCapital property is used to store the capital allocated to this strategy, which
    /// is calculated by an ICapitalAllocationStrategy implementation. It is used by the position sizer
    /// to determine the appropriate position size for assets in the strategy's portfolio.
    /// </remarks>
    SortedDictionary<CurrencyCode, decimal> TargetCapital { get; set; }

    /// <summary>
    /// Gets or sets the available cash for this strategy as a sorted dictionary, where the key
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
    /// Generates a sequence of signal events for the given timestamp, target capital, historical market data, and historical FX conversion rates.
    /// </summary>
    /// <param name="timestamp">The date for which to generate the signals.</param>
    /// <param name="targetCapital">The target capital allocated to the assets.</param>
    /// <param name="historicalMarketData">The historical market data for the assets.</param>
    /// <param name="historicalFxConversionRates">The historical FX conversion rates.</param>
    /// <returns>A sequence of signal events generated by the strategy.</returns>
    /// <remarks>
    /// The GenerateSignals method should be implemented by the trading strategy to analyze
    /// the market data and generate appropriate trading signals (e.g., buy or sell signals)
    /// based on the strategy's specific rules and conditions, taking into account the target
    /// capital and foreign exchange conversion rates.
    /// </remarks>
    IEnumerable<SignalEvent> GenerateSignals(
        DateOnly timestamp,
        SortedDictionary<CurrencyCode, decimal> targetCapital,
        SortedDictionary<DateOnly, SortedDictionary<string, MarketData>> historicalMarketData,
        SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates);
}
