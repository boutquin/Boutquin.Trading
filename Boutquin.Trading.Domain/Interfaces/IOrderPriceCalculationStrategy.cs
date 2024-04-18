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

/// <summary>
/// Defines an interface for calculating order prices based on historical market data, given an asset and a trade action.
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
    /// Calculates the order prices (primary and secondary) and the order type for a given asset, trade action, and historical market data.
    /// </summary>
    /// <param name="timestamp">The timestamp at which the order prices are to be calculated.</param>
    /// <param name="asset">The asset for which the order prices are to be calculated, represented as a string.</param>
    /// <param name="tradeAction">The trade action (buy or sell) for which the order prices are to be calculated.</param>
    /// <param name="historicalData">The historical market data, organized as a dictionary with timestamps as keys and dictionaries of asset market data as values.</param>
    /// <returns>A tuple containing the order type, primary price, and secondary price for the calculated order.</returns>
    (OrderType OrderType, decimal PrimaryPrice, decimal SecondaryPrice) 
        CalculateOrderPrices(
            DateOnly timestamp,
            Ticker asset,
            TradeAction tradeAction,
            IReadOnlyDictionary<DateOnly, SortedDictionary<Ticker, MarketData>?> historicalData);
}
