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
namespace Boutquin.Trading.Domain.Data;

/// <summary>
/// The MarketData record encapsulates the data points for various market-related
/// events for a specific financial asset at a specific point in time,
/// providing information about the opening price, highest price, lowest price,
/// closing price, adjusted closing price, trading volume, dividend per share,
/// and split coefficient.
/// </summary>
/// <param name="Timestamp">The timestamp of the market data event,
/// represented as a DateOnly object.
/// </param>
/// <param name="Open">The opening price of the financial asset at the
/// beginning of the trading session, represented as a decimal value.
/// </param>
/// <param name="High">The highest price at which the financial asset traded
/// during the trading session, represented as a decimal value.
/// </param>
/// <param name="Low">The lowest price at which the financial asset traded
/// during the trading session, represented as a decimal value.
/// </param>
/// <param name="Close">The closing price of the financial asset at the
/// end of the trading session, represented as a decimal value.
/// </param>
/// <param name="AdjustedClose">The adjusted closing price of the financial
/// asset, accounting for corporate actions such as dividends, stock splits,
/// and new stock offerings, represented as a decimal value.
/// </param>
/// <param name="Volume">The total number of shares of the financial asset
/// traded during the trading session, represented as a long value.
/// </param>
/// <param name="DividendPerShare">The dividend per share paid by the
/// financial asset during the dividend event, if any, represented as a decimal
/// value.
/// </param>
/// <param name="SplitCoefficient">The split coefficient of the financial
/// asset, which indicates the proportion of shares after a stock split event,
/// represented as a decimal value. A value of 1 indicates no stock split.
/// </param>
public sealed record MarketData(
    DateOnly Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal AdjustedClose,
    long Volume,
    decimal DividendPerShare,
    decimal SplitCoefficient)
{
    /// <summary>
    /// Adjusts the market data for a split event by applying the given split ratio and returns
    /// a new instance of MarketData with the adjusted values.
    /// </summary>
    /// <param name="splitEventSplitRatio">The split ratio to apply.</param>
    /// <returns>A new instance of MarketData with the adjusted values.</returns>
    /// <remarks>
    /// The method creates a new instance of MarketData with adjusted Open, High, Low, Close,
    /// AdjustedClose, Volume, and SplitCoefficient properties to account for the split event.
    /// </remarks>
    public MarketData AdjustForSplit(decimal splitEventSplitRatio)
    {
        return new MarketData(
            Timestamp,
            Open / splitEventSplitRatio,
            High / splitEventSplitRatio,
            Low / splitEventSplitRatio,
            Close / splitEventSplitRatio,
            AdjustedClose / splitEventSplitRatio,
            (long)(Volume * splitEventSplitRatio),
            DividendPerShare,
            SplitCoefficient * splitEventSplitRatio);
    }
}
