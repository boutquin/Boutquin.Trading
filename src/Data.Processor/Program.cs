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

namespace Boutquin.Trading.Data.Processor;

internal sealed class Program
{
    private static async Task Main(string[] _)
    {
        // Retrieve the list of symbols from the Symbols file
        var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dataDir))
        {
            throw new DirectoryNotFoundException($"Data directory not found at '{dataDir}'. Ensure the Data folder is copied to the output directory.");
        }
        var filename = Path.Combine(dataDir, "Symbols.csv");
        var symbolReader = new CsvSymbolReader(filename);
        var assets = await symbolReader.ReadSymbolsAsync(CancellationToken.None).ConfigureAwait(false);

        // Instantiate the composite market data fetcher (Tiingo for equities, Frankfurter for FX)
        var tiingoApiKey = Environment.GetEnvironmentVariable("TIINGO_API_KEY")
            ?? throw new InvalidOperationException("TIINGO_API_KEY environment variable is not set.");
        using var equityFetcher = new TiingoFetcher(tiingoApiKey);
        using var fxFetcher = new FrankfurterFetcher();
        using var apiFetcher = new CompositeMarketDataFetcher(equityFetcher, fxFetcher);
        var writer = new CsvMarketDataStorage(dataDir);
        var marketDataProcessor = new MarketDataProcessor(apiFetcher, writer);

        try
        {
            await marketDataProcessor.ProcessAndStoreMarketDataAsync(assets, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("An error occurred while processing market data:");
            Console.Error.WriteLine(ex.ToString());
            return;
        }

        var fetcher = new CsvMarketDataFetcher(dataDir);

        try
        {
            await foreach (var dataPoint in fetcher.FetchMarketDataAsync(assets, CancellationToken.None).ConfigureAwait(false))
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
            Console.Error.WriteLine("An error occurred while reading market data:");
            Console.Error.WriteLine(ex.ToString());
        }
    }
}
