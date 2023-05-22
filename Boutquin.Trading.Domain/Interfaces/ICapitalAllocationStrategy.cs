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

/// <summary>
/// The ICapitalAllocationStrategy interface defines a method for allocating capital across multiple strategies.
/// This interface is used by the Portfolio class to compute the SortedDictionary&lt;CurrencyCode, decimal&gt; TargetCapital,
/// which is used for generating signals in each strategy.
/// </summary>
public interface ICapitalAllocationStrategy
{
    /// <summary>
    /// Allocates capital across multiple strategies based on their performance, taking into account historical market data and
    /// foreign exchange conversion rates.
    /// </summary>
    /// <param name="strategies">An ImmutableList of IStrategy objects representing the trading strategies to allocate capital to.</param>
    /// <param name="historicalMarketData">A read-only dictionary containing historical market data for multiple assets.</param>
    /// <param name="historicalFxConversionRates">A read-only dictionary containing historical foreign exchange conversion rates for each currency.</param>
    /// <returns>A sorted dictionary containing the allocated capital for each strategy, with the strategy's name as the key and a SortedDictionary&lt;CurrencyCode, decimal&gt; representing the allocated capital in each currency as the value.</returns>
    /// <remarks>
    /// The AllocateCapital method should be implemented by the capital allocation strategy to distribute capital among the strategies
    /// based on their performance or other criteria. The method should take into account historical market data and foreign exchange
    /// conversion rates to ensure that the allocated capital is appropriate for each strategy.
    /// </remarks>
    IReadOnlyDictionary<string, SortedDictionary<CurrencyCode, decimal>> AllocateCapital(
        ImmutableList<IStrategy> strategies,
        IReadOnlyDictionary<DateOnly, SortedDictionary<string, MarketData>?> historicalMarketData,
        IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates);
}
