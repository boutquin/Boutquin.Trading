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

using System.Text.Json;
using Boutquin.Domain.Helpers;
using Boutquin.Trading.Data.Tiingo.Responses;
using Boutquin.Trading.Domain.Data;
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Interfaces;
using Boutquin.Trading.Domain.ValueObjects;

namespace Boutquin.Trading.Data.Tiingo;

/// <summary>
/// Fetches historical equity market data from the Tiingo REST API.
/// Implements <see cref="IMarketDataFetcher"/> for the FetchMarketDataAsync contract.
/// FetchFxRatesAsync is not supported — use FrankfurterFetcher for FX.
/// </summary>
public sealed class TiingoFetcher : IMarketDataFetcher, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly string _apiEndpoint;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TiingoFetcher(
        string apiKey,
        HttpClient? httpClient = null,
        string apiEndpoint = "https://api.tiingo.com")
    {
        Guard.AgainstNullOrWhiteSpace(() => apiKey);
        Guard.AgainstNullOrWhiteSpace(() => apiEndpoint);

        _ownsClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient();
        _apiEndpoint = apiEndpoint.TrimEnd('/');

        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Token {apiKey}");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>> FetchMarketDataAsync(
        IEnumerable<Asset> symbols)
    {
        var symbolList = symbols?.ToList()
            ?? throw new ArgumentNullException(nameof(symbols));

        if (symbolList.Count == 0)
        {
            throw new ArgumentException("At least one symbol must be provided.", nameof(symbols));
        }

        foreach (var symbol in symbolList)
        {
            var url = $"{_apiEndpoint}/tiingo/daily/{Uri.EscapeDataString(symbol.Ticker)}/prices?resampleFreq=daily&format=json";

            TiingoDailyPrice[]? prices;
            try
            {
                var response = await _httpClient.GetAsync(url).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new MarketDataRetrievalException(
                        $"Tiingo API returned HTTP {(int)response.StatusCode} for symbol '{symbol.Ticker}'.");
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                prices = JsonSerializer.Deserialize<TiingoDailyPrice[]>(json, s_jsonOptions);
            }
            catch (MarketDataRetrievalException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                throw new MarketDataRetrievalException(
                    $"Failed to fetch market data from Tiingo for symbol '{symbol.Ticker}'.", ex);
            }
            catch (JsonException ex)
            {
                throw new MarketDataRetrievalException(
                    $"Failed to deserialize Tiingo response for symbol '{symbol.Ticker}'.", ex);
            }

            if (prices == null)
            {
                throw new MarketDataRetrievalException(
                    $"Tiingo returned null response for symbol '{symbol.Ticker}'.");
            }

            foreach (var price in prices)
            {
                var date = DateOnly.FromDateTime(price.Date.UtcDateTime);

                var marketData = new MarketData(
                    Timestamp: date,
                    Open: price.Open,
                    High: price.High,
                    Low: price.Low,
                    Close: price.Close,
                    AdjustedClose: price.AdjClose,
                    Volume: price.Volume,
                    DividendPerShare: price.DivCash,
                    SplitCoefficient: price.SplitFactor);

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
        IEnumerable<string> currencyPairs)
    {
        throw new NotSupportedException(
            "TiingoFetcher provides equity data only. Use FrankfurterFetcher for FX rates.");
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}
