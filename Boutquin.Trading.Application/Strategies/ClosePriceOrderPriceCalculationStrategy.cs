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
namespace Boutquin.Trading.Application.Strategies;

/// <summary>
/// Implementation of the IOrderPriceCalculationStrategy that uses closing prices to determine order prices for various order types.
/// </summary>
/// <remarks>
/// This strategy calculates order prices based on the closing price of the asset on the specified date. It supports Market, Limit, Stop, and StopLimit order types.
/// </remarks>
public class ClosePriceOrderPriceCalculationStrategy : IOrderPriceCalculationStrategy
{
    /// <summary>
    /// Calculates the order prices and the order type for a given asset, trade action, and historical market data.
    /// </summary>
    /// <param name="timestamp">The timestamp at which the order prices are to be calculated.</param>
    /// <param name="asset">The asset for which the order prices are to be calculated.</param>
    /// <param name="tradeAction">The trade action (buy or sell) for which the order prices are to be calculated.</param>
    /// <param name="historicalData">The historical market data, organized as a dictionary with timestamps as keys and dictionaries of asset market data as values.</param>
    /// <returns>A tuple containing the order type, primary price, and secondary price for the calculated order.</returns>
    /// <exception cref="ArgumentException">Thrown when no market data is found for the specified date and asset.</exception>
    public (OrderType OrderType, decimal PrimaryPrice, decimal SecondaryPrice)
        CalculateOrderPrices(
            DateOnly timestamp,
            Domain.ValueObjects.Asset asset,
            TradeAction tradeAction,
            IReadOnlyDictionary<DateOnly, SortedDictionary<Domain.ValueObjects.Asset, MarketData>?> historicalData)
    {
        // Check if there is market data for the specified timestamp.
        if (!historicalData.TryGetValue(timestamp, out var marketDataForDate))
        {
            throw new ArgumentException($"No market data found for the specified date: {timestamp}");
        }

        // Check if there is market data for the specified asset.
        if (marketDataForDate == null || !marketDataForDate.TryGetValue(asset, out var marketData))
        {
            throw new ArgumentException($"No market data found for the specified asset: {asset.Ticker} on date: {timestamp}");
        }

        const OrderType OrderType = OrderType.Market; // Default to Market order for simplicity.
        var primaryPrice = marketData.Close; // For Market orders, use the closing price directly.
        const decimal SecondaryPrice = 0m; // Default secondary price, used only for StopLimit orders.

        // Return the order details as a tuple.
        return (OrderType, primaryPrice, SecondaryPrice);
    }
}
