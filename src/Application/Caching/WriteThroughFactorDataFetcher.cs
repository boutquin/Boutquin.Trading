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
/// L2 CSV write-through cache decorator for <see cref="IFactorDataFetcher"/>.
/// On L2 miss, fetches from API, writes to CSV, returns data.
/// On L2 hit (CSV exists), reads from CSV instead of API.
/// </summary>
public sealed class WriteThroughFactorDataFetcher : IFactorDataFetcher, IDisposable
{
    private readonly IFactorDataFetcher _apiFetcher;
    private readonly CsvFactorDataFetcher _csvFetcher;
    private readonly CsvFactorDataStorage _csvStorage;
    private readonly ILogger<WriteThroughFactorDataFetcher> _logger;

    public WriteThroughFactorDataFetcher(
        IFactorDataFetcher apiFetcher,
        string dataDirectory,
        ILogger<WriteThroughFactorDataFetcher> logger)
    {
        _apiFetcher = apiFetcher ?? throw new ArgumentNullException(nameof(apiFetcher));
        ArgumentNullException.ThrowIfNull(dataDirectory);
        _logger = logger ?? NullLogger<WriteThroughFactorDataFetcher>.Instance;
        _csvFetcher = new CsvFactorDataFetcher(dataDirectory);
        _csvStorage = new CsvFactorDataStorage(dataDirectory);
    }

    public WriteThroughFactorDataFetcher(IFactorDataFetcher apiFetcher, string dataDirectory)
        : this(apiFetcher, dataDirectory, NullLogger<WriteThroughFactorDataFetcher>.Instance)
    {
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> FetchDailyAsync(
        FamaFrenchDataset dataset,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in FetchWithWriteThroughAsync(
            dataset, "daily", startDate, endDate,
            _apiFetcher.FetchDailyAsync,
            cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> FetchMonthlyAsync(
        FamaFrenchDataset dataset,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in FetchWithWriteThroughAsync(
            dataset, "monthly", startDate, endDate,
            _apiFetcher.FetchMonthlyAsync,
            cancellationToken).ConfigureAwait(false))
        {
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

    private async IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> FetchWithWriteThroughAsync(
        FamaFrenchDataset dataset,
        string frequency,
        DateOnly? startDate,
        DateOnly? endDate,
        Func<FamaFrenchDataset, DateOnly?, DateOnly?, CancellationToken, IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>> fetchFunc,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var csvPath = _csvFetcher.GetCsvFileName(dataset, frequency);

        if (!File.Exists(csvPath))
        {
            _logger.LogDebug("L2 cache miss for FF {Dataset} {Frequency}, fetching from API", dataset, frequency);

            // Materialize from API, write to CSV
            var apiData = new List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>();
            await foreach (var item in fetchFunc(dataset, null, null, cancellationToken).ConfigureAwait(false))
            {
                apiData.Add(item);
            }

            await _csvStorage.SaveFactorsAsync(dataset, frequency, apiData.ToAsyncEnumerable(), cancellationToken).ConfigureAwait(false);
        }

        // Read from CSV (applying date filters)
        IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> csvStream = frequency == "daily"
            ? _csvFetcher.FetchDailyAsync(dataset, startDate, endDate, cancellationToken)
            : _csvFetcher.FetchMonthlyAsync(dataset, startDate, endDate, cancellationToken);

        await foreach (var item in csvStream.ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }
}
