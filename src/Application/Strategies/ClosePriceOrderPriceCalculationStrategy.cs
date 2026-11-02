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

namespace Boutquin.Trading.Application.Strategies;

/// <summary>
/// Implementation of the IOrderPriceCalculationStrategy that uses closing prices to determine order prices for various order types.
/// </summary>
/// <remarks>
/// This strategy always returns a Market order type with the closing price.
/// It does not calculate separate prices for Limit, Stop, or StopLimit order types.
/// </remarks>
public sealed class ClosePriceOrderPriceCalculationStrategy : IOrderPriceCalculationStrategy
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
    public (OrderType OrderType, decimal PrimaryPrice, decimal? SecondaryPrice)
        CalculateOrderPrices(
            DateOnly timestamp,
            Symbol asset,
            TradeAction tradeAction,
            IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>> historicalData)
    {
        // Exact-date lookup first, then cross-calendar fallback to the most recent prior bar.
        // See <see cref="HistoricalMarketDataLookup.TryFindMostRecentBar"/> for rationale — a
        // CA portfolio placing an order on a TSX trading day that happens to be a US holiday
        // (or where the provider has a data gap) would otherwise throw, aborting the backtest.
        Bar? marketData = historicalData.TryGetValue(timestamp, out var marketDataForDate) &&
            marketDataForDate is not null &&
            marketDataForDate.TryGetValue(asset, out var exactBar)
            ? exactBar
            : HistoricalMarketDataLookup.TryFindMostRecentBar(historicalData, asset, timestamp);

        if (marketData is null)
        {
            throw new ArgumentException($"No market data found for the specified asset: {asset.Ticker} on date: {timestamp}");
        }

        const OrderType OrderType = OrderType.Market; // Default to Market order for simplicity.
        // Use AdjustedClose so that fill prices are consistent with position sizing
        // and valuation (both use AdjustedClose). Using raw Close creates a price
        // mismatch on split dates where Close is discontinuous but AdjustedClose is not.
        var primaryPrice = marketData.Value.AdjustedClose;

        // TYP-A02: Return null for SecondaryPrice when not applicable
        return (OrderType, primaryPrice, null);
    }
}
