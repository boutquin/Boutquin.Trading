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
using System.Text.Json;
using Boutquin.Trading.Data.Frankfurter.Responses;
using Boutquin.Trading.Domain.Data;
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Interfaces;
using Boutquin.Trading.Domain.ValueObjects;

namespace Boutquin.Trading.Data.Frankfurter;

/// <summary>
/// Fetches historical FX rates from the Frankfurter API (ECB-sourced, no API key required).
/// </summary>
public sealed class FrankfurterFetcher : IMarketDataFetcher, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly string _apiEndpoint;
    private readonly DateOnly _startDate;
    private readonly DateOnly? _endDate;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> s_supportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "AUD", "BGN", "BRL", "CAD", "CHF", "CNY", "CZK", "DKK", "EUR",
        "GBP", "HKD", "HUF", "IDR", "ILS", "INR", "ISK", "JPY", "KRW",
        "MXN", "MYR", "NOK", "NZD", "PHP", "PLN", "RON", "SEK", "SGD",
        "THB", "TRY", "USD", "ZAR"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FrankfurterFetcher"/> class.
    /// </summary>
    /// <param name="httpClient">Optional HTTP client for making requests.</param>
    /// <param name="apiEndpoint">Base URL for the Frankfurter API.</param>
    /// <param name="startDate">Optional start date for filtering historical data.</param>
    /// <param name="endDate">Optional end date for filtering historical data.</param>
    public FrankfurterFetcher(
        HttpClient? httpClient = null,
        string apiEndpoint = "https://api.frankfurter.dev",
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        _ownsClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient();
        _apiEndpoint = (apiEndpoint ?? throw new ArgumentNullException(nameof(apiEndpoint))).TrimEnd('/');
        _startDate = startDate ?? new DateOnly(1999, 1, 4);
        _endDate = endDate;

        if (_endDate.HasValue && _startDate > _endDate.Value)
        {
            throw new ArgumentException("startDate cannot be after endDate.");
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>> FetchMarketDataAsync(
        IEnumerable<Asset> symbols, CancellationToken cancellationToken)
    {
        throw new NotSupportedException(
            "FrankfurterFetcher provides FX rate data only. Use TiingoFetcher for equity data.");
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> FetchFxRatesAsync(
        IEnumerable<string> currencyPairs, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pairList = currencyPairs?.ToList()
            ?? throw new ArgumentNullException(nameof(currencyPairs));

        if (pairList.Count == 0)
        {
            throw new ArgumentException("At least one currency pair must be provided.", nameof(currencyPairs));
        }

        var parsedPairs = new List<(string BaseCurrency, string QuoteCurrency)>();
        foreach (var pair in pairList)
        {
            var parts = pair.Split('_', '/');
            if (parts.Length != 2)
            {
                throw new ArgumentException(
                    $"Invalid currency pair format: '{pair}'. Expected 'BASE_QUOTE' or 'BASE/QUOTE'.",
                    nameof(currencyPairs));
            }

            var baseCurrency = parts[0].Trim().ToUpperInvariant();
            var quoteCurrency = parts[1].Trim().ToUpperInvariant();

            if (!s_supportedCurrencies.Contains(baseCurrency))
            {
                throw new NotSupportedException(
                    $"Currency '{baseCurrency}' is not supported by ECB/Frankfurter." +
                    (baseCurrency == "RUB" ? " RUB was removed after 2022 EU sanctions." : ""));
            }

            if (!s_supportedCurrencies.Contains(quoteCurrency))
            {
                throw new NotSupportedException(
                    $"Currency '{quoteCurrency}' is not supported by ECB/Frankfurter." +
                    (quoteCurrency == "RUB" ? " RUB was removed after 2022 EU sanctions." : ""));
            }

            parsedPairs.Add((baseCurrency, quoteCurrency));
        }

        var grouped = parsedPairs
            .GroupBy(p => p.BaseCurrency, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => p.QuoteCurrency).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

        foreach (var (baseCurrency, quoteCurrencies) in grouped)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dateRange = _endDate.HasValue
                ? $"{_startDate:yyyy-MM-dd}..{_endDate.Value:yyyy-MM-dd}"
                : $"{_startDate:yyyy-MM-dd}..";
            var symbolsParam = Uri.EscapeDataString(string.Join(",", quoteCurrencies));
            var url = $"{_apiEndpoint}/v1/{dateRange}?base={Uri.EscapeDataString(baseCurrency)}&symbols={symbolsParam}";

            FrankfurterRangeResponse? rangeResponse;
            try
            {
                // R2I-05: Dispose HttpResponseMessage
                using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new MarketDataRetrievalException(
                        $"Frankfurter API returned HTTP {(int)response.StatusCode} for base '{baseCurrency}'.");
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                rangeResponse = JsonSerializer.Deserialize<FrankfurterRangeResponse>(json, s_jsonOptions);
            }
            catch (MarketDataRetrievalException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                throw new MarketDataRetrievalException(
                    $"Failed to fetch FX rates from Frankfurter for base '{baseCurrency}'.", ex);
            }
            catch (JsonException ex)
            {
                throw new MarketDataRetrievalException(
                    $"Failed to deserialize Frankfurter response for base '{baseCurrency}'.", ex);
            }

            if (rangeResponse?.Rates == null)
            {
                throw new MarketDataRetrievalException(
                    $"Frankfurter returned null response for base '{baseCurrency}'.");
            }

            foreach (var (dateString, currencyRates) in rangeResponse.Rates)
            {
                if (!DateOnly.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date))
                {
                    continue;
                }

                var ratesDict = new SortedDictionary<CurrencyCode, decimal>();

                foreach (var (currencyString, rate) in currencyRates)
                {
                    if (Enum.TryParse<CurrencyCode>(currencyString, ignoreCase: true, out var currencyCode))
                    {
                        ratesDict[currencyCode] = rate;
                    }
                }

                if (ratesDict.Count > 0)
                {
                    yield return new KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>(date, ratesDict);
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}
