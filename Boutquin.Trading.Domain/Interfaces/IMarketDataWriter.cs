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
/// The IMarketDataWriter interface defines the methods for saving
/// historical market data and dividend data to a data store.
/// </summary>
public interface IMarketDataWriter
{
    /// <summary>
    /// Persists the historical market data to a data store.
    /// </summary>
    /// <param name="marketData">A SortedDictionary with timestamps as keys
    /// and MarketData objects as values, representing the historical market
    /// data for specified assets within a specified date range.
    /// </param>
    Task SaveHistoricalMarketDataAsync(SortedDictionary<DateOnly, MarketData> marketData);
}
