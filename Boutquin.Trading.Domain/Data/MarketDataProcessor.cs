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

using Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class MarketDataProcessor(
    IMarketDataFetcher fetcher,
    IMarketDataStorage storage,
    ILoggerFactory loggerFactory = null)
    : IMarketDataProcessor
{
    private readonly IMarketDataFetcher _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
    private readonly IMarketDataStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    private readonly ILogger _logger = loggerFactory?.CreateLogger<MarketDataProcessor>() ?? new NullLogger<MarketDataProcessor>();

    public async Task ProcessAndStoreMarketDataAsync(IEnumerable<string> symbols)
    {
        if (symbols == null || !symbols.Any())
        {
            throw new ArgumentException("At least one symbol must be provided.", nameof(symbols));
        }

        try
        {
            // Fetch the market data using the provided fetcher.
            var marketData = _fetcher.FetchMarketDataAsync(symbols);

            // Iterate through the fetched market data.
            await foreach (var dataPoint in marketData)
            {
                try
                {
                    // Save the market data using the provided storage.
                    await _storage.SaveMarketDataAsync(dataPoint);
                }
                catch (Exception ex)
                {
                    // Log the error and continue with the next data point.
                    _logger.LogError(ex, $"Error saving market data for {dataPoint.Key}");
                }
            }
        }
        catch (Exception ex)
        {
            // Log the error and rethrow the exception.
            _logger.LogError(ex, "Error processing market data");
            throw;
        }
    }
}
