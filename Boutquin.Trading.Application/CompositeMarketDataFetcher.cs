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

namespace Boutquin.Trading.Application;

using Domain.ValueObjects;

/// <summary>
/// Combines an equity data fetcher and an FX rate fetcher into a single
/// <see cref="IMarketDataFetcher"/> implementation for use by <see cref="Backtest"/>.
/// </summary>
public sealed class CompositeMarketDataFetcher : IMarketDataFetcher, IDisposable
{
    private readonly IMarketDataFetcher _equityFetcher;
    private readonly IMarketDataFetcher _fxFetcher;

    public CompositeMarketDataFetcher(
        IMarketDataFetcher equityFetcher,
        IMarketDataFetcher fxFetcher)
    {
        _equityFetcher = equityFetcher ?? throw new ArgumentNullException(nameof(equityFetcher));
        _fxFetcher = fxFetcher ?? throw new ArgumentNullException(nameof(fxFetcher));
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>> FetchMarketDataAsync(
        IEnumerable<Asset> symbols, CancellationToken cancellationToken) =>
        _equityFetcher.FetchMarketDataAsync(symbols, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> FetchFxRatesAsync(
        IEnumerable<string> currencyPairs, CancellationToken cancellationToken) =>
        _fxFetcher.FetchFxRatesAsync(currencyPairs, cancellationToken);

    public void Dispose()
    {
        (_equityFetcher as IDisposable)?.Dispose();
        (_fxFetcher as IDisposable)?.Dispose();
    }
}
