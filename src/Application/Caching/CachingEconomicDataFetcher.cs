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
/// L1 in-process memory cache decorator for <see cref="IEconomicDataFetcher"/>.
/// Supports superset filtering: if a cached entry covers a wider date range than requested,
/// the result is filtered in-memory instead of re-fetching.
/// </summary>
public sealed class CachingEconomicDataFetcher : IEconomicDataFetcher, IDisposable
{
    private readonly IEconomicDataFetcher _inner;
    private readonly ConcurrentDictionary<string, Lazy<Task<List<KeyValuePair<DateOnly, decimal>>>>> _cache = new();
    private readonly ILogger<CachingEconomicDataFetcher> _logger;

    /// <summary>Initializes a new instance with the specified inner fetcher and logger.</summary>
    /// <param name="inner">The underlying economic data fetcher.</param>
    /// <param name="logger">The logger instance.</param>
    public CachingEconomicDataFetcher(IEconomicDataFetcher inner, ILogger<CachingEconomicDataFetcher> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? NullLogger<CachingEconomicDataFetcher>.Instance;
    }

    /// <summary>Initializes a new instance with the specified inner fetcher.</summary>
    /// <param name="inner">The underlying economic data fetcher.</param>
    public CachingEconomicDataFetcher(IEconomicDataFetcher inner)
        : this(inner, NullLogger<CachingEconomicDataFetcher>.Instance)
    {
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, decimal>> FetchSeriesAsync(
        string seriesId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Check for superset: if we have the full range cached, filter in-memory
        var supersetData = TryGetSuperset(seriesId, startDate, endDate);
        if (supersetData != null)
        {
            _logger.LogDebug("L1 cache superset hit for {SeriesId}, filtering in-memory", seriesId);
            foreach (var item in FilterByDateRange(supersetData, startDate, endDate))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
            yield break;
        }

        var key = BuildKey(seriesId, startDate, endDate);
        var lazy = _cache.GetOrAdd(key, _ => new Lazy<Task<List<KeyValuePair<DateOnly, decimal>>>>(
            () => MaterializeAsync(seriesId, startDate, endDate, CancellationToken.None)));

        List<KeyValuePair<DateOnly, decimal>> data;
        try
        {
            data = await lazy.Value.ConfigureAwait(false);
        }
        catch
        {
            // Evict the faulted entry so the next caller retries; only remove our exact Lazy instance
            _cache.TryRemove(KeyValuePair.Create(key, lazy));
            throw;
        }

        foreach (var item in data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cache.Clear();

        if (_inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private List<KeyValuePair<DateOnly, decimal>>? TryGetSuperset(string seriesId, DateOnly? startDate, DateOnly? endDate)
    {
        // The broadest possible key is seriesId|*|*
        var fullRangeKey = BuildKey(seriesId, null, null);
        if (_cache.TryGetValue(fullRangeKey, out var lazyData) && lazyData.IsValueCreated && lazyData.Value.IsCompletedSuccessfully)
        {
            // We have the full range — filter is safe
            if (startDate != null || endDate != null)
            {
                return lazyData.Value.Result;
            }
        }
        return null;
    }

    private static IEnumerable<KeyValuePair<DateOnly, decimal>> FilterByDateRange(
        List<KeyValuePair<DateOnly, decimal>> data, DateOnly? startDate, DateOnly? endDate)
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

    private async Task<List<KeyValuePair<DateOnly, decimal>>> MaterializeAsync(
        string seriesId, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken)
    {
        _logger.LogDebug("L1 cache miss for {SeriesId}, materializing from inner fetcher", seriesId);
        var result = new List<KeyValuePair<DateOnly, decimal>>();
        await foreach (var item in _inner.FetchSeriesAsync(seriesId, startDate, endDate, cancellationToken).ConfigureAwait(false))
        {
            result.Add(item);
        }
        return result;
    }

    private static string BuildKey(string seriesId, DateOnly? startDate, DateOnly? endDate) =>
        $"{seriesId}|{(startDate.HasValue ? startDate.Value.ToString("O") : "*")}|{(endDate.HasValue ? endDate.Value.ToString("O") : "*")}";
}
