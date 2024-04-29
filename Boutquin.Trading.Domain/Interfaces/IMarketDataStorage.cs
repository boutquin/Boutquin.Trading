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
/// The IMarketDataStorage interface provides a contract for storing market data.
/// </summary>
public interface IMarketDataStorage
{
    /// <summary>
    /// Asynchronously saves a single market data point.
    /// </summary>
    /// <param name="dataPoint">The KeyValuePair containing the date and the market data to be saved.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if dataPoint is null.</exception>
    /// <exception cref="System.IO.IOException">Thrown if an error occurs while saving the market data.</exception>
    Task SaveMarketDataAsync(KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>?> dataPoint);

    /// <summary>
    /// Asynchronously saves multiple market data points.
    /// </summary>
    /// <param name="dataPoints">An IEnumerable of KeyValuePair containing the date and the market data to be saved.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if dataPoints is null.</exception>
    /// <exception cref="System.IO.IOException">Thrown if an error occurs while saving the market data.</exception>
    Task SaveMarketDataAsync(IEnumerable<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>> dataPoints);
}
