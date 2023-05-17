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
namespace Boutquin.Trading.Data.Processor;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using AlphaVantage;

using Boutquin.Trading.Domain.Data;
using Domain.Helpers;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Retrieve the list of symbols from the Symbols file
        var dataDir = Path.Combine(new DirectoryInfo("./../../../.").FullName, "Data");
        var filename = Path.Combine(dataDir, "Symbols.csv");
        var symbolReader = new CsvSymbolReader(filename);
        var assets = await symbolReader.ReadSymbolsAsync();

        // Retrieve the API key from the environment
        var apiKey = Environment.GetEnvironmentVariable("ALPHA_VANTAGE_API_KEY");

        // Create a distributed cache instance (e.g., MemoryDistributedCache)
        var cache = new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

        // Instantiate AlphaVantageFetcher with the required parameters
        var apiFetcher = new AlphaVantageFetcher(apiKey, cache);
        var writer = new CsvMarketDataStorage(dataDir);
        var marketDataProcessor = new MarketDataProcessor(apiFetcher, writer);

        //await marketDataProcessor.ProcessAndStoreMarketDataAsync(assets);

        var fetcher = new CsvMarketDataFetcher(dataDir);

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
