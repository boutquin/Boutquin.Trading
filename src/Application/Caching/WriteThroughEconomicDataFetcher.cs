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

namespace Boutquin.Trading.Application.Caching;

/// <summary>
/// L2 CSV write-through cache decorator for <see cref="IEconomicDataFetcher"/>.
/// On L2 miss, fetches from API, writes to CSV, returns data.
/// On L2 hit (CSV exists), reads from CSV instead of API.
/// </summary>
public sealed class WriteThroughEconomicDataFetcher : IEconomicDataFetcher, IDisposable
{
    private readonly IEconomicDataFetcher _apiFetcher;
    private readonly CsvEconomicDataFetcher _csvFetcher;
    private readonly CsvEconomicDataStorage _csvStorage;
    private readonly ILogger<WriteThroughEconomicDataFetcher> _logger;

    public WriteThroughEconomicDataFetcher(
        IEconomicDataFetcher apiFetcher,
        string dataDirectory,
        ILogger<WriteThroughEconomicDataFetcher> logger)
    {
        _apiFetcher = apiFetcher ?? throw new ArgumentNullException(nameof(apiFetcher));
        ArgumentNullException.ThrowIfNull(dataDirectory);
        _logger = logger ?? NullLogger<WriteThroughEconomicDataFetcher>.Instance;
        _csvFetcher = new CsvEconomicDataFetcher(dataDirectory);
        _csvStorage = new CsvEconomicDataStorage(dataDirectory);
    }

    public WriteThroughEconomicDataFetcher(IEconomicDataFetcher apiFetcher, string dataDirectory)
        : this(apiFetcher, dataDirectory, NullLogger<WriteThroughEconomicDataFetcher>.Instance)
    {
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, decimal>> FetchSeriesAsync(
        string seriesId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var csvPath = _csvFetcher.GetCsvFileName(seriesId);

        if (!File.Exists(csvPath))
        {
            _logger.LogDebug("L2 cache miss for FRED series {SeriesId}, fetching from API", seriesId);

            // Materialize from API, write to CSV
            var apiData = new List<KeyValuePair<DateOnly, decimal>>();
            await foreach (var item in _apiFetcher.FetchSeriesAsync(seriesId, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                apiData.Add(item);
            }

            await _csvStorage.SaveSeriesAsync(seriesId, apiData.ToAsyncEnumerable(), cancellationToken).ConfigureAwait(false);
        }

        // Read from CSV (applying date filters)
        await foreach (var item in _csvFetcher.FetchSeriesAsync(seriesId, startDate, endDate, cancellationToken).ConfigureAwait(false))
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
