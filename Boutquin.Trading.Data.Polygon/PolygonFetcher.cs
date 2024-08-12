// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
namespace Boutquin.Trading.Data.Polygon;

using Microsoft.Extensions.Caching.Distributed;

/// <summary>
/// Fetches market data from Polygon.io API.
/// </summary>
public class PolygonFetcher : IMarketDataFetcher
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly string _apiKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolygonFetcher"/> class.
    /// </summary>
    /// <param name="cache">The IDistributedCache instance for caching the API responses.</param>
    /// <param name="httpClient">An optional HttpClient instance to use for making API requests. If not provided, a new instance will be created.</param>
    public PolygonFetcher(
        IDistributedCache cache,
        HttpClient httpClient = null)
    {
        _apiKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY");
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new ArgumentNullException(nameof(_apiKey), "API key cannot be null or empty.");
        }

        _cache = cache ?? throw new ArgumentNullException(nameof(cache), "IDistributedCache cannot be null.");
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Fetches historical market data for the specified financial assets and
    /// returns an asynchronous stream of key-value pairs, where the key is a DateOnly object
    /// representing the date and the value is a sorted dictionary of asset symbols and their
    /// corresponding MarketData objects.
    /// </summary>
    /// <param name="symbols">A list of financial asset symbols for which to fetch historical market data.</param>
    /// <returns>An IAsyncEnumerable of key-value pairs, where the key is a DateOnly object and the value is a SortedDictionary of string asset symbols and MarketData values.</returns>
    /// <exception cref="MarketDataRetrievalException">Thrown when there is an error in fetching or parsing the market data.</exception>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<Domain.ValueObjects.Asset, MarketData>?>> FetchMarketDataAsync(IEnumerable<Domain.ValueObjects.Asset> symbols)
    {
        var date = GetLastCloseDate();
        foreach (var symbol in symbols)
        {
            SortedDictionary<Domain.ValueObjects.Asset, MarketData>? result = null;
            try
            {
                var url = $"https://api.polygon.io/v1/open-close/{symbol.Ticker}/{date:yyyy-MM-dd}?adjusted=true&apiKey={_apiKey}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var marketData = JsonSerializer.Deserialize<MarketData>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new DateOnlyConverter() }
                });

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

            yield return new KeyValuePair<DateOnly, SortedDictionary<Domain.ValueObjects.Asset, MarketData>?>(date, result);
        }
    }

    /// <summary>
    /// Fetches historical foreign exchange rates for the specified currency pairs and
    /// returns an asynchronous stream of key-value pairs, where the key is a DateOnly object
    /// representing the date and the value is a sorted dictionary of currency pair symbols and their
    /// corresponding exchange rates.
    /// </summary>
    /// <param name="currencyPairs">A list of currency pair symbols for which to fetch historical exchange rates.</param>
    /// <returns>An IAsyncEnumerable of key-value pairs, where the key is a DateOnly object and the value is a SortedDictionary of string currency pair symbols and decimal exchange rates.</returns>
    /// <exception cref="MarketDataRetrievalException">Thrown when there is an error in fetching or parsing the foreign exchange data.</exception>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> FetchFxRatesAsync(IEnumerable<string> currencyPairs)
    {
        var date = GetLastCloseDate();
        foreach (var pair in currencyPairs)
        {
            SortedDictionary<CurrencyCode, decimal>? result = null;
            try
            {
                var url = $"https://api.polygon.io/v1/conversion/{pair}/{date:yyyy-MM-dd}?apiKey={_apiKey}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var fxRate = JsonSerializer.Deserialize<decimal>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (fxRate == 0)
                {
                    throw new MarketDataProcessingException("Failed to deserialize the FX rate data.");
                }

                result = new SortedDictionary<CurrencyCode, decimal> { [Enum.Parse<CurrencyCode>(pair)] = fxRate };
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

    private DateOnly GetLastCloseDate()
    {
        var now = DateTime.Now;
        var dayOfWeek = now.DayOfWeek;
        var timeOfDay = now.TimeOfDay;

        // If it's a weekend, return the previous Friday
        if (dayOfWeek == DayOfWeek.Saturday)
        {
            return DateOnly.FromDateTime(now.AddDays(-1));
        }
        else if (dayOfWeek == DayOfWeek.Sunday)
        {
            return DateOnly.FromDateTime(now.AddDays(-2));
        }
        // If it's a weekday but before 5:00 PM, return the previous business day
        else if (dayOfWeek == DayOfWeek.Monday && timeOfDay <= new TimeSpan(23, 0, 0))
        {
            // If it's Monday before 5 PM, return the previous Friday
            return DateOnly.FromDateTime(now.AddDays(-3));
        }
        else if (timeOfDay <= new TimeSpan(17, 0, 0))
        {
            // If it's a weekday before 5 PM, return the previous day
            return DateOnly.FromDateTime(now.AddDays(-1));
        }
        // Otherwise, return today
        return DateOnly.FromDateTime(now);
    }
}
