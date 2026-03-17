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

using System.Runtime.CompilerServices;

using Boutquin.Trading.Domain.ValueObjects;

using CsvFileNameHelper = Boutquin.Trading.Data.CSV.MarketDataFileNameHelper;

namespace Boutquin.Trading.Application.Caching;

/// <summary>
/// L2 CSV write-through cache decorator for <see cref="IMarketDataFetcher"/>.
/// On L2 miss, fetches from API, writes to CSV, returns data.
/// On L2 hit (CSV exists), reads from CSV instead of API.
/// Per-symbol CSV existence check: only missing symbols hit the API.
/// </summary>
public sealed class WriteThroughMarketDataFetcher : IMarketDataFetcher, IDisposable
{
    private readonly IMarketDataFetcher _apiFetcher;
    private readonly CsvMarketDataFetcher _csvFetcher;
    private readonly CsvMarketDataStorage _csvStorage;
    private readonly string _dataDirectory;
    private readonly ILogger<WriteThroughMarketDataFetcher> _logger;

    public WriteThroughMarketDataFetcher(
        IMarketDataFetcher apiFetcher,
        string dataDirectory,
        ILogger<WriteThroughMarketDataFetcher> logger)
    {
        _apiFetcher = apiFetcher ?? throw new ArgumentNullException(nameof(apiFetcher));
        _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        _logger = logger ?? NullLogger<WriteThroughMarketDataFetcher>.Instance;
        _csvFetcher = new CsvMarketDataFetcher(dataDirectory);
        _csvStorage = new CsvMarketDataStorage(dataDirectory);
    }

    public WriteThroughMarketDataFetcher(IMarketDataFetcher apiFetcher, string dataDirectory)
        : this(apiFetcher, dataDirectory, NullLogger<WriteThroughMarketDataFetcher>.Instance)
    {
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>> FetchMarketDataAsync(
        IEnumerable<Asset> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();

        // Partition into cached (CSV exists) and uncached
        var cached = new List<Asset>();
        var uncached = new List<Asset>();

        foreach (var symbol in symbolList)
        {
            var csvPath = CsvFileNameHelper.GetCsvFileNameForMarketData(_dataDirectory, symbol.Ticker);
            if (File.Exists(csvPath))
            {
                cached.Add(symbol);
            }
            else
            {
                uncached.Add(symbol);
            }
        }

        // Fetch uncached from API and write through to CSV
        if (uncached.Count > 0)
        {
            _logger.LogDebug("L2 cache miss for {Count} symbols, fetching from API", uncached.Count);
            await foreach (var item in _apiFetcher.FetchMarketDataAsync(uncached, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Write through to CSV storage
                await _csvStorage.SaveMarketDataAsync(item, cancellationToken).ConfigureAwait(false);
            }
        }

        // Now read everything from CSV (both previously cached and newly written)
        await foreach (var item in _csvFetcher.FetchMarketDataAsync(symbolList, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> FetchFxRatesAsync(
        IEnumerable<string> currencyPairs,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pairList = currencyPairs.ToList();

        // Partition into cached and uncached
        var cached = new List<string>();
        var uncached = new List<string>();

        foreach (var pair in pairList)
        {
            var csvPath = CsvFileNameHelper.GetCsvFileNameForFxRateData(_dataDirectory, pair);
            if (File.Exists(csvPath))
            {
                cached.Add(pair);
            }
            else
            {
                uncached.Add(pair);
            }
        }

        // Fetch uncached FX rates from API and write through to CSV
        if (uncached.Count > 0)
        {
            _logger.LogDebug("L2 cache miss for {Count} FX pairs, fetching from API", uncached.Count);

            // Materialize API data per pair and write CSV
            var apiData = new List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>();
            await foreach (var item in _apiFetcher.FetchFxRatesAsync(uncached, cancellationToken).ConfigureAwait(false))
            {
                apiData.Add(item);
            }

            // Write FX CSVs — one file per pair (overwrite mode per spec)
            foreach (var pair in uncached)
            {
                var splitPair = pair.Split('_');
                if (splitPair.Length != 2 || !Enum.TryParse<CurrencyCode>(splitPair[1], out var quoteCurrency))
                {
                    continue;
                }

                var filePath = CsvFileNameHelper.GetCsvFileNameForFxRateData(_dataDirectory, pair);
                var tmpPath = filePath + ".tmp";

                try
                {
                    await using var fileStream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await using var writer = new StreamWriter(fileStream);
                    await writer.WriteLineAsync("Date,Rate").ConfigureAwait(false);

                    foreach (var kvp in apiData)
                    {
                        if (kvp.Value.TryGetValue(quoteCurrency, out var rate))
                        {
                            var line = FormattableString.Invariant($"{kvp.Key},{rate}");
                            await writer.WriteLineAsync(line).ConfigureAwait(false);
                        }
                    }
                }
                catch
                {
                    // Cleanup partial temp file on failure
                    if (File.Exists(tmpPath))
                    {
                        File.Delete(tmpPath);
                    }
                    throw;
                }

                // Atomic rename
                File.Move(tmpPath, filePath, overwrite: true);
            }
        }

        // Read all from CSV
        await foreach (var item in _csvFetcher.FetchFxRatesAsync(pairList, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    public void Dispose()
    {
        if (_apiFetcher is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
