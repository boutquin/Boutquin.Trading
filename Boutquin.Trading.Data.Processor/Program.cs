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

using Boutquin.Trading.Data.AlphaVantage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Boutquin.Trading.Data.Processor;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var assets = new List<string> { "AAPL", "GOOG" };

        // Retrieve the API key from the environment
        var apiKey = Environment.GetEnvironmentVariable("ALPHA_VANTAGE_API_KEY");

        // Create a distributed cache instance (e.g., MemoryDistributedCache)
        var cache = new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

        // Instantiate AlphaVantageFetcher with the required parameters
        var fetcher = new AlphaVantageFetcher(apiKey, cache);

        try
        {
            await foreach (var dataPoint in fetcher.FetchMarketDataAsync(assets))
            {
                var date = dataPoint.Key;
                var marketData = dataPoint.Value;

                Console.WriteLine($"Date: {date}");
                foreach (var assetMarketData in marketData)
                {
                    var assetSymbol = assetMarketData.Key;
                    var assetData = assetMarketData.Value;

                    Console.WriteLine($"{assetSymbol} - Open: {assetData.Open}, High: {assetData.High}, Low: {assetData.Low}, Close: {assetData.Close}, AdjustedClose: {assetData.AdjustedClose}, Volume: {assetData.Volume}, DividendPerShare: {assetData.DividendPerShare}, SplitCoefficient: {assetData.SplitCoefficient}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}
