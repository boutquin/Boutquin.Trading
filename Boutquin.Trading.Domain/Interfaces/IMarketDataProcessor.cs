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

/// <summary>
/// The IMarketDataProcessor interface provides a contract for processing and storing market data.
/// </summary>
public interface IMarketDataProcessor
{
    /// <summary>
    /// Asynchronously processes and stores market data for a list of symbols.
    /// </summary>
    /// <param name="symbols">A list of symbols representing the assets to fetch and store market data for.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if symbols is null or empty.</exception>
    /// <exception cref="MarketDataProcessingException">Thrown if an error occurs while processing the market data.</exception>
    Task ProcessAndStoreMarketDataAsync(IEnumerable<Asset> symbols);
}
