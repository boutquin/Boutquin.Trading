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

using Boutquin.Trading.Domain.Data;

namespace Boutquin.Trading.Domain.Interfaces;

/// <summary>
/// The IPriceCalculationStrategy interface defines the contract for a price calculation
/// strategy, which is responsible for calculating a price based on a given set of historical
/// market data.
/// </summary>
public interface IPriceCalculationStrategy
{
    /// <summary>
    /// Calculates a price using the provided historical market data.
    /// </summary>
    /// <param name="historicalData">A sorted dictionary containing historical market data.</param>
    /// <returns>The calculated price as a decimal value.</returns>
    /// <remarks>
    /// The CalculatePrice method should be implemented by a specific price calculation
    /// strategy to compute a price based on the provided historical market data. The
    /// calculated price can be used for various purposes such as generating trading
    /// signals, determining position sizing, or risk management.
    /// </remarks>
    /// <example>
    /// This is an example of how the CalculatePrice method can be used:
    /// <code>
    /// IPriceCalculationStrategy priceStrategy = new MyCustomPriceStrategy();
    /// SortedDictionary&lt;DateOnly, MarketData&gt; historicalData = GetHistoricalMarketData();
    /// decimal calculatedPrice = priceStrategy.CalculatePrice(historicalData);
    /// Console.WriteLine($"Calculated price: {calculatedPrice}");
    /// </code>
    /// </example>
    decimal CalculatePrice(SortedDictionary<DateOnly, MarketData> historicalData);
}
