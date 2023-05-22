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
/// The IMarketDataFetcher interface defines the contract for fetching historical
/// market data for specified financial assets.
/// </summary>
public interface IMarketDataFetcher
{
    /// <summary>
    /// Fetches historical market data for the specified financial assets and
    /// returns an asynchronous stream of key-value pairs, where the key is a DateOnly object
    /// representing the date and the value is a sorted dictionary of asset symbols and their
    /// corresponding MarketData objects.
    /// </summary>
    /// <param name="symbols">A list of financial asset symbols for which to fetch historical market data.</param>
    /// <returns>An IAsyncEnumerable of key-value pairs, where the key is a DateOnly object and the value is a SortedDictionary of string asset symbols and MarketData values.</returns>
    /// <exception cref="MarketDataRetrievalException">Thrown when there is an error in fetching or parsing the market data.</exception>
    IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<string, MarketData>?>> FetchMarketDataAsync(IEnumerable<string> symbols);

    /// <summary>
    /// Fetches historical foreign exchange rates for the specified currency pairs and
    /// returns an asynchronous stream of key-value pairs, where the key is a DateOnly object
    /// representing the date and the value is a sorted dictionary of currency pair symbols and their
    /// corresponding exchange rates.
    /// </summary>
    /// <param name="currencyPairs">A list of currency pair symbols for which to fetch historical exchange rates.</param>
    /// <returns>An IAsyncEnumerable of key-value pairs, where the key is a DateOnly object and the value is a SortedDictionary of string currency pair symbols and decimal exchange rates.</returns>
    /// <exception cref="FxDataRetrievalException">Thrown when there is an error in fetching or parsing the foreign exchange data.</exception>
    IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> FetchFxRatesAsync(IEnumerable<string> currencyPairs);
}
