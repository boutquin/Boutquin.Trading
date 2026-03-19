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

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Boutquin.Domain.Helpers;
using Boutquin.Trading.Data.TwelveData.Responses;
using Boutquin.Trading.Domain.Data;
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Interfaces;
using Boutquin.Trading.Domain.ValueObjects;

namespace Boutquin.Trading.Data.TwelveData;

/// <summary>
/// Fetches historical equity market data from the Twelve Data REST API.
/// Implements <see cref="IMarketDataFetcher"/> for the FetchMarketDataAsync contract.
/// Combines time_series, dividends, and splits endpoints to produce full MarketData records.
/// FetchFxRatesAsync is not supported — use FrankfurterFetcher for FX.
/// </summary>
public sealed class TwelveDataFetcher : IMarketDataFetcher, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly string _apiKey;
    private readonly string _apiEndpoint;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="TwelveDataFetcher"/> class.
    /// </summary>
    /// <param name="apiKey">The Twelve Data API key.</param>
    /// <param name="httpClient">Optional HttpClient instance. If null, one will be created and owned by this fetcher.</param>
    /// <param name="apiEndpoint">Optional API base URL. Defaults to the Twelve Data production endpoint.</param>
    public TwelveDataFetcher(
        string apiKey,
        HttpClient? httpClient = null,
        string apiEndpoint = "https://api.twelvedata.com")
    {
        Guard.AgainstNullOrWhiteSpace(() => apiKey);
        Guard.AgainstNullOrWhiteSpace(() => apiEndpoint);

        _apiKey = apiKey;
        _ownsClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient();
        _apiEndpoint = apiEndpoint.TrimEnd('/');
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>> FetchMarketDataAsync(
        IEnumerable<Asset> symbols,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var symbolList = symbols?.ToList()
            ?? throw new ArgumentNullException(nameof(symbols));

        if (symbolList.Count == 0)
        {
            throw new ArgumentException("At least one symbol must be provided.", nameof(symbols));
        }

        foreach (var symbol in symbolList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ticker = Uri.EscapeDataString(symbol.Ticker);

            // Fetch time series (unadjusted close so we can compute adjusted ourselves),
            // dividends, and splits for each symbol.
            var timeSeriesTask = FetchTimeSeriesAsync(ticker, cancellationToken);
            var dividendsTask = FetchDividendsAsync(ticker, cancellationToken);
            var splitsTask = FetchSplitsAsync(ticker, cancellationToken);

            await Task.WhenAll(timeSeriesTask, dividendsTask, splitsTask).ConfigureAwait(false);

            var timeSeries = await timeSeriesTask.ConfigureAwait(false);
            var dividendsByDate = await dividendsTask.ConfigureAwait(false);
            var splitsByDate = await splitsTask.ConfigureAwait(false);

            foreach (var entry in timeSeries)
            {
                var date = entry.Key;
                var (open, high, low, close, volume) = entry.Value;

                dividendsByDate.TryGetValue(date, out var dividend);
                splitsByDate.TryGetValue(date, out var splitCoefficient);
                if (splitCoefficient == 0m)
                {
                    splitCoefficient = 1.0m;
                }

                // Twelve Data time_series close is split-adjusted by default.
                // Use it as the adjusted close.
                var marketData = new MarketData(
                    Timestamp: date,
                    Open: open,
                    High: high,
                    Low: low,
                    Close: close,
                    AdjustedClose: close,
                    Volume: volume,
                    DividendPerShare: dividend,
                    SplitCoefficient: splitCoefficient);

                var assetDict = new SortedDictionary<Asset, MarketData>
                {
                    [symbol] = marketData
                };

                yield return new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(date, assetDict);
            }
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> FetchFxRatesAsync(
        IEnumerable<string> currencyPairs,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "TwelveDataFetcher provides equity data only. Use FrankfurterFetcher for FX rates.");
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    /// <summary>
    /// Fetches daily OHLCV time series for a single ticker.
    /// Returns a sorted dictionary keyed by date.
    /// </summary>
    private async Task<SortedDictionary<DateOnly, (decimal Open, decimal High, decimal Low, decimal Close, long Volume)>>
        FetchTimeSeriesAsync(string escapedTicker, CancellationToken cancellationToken)
    {
        var url = $"{_apiEndpoint}/time_series?symbol={escapedTicker}&interval=1day&outputsize=5000&apikey={_apiKey}";

        TwelveDataTimeSeriesResponse? response;
        try
        {
            var httpResponse = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new MarketDataRetrievalException(
                    $"Twelve Data API returned HTTP {(int)httpResponse.StatusCode} for symbol '{escapedTicker}'.");
            }

            var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            response = JsonSerializer.Deserialize<TwelveDataTimeSeriesResponse>(json, s_jsonOptions);
        }
        catch (MarketDataRetrievalException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new MarketDataRetrievalException(
                $"Failed to fetch market data from Twelve Data for symbol '{escapedTicker}'.", ex);
        }
        catch (JsonException ex)
        {
            throw new MarketDataRetrievalException(
                $"Failed to deserialize Twelve Data time series response for symbol '{escapedTicker}'.", ex);
        }

        if (response?.Values == null)
        {
            var errorMsg = response?.Message ?? "null response";
            throw new MarketDataRetrievalException(
                $"Twelve Data returned no time series data for symbol '{escapedTicker}': {errorMsg}");
        }

        var result = new SortedDictionary<DateOnly, (decimal Open, decimal High, decimal Low, decimal Close, long Volume)>();

        foreach (var value in response.Values)
        {
            if (!DateOnly.TryParseExact(value.Datetime, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            if (!decimal.TryParse(value.Open, CultureInfo.InvariantCulture, out var open) ||
                !decimal.TryParse(value.High, CultureInfo.InvariantCulture, out var high) ||
                !decimal.TryParse(value.Low, CultureInfo.InvariantCulture, out var low) ||
                !decimal.TryParse(value.Close, CultureInfo.InvariantCulture, out var close))
            {
                continue;
            }

            _ = long.TryParse(value.Volume, CultureInfo.InvariantCulture, out var volume);

            result[date] = (open, high, low, close, volume);
        }

        return result;
    }

    /// <summary>
    /// Fetches dividend history for a single ticker.
    /// Returns a dictionary of ex-date to dividend amount.
    /// </summary>
    private async Task<Dictionary<DateOnly, decimal>> FetchDividendsAsync(string escapedTicker, CancellationToken cancellationToken)
    {
        var url = $"{_apiEndpoint}/dividends?symbol={escapedTicker}&apikey={_apiKey}";

        TwelveDataDividendsResponse? response;
        try
        {
            var httpResponse = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                // Dividends are supplementary; return empty on failure rather than
                // blocking the entire fetch.
                return new Dictionary<DateOnly, decimal>();
            }

            var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            response = JsonSerializer.Deserialize<TwelveDataDividendsResponse>(json, s_jsonOptions);
        }
        catch (Exception) when (IsNonCriticalException())
        {
            return new Dictionary<DateOnly, decimal>();
        }

        var result = new Dictionary<DateOnly, decimal>();

        if (response?.Dividends == null)
        {
            return result;
        }

        foreach (var dividend in response.Dividends)
        {
            if (DateOnly.TryParseExact(dividend.ExDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                result[date] = dividend.Amount;
            }
        }

        return result;

        // Local function to filter non-critical exceptions (HTTP and JSON errors).
        static bool IsNonCriticalException() => true;
    }

    /// <summary>
    /// Fetches split history for a single ticker.
    /// Returns a dictionary of split date to split coefficient (to_factor / from_factor).
    /// </summary>
    private async Task<Dictionary<DateOnly, decimal>> FetchSplitsAsync(string escapedTicker, CancellationToken cancellationToken)
    {
        var url = $"{_apiEndpoint}/splits?symbol={escapedTicker}&apikey={_apiKey}";

        TwelveDataSplitsResponse? response;
        try
        {
            var httpResponse = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                return new Dictionary<DateOnly, decimal>();
            }

            var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            response = JsonSerializer.Deserialize<TwelveDataSplitsResponse>(json, s_jsonOptions);
        }
        catch (Exception) when (IsNonCriticalException())
        {
            return new Dictionary<DateOnly, decimal>();
        }

        var result = new Dictionary<DateOnly, decimal>();

        if (response?.Splits == null)
        {
            return result;
        }

        foreach (var split in response.Splits)
        {
            if (!DateOnly.TryParseExact(split.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            // Prefer from_factor/to_factor fields. Fall back to parsing the ratio string (e.g., "4:1").
            decimal coefficient;
            if (split.FromFactor.HasValue && split.ToFactor.HasValue && split.FromFactor.Value != 0)
            {
                coefficient = split.ToFactor.Value / split.FromFactor.Value;
            }
            else if (!string.IsNullOrWhiteSpace(split.Ratio) && TryParseSplitRatio(split.Ratio, out coefficient))
            {
                // coefficient assigned by TryParseSplitRatio
            }
            else
            {
                coefficient = 1.0m;
            }

            result[date] = coefficient;
        }

        return result;

        static bool IsNonCriticalException() => true;
    }

    /// <summary>
    /// Parses a split ratio string like "4:1" into a decimal coefficient (4.0).
    /// </summary>
    private static bool TryParseSplitRatio(string ratio, out decimal coefficient)
    {
        coefficient = 1.0m;
        var parts = ratio.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!decimal.TryParse(parts[0].Trim(), CultureInfo.InvariantCulture, out var numerator) ||
            !decimal.TryParse(parts[1].Trim(), CultureInfo.InvariantCulture, out var denominator))
        {
            return false;
        }

        if (denominator == 0)
        {
            return false;
        }

        coefficient = numerator / denominator;
        return true;
    }
}
