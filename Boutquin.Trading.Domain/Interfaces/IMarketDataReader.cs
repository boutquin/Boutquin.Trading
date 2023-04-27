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
using Boutquin.Trading.Domain.Events;

namespace Boutquin.Trading.Domain.Interfaces;

/// <summary>
/// The IMarketDataReader interface defines the methods for loading
/// historical market data and dividend data from a data source.
/// </summary>
public interface IMarketDataReader
{
    /// <summary>
    /// Loads historical market data for the specified assets within
    /// the specified date range, returning a SortedDictionary with
    /// timestamps as keys and MarketData objects as values.
    /// </summary>
    /// <param name="assets">The assets for which to load market data,
    /// represented as an IEnumerable of strings.
    /// </param>
    /// <param name="startDate">The start date of the date range for which
    /// to load market data, represented as a DateTime object.
    /// </param>
    /// <param name="endDate">The end date of the date range for which
    /// to load market data, represented as a DateTime object.
    /// </param>
    /// <returns>A SortedDictionary with timestamps as keys and MarketData
    /// objects as values, representing the historical market data for
    /// the specified assets within the specified date range.
    /// </returns>
    Task<SortedDictionary<DateOnly, MarketData>> LoadHistoricalMarketDataAsync(
        IEnumerable<string> assets,
        DateOnly startDate,
        DateOnly endDate);

    /// <summary>
    /// Loads historical dividend data for the specified assets within
    /// the specified date range, returning a SortedDictionary with
    /// timestamps as keys and DividendData objects as values.
    /// </summary>
    /// <param name="assets">The assets for which to load dividend data,
    /// represented as an IEnumerable of strings.
    /// </param>
    /// <param name="startDate">The start date of the date range for which
    /// to load dividend data, represented as a DateTime object.
    /// </param>
    /// <param name="endDate">The end date of the date range for which
    /// to load dividend data, represented as a DateTime object.
    /// </param>
    /// <returns>A SortedDictionary with timestamps as keys and DividendEvent
    /// objects as values, representing the historical dividend data for
    /// the specified assets within the specified date range.
    /// </returns>
    Task<SortedDictionary<DateOnly, DividendData>> LoadHistoricalDividendDataAsync(
        IEnumerable<string> assets,
        DateOnly startDate,
        DateOnly endDate);
}
