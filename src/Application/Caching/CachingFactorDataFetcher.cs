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

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Boutquin.Trading.Application.Caching;

/// <summary>
/// L1 in-process memory cache decorator for <see cref="IFactorDataFetcher"/>.
/// Maintains separate caches for daily and monthly data.
/// Supports superset filtering for date range subsets.
/// </summary>
public sealed class CachingFactorDataFetcher : IFactorDataFetcher, IDisposable
{
    private readonly IFactorDataFetcher _inner;
    private readonly ConcurrentDictionary<string, Lazy<Task<List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>>>> _dailyCache = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>>>> _monthlyCache = new();
    private readonly ILogger<CachingFactorDataFetcher> _logger;

    /// <summary>Initializes a new instance with the specified inner fetcher and logger.</summary>
    /// <param name="inner">The underlying factor data fetcher.</param>
    /// <param name="logger">The logger instance.</param>
    public CachingFactorDataFetcher(IFactorDataFetcher inner, ILogger<CachingFactorDataFetcher> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? NullLogger<CachingFactorDataFetcher>.Instance;
    }

    /// <summary>Initializes a new instance with the specified inner fetcher.</summary>
    /// <param name="inner">The underlying factor data fetcher.</param>
    public CachingFactorDataFetcher(IFactorDataFetcher inner)
        : this(inner, NullLogger<CachingFactorDataFetcher>.Instance)
    {
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> FetchDailyAsync(
        FamaFrenchDataset dataset,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in FetchFromCacheAsync(_dailyCache, dataset, startDate, endDate,
            _inner.FetchDailyAsync, "daily", cancellationToken).ConfigureAwait(false))
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
        await foreach (var item in FetchFromCacheAsync(_monthlyCache, dataset, startDate, endDate,
            _inner.FetchMonthlyAsync, "monthly", cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _dailyCache.Clear();
        _monthlyCache.Clear();

        if (_inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> FetchFromCacheAsync(
        ConcurrentDictionary<string, Lazy<Task<List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>>>> cache,
        FamaFrenchDataset dataset,
        DateOnly? startDate,
        DateOnly? endDate,
        Func<FamaFrenchDataset, DateOnly?, DateOnly?, CancellationToken, IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>> fetchFunc,
        string frequency,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Check for superset
        var supersetData = TryGetSuperset(cache, dataset, startDate, endDate);
        if (supersetData != null)
        {
            _logger.LogDebug("L1 cache superset hit for {Dataset} {Frequency}, filtering in-memory", dataset, frequency);
            foreach (var item in FilterByDateRange(supersetData, startDate, endDate))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
            yield break;
        }

        var key = BuildKey(dataset, startDate, endDate);
        var lazy = cache.GetOrAdd(key, _ => new Lazy<Task<List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>>>(
            () => MaterializeAsync(fetchFunc, dataset, startDate, endDate, CancellationToken.None)));

        List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> data;
        try
        {
            data = await lazy.Value.ConfigureAwait(false);
        }
        catch
        {
            // Evict the faulted entry so the next caller retries; only remove our exact Lazy instance
            cache.TryRemove(KeyValuePair.Create(key, lazy));
            throw;
        }

        foreach (var item in data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    private static List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>? TryGetSuperset(
        ConcurrentDictionary<string, Lazy<Task<List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>>>> cache,
        FamaFrenchDataset dataset,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        var fullRangeKey = BuildKey(dataset, null, null);
        if (cache.TryGetValue(fullRangeKey, out var lazyData) && lazyData.IsValueCreated && lazyData.Value.IsCompletedSuccessfully)
        {
            if (startDate != null || endDate != null)
            {
                return lazyData.Value.Result;
            }
        }
        return null;
    }

    private static IEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> FilterByDateRange(
        List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> data, DateOnly? startDate, DateOnly? endDate)
    {
        foreach (var item in data)
        {
            if (startDate.HasValue && item.Key < startDate.Value)
            {
                continue;
            }
            if (endDate.HasValue && item.Key > endDate.Value)
            {
                continue;
            }
            yield return item;
        }
    }

    private static async Task<List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>> MaterializeAsync(
        Func<FamaFrenchDataset, DateOnly?, DateOnly?, CancellationToken, IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>> fetchFunc,
        FamaFrenchDataset dataset,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken)
    {
        var result = new List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>();
        await foreach (var item in fetchFunc(dataset, startDate, endDate, cancellationToken).ConfigureAwait(false))
        {
            result.Add(item);
        }
        return result;
    }

    private static string BuildKey(FamaFrenchDataset dataset, DateOnly? startDate, DateOnly? endDate) =>
        $"{dataset}|{(startDate.HasValue ? startDate.Value.ToString("O") : "*")}|{(endDate.HasValue ? endDate.Value.ToString("O") : "*")}";
}
