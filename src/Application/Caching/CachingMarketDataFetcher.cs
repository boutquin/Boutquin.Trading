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

using Boutquin.Trading.Domain.ValueObjects;

namespace Boutquin.Trading.Application.Caching;

/// <summary>
/// L1 in-process memory cache decorator for <see cref="IMarketDataFetcher"/>.
/// Eliminates redundant fetches within a single session/backtest run.
/// Thread-safe via ConcurrentDictionary + Lazy&lt;Task&gt; pattern for exactly-once materialization.
/// </summary>
public sealed class CachingMarketDataFetcher : IMarketDataFetcher, IDisposable
{
    private readonly IMarketDataFetcher _inner;
    private readonly ConcurrentDictionary<string, Lazy<Task<List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>>>> _marketDataCache = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>>>> _fxCache = new();
    private readonly ILogger<CachingMarketDataFetcher> _logger;

    public CachingMarketDataFetcher(IMarketDataFetcher inner, ILogger<CachingMarketDataFetcher> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? NullLogger<CachingMarketDataFetcher>.Instance;
    }

    public CachingMarketDataFetcher(IMarketDataFetcher inner)
        : this(inner, NullLogger<CachingMarketDataFetcher>.Instance)
    {
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>> FetchMarketDataAsync(
        IEnumerable<Asset> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var key = BuildMarketDataKey(symbols);
        var lazy = _marketDataCache.GetOrAdd(key, _ => new Lazy<Task<List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>>>(
            () => MaterializeMarketDataAsync(symbols, cancellationToken)));

        var data = await lazy.Value.ConfigureAwait(false);

        foreach (var item in data)
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
        var key = BuildFxKey(currencyPairs);
        var lazy = _fxCache.GetOrAdd(key, _ => new Lazy<Task<List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>>>(
            () => MaterializeFxRatesAsync(currencyPairs, cancellationToken)));

        var data = await lazy.Value.ConfigureAwait(false);

        foreach (var item in data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    public void Dispose()
    {
        _marketDataCache.Clear();
        _fxCache.Clear();

        if (_inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task<List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>> MaterializeMarketDataAsync(
        IEnumerable<Asset> symbols, CancellationToken cancellationToken)
    {
        _logger.LogDebug("L1 cache miss for market data, materializing from inner fetcher");
        var result = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in _inner.FetchMarketDataAsync(symbols, cancellationToken).ConfigureAwait(false))
        {
            result.Add(item);
        }
        return result;
    }

    private async Task<List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>> MaterializeFxRatesAsync(
        IEnumerable<string> currencyPairs, CancellationToken cancellationToken)
    {
        _logger.LogDebug("L1 cache miss for FX rates, materializing from inner fetcher");
        var result = new List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>();
        await foreach (var item in _inner.FetchFxRatesAsync(currencyPairs, cancellationToken).ConfigureAwait(false))
        {
            result.Add(item);
        }
        return result;
    }

    private static string BuildMarketDataKey(IEnumerable<Asset> symbols) =>
        string.Join("|", symbols.Select(s => s.Ticker).OrderBy(t => t, StringComparer.Ordinal));

    private static string BuildFxKey(IEnumerable<string> currencyPairs) =>
        string.Join("|", currencyPairs.OrderBy(p => p, StringComparer.Ordinal));
}
