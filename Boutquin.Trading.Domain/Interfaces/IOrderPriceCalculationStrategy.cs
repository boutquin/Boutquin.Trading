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

using Data;
using Enums;

/// <summary>
/// IOrderPriceCalculationStrategy is an interface used to define a strategy for calculating
/// order prices and types for placing orders with the brokerage.
/// </summary>
/// <remarks>
/// The purpose of this interface is to allow different implementations of order price
/// calculation strategies to be used within the portfolio management system. This can be
/// useful to implement various order execution strategies, such as market orders,
/// limit orders, or stop orders, depending on the specific trading strategy and market
/// conditions.
/// </remarks>
public interface IOrderPriceCalculationStrategy
{
    /// <summary>
    /// Calculates the order type, primary price, and secondary price (if applicable) for placing an order
    /// based on the asset, timestamp, and full historical market data.
    /// </summary>
    /// <param name="asset">The asset for which the order prices and type are to be calculated.</param>
    /// <param name="timestamp">The timestamp at which the order prices and type are to be calculated.</param>
    /// <param name="historicalData">The full historical market data for the asset, used to determine the order prices and type.</param>
    /// <returns>A tuple containing the OrderType, primary price, and secondary price (if applicable).</returns>
    /// <remarks>
    /// The primary price can be used for limit and stop orders, while the secondary price
    /// is used for stop-limit orders. For market orders, the primary and secondary prices
    /// can be null. The full historical market data can be used for calculations such as
    /// moving averages or other price-based indicators.
    /// </remarks>
    (OrderType OrderType, decimal PrimaryPrice, decimal SecondaryPrice) CalculateOrderPrices(
        string asset,
        DateOnly timestamp,
        SortedDictionary<DateOnly, SortedDictionary<string, MarketData>> historicalData);
}

