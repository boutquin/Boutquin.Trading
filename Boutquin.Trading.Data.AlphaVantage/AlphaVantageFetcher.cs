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

namespace Boutquin.Trading.Data.AlphaVantage;

using System.Net.Http;
using System;
using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;

using Boutquin.Domain.Converters;
using Domain.Data;
using Domain.Exceptions;

public sealed class AlphaVantageFetcher
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly string _apiKey;
    private readonly string _apiEndpoint;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly TimeSpan _cacheExpiration;

    /// <summary>
    /// Initializes a new instance of the AlphaVantageFetcher class with the specified API key, IDistributedCache, and optional HttpClient, API endpoint, rate limiter, and cache expiration.
    /// </summary>
    /// <param name="apiKey">The API key for the Alpha Vantage API.</param>
    /// <param name="cache">The IDistributedCache instance for caching the API responses.</param>
    /// <param name="httpClient">An optional HttpClient instance to use for making API requests. If not provided, a new instance will be created.</param>
    /// <param name="apiEndpoint">An optional API endpoint for the Alpha Vantage API. If not provided, the default endpoint will be used.</param>
    /// <param name="rateLimiter">An optional SemaphoreSlim instance for rate limiting. If not provided, a new instance with a limit of 5 will be created.</param>
    /// <param name="cacheExpiration">An optional TimeSpan for setting the cache expiration. If not provided, a default value of 6 hours will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided API key or IDistributedCache is null or empty.</exception>
    public AlphaVantageFetcher(
        string apiKey,
        IDistributedCache cache,
        HttpClient httpClient = null,
        string apiEndpoint = "https://www.alphavantage.co/query",
        SemaphoreSlim rateLimiter = null,
        TimeSpan? cacheExpiration = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentNullException(nameof(apiKey), "API key cannot be null or empty.");
        }

        _apiKey = apiKey;
        _cache = cache ?? throw new ArgumentNullException(nameof(cache), "IDistributedCache cannot be null.");
        _httpClient = httpClient ?? new HttpClient();
        _apiEndpoint = !string.IsNullOrEmpty(apiEndpoint) ? apiEndpoint : throw new ArgumentNullException(nameof(apiEndpoint), "API endpoint cannot be null or empty.");
        _rateLimiter = rateLimiter ?? new SemaphoreSlim(5); // Adjust the number according to the allowed rate limit
        _cacheExpiration = cacheExpiration ?? TimeSpan.FromHours(6);
    }

    /// <summary>
    /// Fetches historical market data for the specified assets from the Alpha Vantage API, returning an IAsyncEnumerable of data points as a KeyValuePair with DateOnly as keys and SortedDictionary with asset symbols as keys and MarketData objects as values.
    /// </summary>
    /// <param name="assets">A list of asset symbols to fetch market data for.</param>
    /// <returns>An IAsyncEnumerable of KeyValuePair with DateOnly as keys and SortedDictionary with asset symbols as keys and MarketData objects as values.</returns>
    /// <exception cref="MarketDataRetrievalException">Thrown when an error occurs during the market data retrieval process.</exception>
    /// <example>
    /// <code>
    /// var assets = new List&lt;string&gt; { "AAPL", "GOOG" };
    /// var fetcher = new AlphaVantageFetcher(apiKey, cache);
    /// 
    /// await foreach (var dataPoint in fetcher.FetchMarketDataAsync(assets))
    /// {
    ///     DateOnly date = dataPoint.Key;
    ///     SortedDictionary&lt;string, MarketData&gt; marketData = dataPoint.Value;
    /// 
    ///     Console.WriteLine($"Date: {date}");
    ///     foreach (var assetMarketData in marketData)
    ///     {
    ///         string assetSymbol = assetMarketData.Key;
    ///         MarketData assetData = assetMarketData.Value;
    /// 
    ///         Console.WriteLine($"{assetSymbol} - Open: {assetData.Open}, High: {assetData.High}, Low: {assetData.Low}, Close: {assetData.Close}, AdjustedClose: {assetData.AdjustedClose}, Volume: {assetData.Volume}, DividendPerShare: {assetData.DividendPerShare}, SplitCoefficient: {assetData.SplitCoefficient}");
    ///     }
    /// }
    /// </code>
    /// </example>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>> FetchMarketDataAsync(IEnumerable<string> assets)
    {
        // Validate the input assets list
        if (assets == null || !assets.Any())
        {
            throw new ArgumentException("At least one asset symbol must be provided.", nameof(assets));
        }

        var options = new JsonSerializerOptions();
        options.Converters.Add(new DateOnlyConverter());

        // Iterate through each asset in the list
        foreach (var asset in assets)
        {
            // Create a cache key for the current asset
            var cacheKey = $"MarketData_{asset}";

            // Try to retrieve the market data from the cache
            var cachedData = await _cache.GetStringAsync(cacheKey);

            // If the data is present in the cache, deserialize and return it
            if (cachedData != null)
            {
                var cachedMarketData = JsonSerializer.Deserialize<SortedDictionary<DateOnly, MarketData>>(cachedData, options);
                foreach (var dataPoint in cachedMarketData)
                {
                    yield return new KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>(dataPoint.Key, new SortedDictionary<string, MarketData> { { asset, dataPoint.Value } });
                }
            }
            else
            {
                // If the data is not in the cache, prepare the API request URL
                var requestUri = $"{_apiEndpoint}?function=TIME_SERIES_DAILY_ADJUSTED&symbol={asset}&apikey={_apiKey}&outputsize=full&datatype=json";

                // Make the API request with rate limiting
                var response = await GetAsyncWithRateLimiting(requestUri);

                // Ensure the request was successful
                response.EnsureSuccessStatusCode();

                // Read the response content as a string
                var responseContent = await response.Content.ReadAsStringAsync();

                // Parse the JSON response
                using var jsonDocument = JsonDocument.Parse(responseContent);
                var root = jsonDocument.RootElement;

                // Check if the JSON response contains the required time series data
                if (!root.TryGetProperty("Time Series (Daily)", out var timeSeries))
                {
                    throw new MarketDataRetrievalException("Failed to parse market data from the Alpha Vantage API response.");
                }

                // Deserialize the JSON time series data to a SortedDictionary<DateOnly, MarketData>
                var marketData = new SortedDictionary<DateOnly, MarketData>();
                foreach (var entry in timeSeries.EnumerateObject())
                {
                    var date = DateOnly.Parse(entry.Name);
                    var marketDataJson = entry.Value;

                    var dataPoint = new MarketData(
                        date,
                        decimal.Parse(marketDataJson.GetProperty("1. open").GetString()),
                        decimal.Parse(marketDataJson.GetProperty("2. high").GetString()),
                        decimal.Parse(marketDataJson.GetProperty("3. low").GetString()),
                        decimal.Parse(marketDataJson.GetProperty("4. close").GetString()),
                        decimal.Parse(marketDataJson.GetProperty("5. adjusted close").GetString()),
                        long.Parse(marketDataJson.GetProperty("6. volume").GetString()),
                        decimal.Parse(marketDataJson.GetProperty("7. dividend amount").GetString()),
                        decimal.Parse(marketDataJson.GetProperty("8. split coefficient").GetString()));
                        
                    marketData.Add(date, dataPoint);
                    yield return new KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>(date, new SortedDictionary<string, MarketData> { { asset, dataPoint } });
                }

                // Serialize and store the market data in the cache with the specified expiration time
                var serializedMarketData = JsonSerializer.Serialize(marketData, options);
                await _cache.SetStringAsync(cacheKey, serializedMarketData, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _cacheExpiration });
            }
        }
    }

    /// <summary>
    /// A private helper method to perform an HTTP GET request with rate limiting.
    /// </summary>
    /// <param name="requestUri">The request URI to send the HTTP GET request to.</param>
    /// <returns>A task that represents the asynchronous operation, containing the HttpResponseMessage upon completion.</returns>
    /// <exception cref="MarketDataRetrievalException">Thrown when an error occurs during the rate-limited GET request.</exception>
    private async Task<HttpResponseMessage> GetAsyncWithRateLimiting(string requestUri)
    {
        await _rateLimiter.WaitAsync();
        try
        {
            return await _httpClient.GetAsync(requestUri);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }
}
