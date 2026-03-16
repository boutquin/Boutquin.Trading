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

namespace Boutquin.Trading.Data.Polygon;

using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;

/// <summary>
/// BUG-I09 fix: Proper response DTO for Polygon.io FX conversion endpoint.
/// </summary>
internal sealed record PolygonFxConversionResponse(
    [property: JsonPropertyName("converted")] decimal Converted,
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("initialAmount")] decimal InitialAmount,
    [property: JsonPropertyName("last")] PolygonFxLastQuote Last
);

/// <summary>
/// Nested quote from the Polygon FX conversion response.
/// </summary>
internal sealed record PolygonFxLastQuote(
    [property: JsonPropertyName("ask")] decimal Ask,
    [property: JsonPropertyName("bid")] decimal Bid,
    [property: JsonPropertyName("exchange")] int Exchange,
    [property: JsonPropertyName("timestamp")] long Timestamp
);

/// <summary>
/// Fetches market data from Polygon.io API.
/// </summary>
// TYP-I02 fix: Seal the class
public sealed class PolygonFetcher : IMarketDataFetcher, IDisposable
{
    // PERF-I02 fix: Reuse JsonSerializerOptions
    private static readonly JsonSerializerOptions s_marketDataJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new DateOnlyConverter() }
    };

    private static readonly JsonSerializerOptions s_fxJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly IDistributedCache _cache;
    private readonly string _apiKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolygonFetcher"/> class.
    /// </summary>
    public PolygonFetcher(
        IDistributedCache cache,
        HttpClient httpClient = null)
    {
        _apiKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY")
            ?? throw new ArgumentNullException(nameof(_apiKey), "API key cannot be null or empty.");
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new ArgumentNullException(nameof(_apiKey), "API key cannot be null or empty.");
        }

        _cache = cache ?? throw new ArgumentNullException(nameof(cache), "IDistributedCache cannot be null.");
        _ownsClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<Domain.ValueObjects.Asset, MarketData>>> FetchMarketDataAsync(IEnumerable<Domain.ValueObjects.Asset> symbols)
    {
        var date = GetLastCloseDate();
        foreach (var symbol in symbols)
        {
            SortedDictionary<Domain.ValueObjects.Asset, MarketData> result = null!;
            try
            {
                // SEC-I01 fix: Move API key to Authorization header
                var url = $"https://api.polygon.io/v1/open-close/{Uri.EscapeDataString(symbol.Ticker)}/{date:yyyy-MM-dd}?adjusted=true";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var marketData = JsonSerializer.Deserialize<MarketData>(responseBody, s_marketDataJsonOptions);

                if (marketData == null)
                {
                    throw new MarketDataProcessingException("Failed to deserialize the market data.");
                }

                result = new SortedDictionary<Domain.ValueObjects.Asset, MarketData> { [symbol] = marketData };
            }
            catch (HttpRequestException e)
            {
                throw new MarketDataRetrievalException("Error occurred while retrieving market data from Polygon.io.", e);
            }
            catch (JsonException e)
            {
                throw new MarketDataProcessingException("Error occurred while processing market data from Polygon.io.", e);
            }
            catch (Exception e)
            {
                throw new MarketDataStorageException("Unexpected error occurred while storing market data from Polygon.io.", e);
            }

            yield return new KeyValuePair<DateOnly, SortedDictionary<Domain.ValueObjects.Asset, MarketData>>(date, result);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> FetchFxRatesAsync(IEnumerable<string> currencyPairs)
    {
        var date = GetLastCloseDate();
        foreach (var pair in currencyPairs)
        {
            SortedDictionary<CurrencyCode, decimal>? result = null;
            try
            {
                // SEC-I01 fix: Move API key to Authorization header
                var url = $"https://api.polygon.io/v1/conversion/{Uri.EscapeDataString(pair)}/{date:yyyy-MM-dd}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                // BUG-I09 fix: Deserialize to proper DTO
                var fxResponse = JsonSerializer.Deserialize<PolygonFxConversionResponse>(responseBody, s_fxJsonOptions);

                if (fxResponse == null || fxResponse.Converted == 0)
                {
                    throw new MarketDataProcessingException("Failed to deserialize the FX rate data.");
                }

                // BUG-I10 fix: Split pair and parse only quote currency
                var pairParts = pair.Split('/');
                if (pairParts.Length != 2 || !Enum.TryParse<CurrencyCode>(pairParts[1], out var quoteCurrency))
                {
                    throw new ArgumentException($"Invalid currency pair format: '{pair}'. Expected format: 'BASE/QUOTE'.");
                }

                result = new SortedDictionary<CurrencyCode, decimal> { [quoteCurrency] = fxResponse.Converted };
            }
            catch (HttpRequestException e)
            {
                throw new MarketDataRetrievalException("Error occurred while retrieving FX rate data from Polygon.io.", e);
            }
            catch (JsonException e)
            {
                throw new MarketDataProcessingException("Error occurred while processing FX rate data from Polygon.io.", e);
            }
            catch (Exception e)
            {
                throw new MarketDataStorageException("Unexpected error occurred while storing FX rate data from Polygon.io.", e);
            }

            yield return new KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>(date, result);
        }
    }

    private static DateOnly GetLastCloseDate()
    {
        var now = DateTime.Now;
        var dayOfWeek = now.DayOfWeek;
        var timeOfDay = now.TimeOfDay;

        if (dayOfWeek == DayOfWeek.Saturday)
        {
            return DateOnly.FromDateTime(now.AddDays(-1));
        }
        else if (dayOfWeek == DayOfWeek.Sunday)
        {
            return DateOnly.FromDateTime(now.AddDays(-2));
        }
        // BUG-I11 fix: Use 17:00 instead of 23:00
        else if (dayOfWeek == DayOfWeek.Monday && timeOfDay <= new TimeSpan(17, 0, 0))
        {
            return DateOnly.FromDateTime(now.AddDays(-3));
        }
        else if (timeOfDay <= new TimeSpan(17, 0, 0))
        {
            return DateOnly.FromDateTime(now.AddDays(-1));
        }
        return DateOnly.FromDateTime(now);
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}
