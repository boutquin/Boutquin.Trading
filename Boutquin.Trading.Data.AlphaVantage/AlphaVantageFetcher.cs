// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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

using System.Net;
using System.Text.Json;

using Boutquin.Domain.Converters;
using Boutquin.Domain.Helpers;

using Domain.Data;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Interfaces;

using Microsoft.Extensions.Caching.Distributed;

/// <summary>
/// A class for fetching market data from the Alpha Vantage API, with caching and rate limiting support.
/// </summary>
public sealed class AlphaVantageFetcher : IMarketDataFetcher
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly string _apiKey;
    private readonly string _apiEndpoint;
    private readonly SemaphoreSlim _rateLimitSemaphore;
    private readonly TimeSpan _cacheExpiration;
    private readonly int _rateLimitDelay;

    /// <summary>
    /// Initializes a new instance of the AlphaVantageFetcher class with the specified API key, IDistributedCache, and optional HttpClient, API endpoint, rate limiter, and cache expiration.
    /// </summary>
    /// <param name="apiKey">The API key for the Alpha Vantage API.</param>
    /// <param name="cache">The IDistributedCache instance for caching the API responses.</param>
    /// <param name="httpClient">An optional HttpClient instance to use for making API requests. If not provided, a new instance will be created.</param>
    /// <param name="apiEndpoint">An optional API endpoint for the Alpha Vantage API. If not provided, the default endpoint will be used.</param>
    /// <param name="rateLimiter">An optional SemaphoreSlim instance for rate limiting. If not provided, a new instance with a limit of 5 will be created.</param>
    /// <param name="rateLimitDelay">An optional delay between API requests when rate limiting is applied. If not provided, a default value of 5 will be used.</param>
    /// <param name="cacheExpiration">An optional TimeSpan for setting the cache expiration. If not provided, a default value of 6 hours will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided API key or IDistributedCache is null or empty.</exception>
    public AlphaVantageFetcher(
        string apiKey,
        IDistributedCache cache, 
        HttpClient httpClient = null,
        string apiEndpoint = "https://www.alphavantage.co/query",
        SemaphoreSlim rateLimiter = null,
        int rateLimitDelay = 5,
        TimeSpan? cacheExpiration = null)
    {
        _apiKey = Environment.GetEnvironmentVariable("ALPHA_VANTAGE_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentNullException(nameof(apiKey), "API key cannot be null or empty.");
        }

        _apiKey = apiKey;
        _cache = cache ?? throw new ArgumentNullException(nameof(cache), "IDistributedCache cannot be null.");
        _rateLimitDelay = rateLimitDelay;
        _httpClient = httpClient ?? new HttpClient();
        _apiEndpoint = !string.IsNullOrEmpty(apiEndpoint) ? apiEndpoint : throw new ArgumentNullException(nameof(apiEndpoint), "API endpoint cannot be null or empty.");
        _rateLimitSemaphore = rateLimiter ?? new SemaphoreSlim(5); // Adjust the number according to the allowed rate limit
        _cacheExpiration = cacheExpiration ?? TimeSpan.FromHours(6);
    }

    /// <summary>
    /// Initializes a new instance of the AlphaVantageFetcher class with the specified API key, IDistributedCache, and optional HttpClient, API endpoint, rate limiter, and cache expiration.
    /// </summary>
    /// <param name="cache">The IDistributedCache instance for caching the API responses.</param>
    /// <param name="httpClient">An optional HttpClient instance to use for making API requests. If not provided, a new instance will be created.</param>
    /// <param name="apiEndpoint">An optional API endpoint for the Alpha Vantage API. If not provided, the default endpoint will be used.</param>
    /// <param name="rateLimiter">An optional SemaphoreSlim instance for rate limiting. If not provided, a new instance with a limit of 5 will be created.</param>
    /// <param name="rateLimitDelay">An optional delay between API requests when rate limiting is applied. If not provided, a default value of 5 will be used.</param>
    /// <param name="cacheExpiration">An optional TimeSpan for setting the cache expiration. If not provided, a default value of 6 hours will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided API key or IDistributedCache is null or empty.</exception>
    public AlphaVantageFetcher(
        IDistributedCache cache,
        HttpClient httpClient = null,
        string apiEndpoint = "https://www.alphavantage.co/query",
        SemaphoreSlim rateLimiter = null,
        int rateLimitDelay = 5,
        TimeSpan? cacheExpiration = null)
    {
        _apiKey = Environment.GetEnvironmentVariable("ALPHA_VANTAGE_API_KEY");
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException("API key cannot be read from ALPHA_VANTAGE_API_KEY environment variable.");
        }

        _cache = cache ?? throw new ArgumentNullException(nameof(cache), "IDistributedCache cannot be null.");
        _rateLimitDelay = rateLimitDelay;
        _httpClient = httpClient ?? new HttpClient();
        _apiEndpoint = !string.IsNullOrEmpty(apiEndpoint) ? apiEndpoint : throw new ArgumentNullException(nameof(apiEndpoint), "API endpoint cannot be null or empty.");
        _rateLimitSemaphore = rateLimiter ?? new SemaphoreSlim(5); // Adjust the number according to the allowed rate limit
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
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<string, MarketData>?>> FetchMarketDataAsync(IEnumerable<string> assets)
    {
        // Validate the input assets list
        Guard.AgainstEmptyOrNullEnumerable(() => assets); // Throws ArgumentException

        // Configure the JsonSerializerOptions with custom converter for DateOnly and DateOnlyDictionary
        var options = new JsonSerializerOptions();
        options.Converters.Add(new DateOnlyConverter());
        options.Converters.Add(new DateOnlyDictionaryConverterFactory());

        // Retrieve the market data from the cache for all assets in parallel
        var cacheKeys = assets.Select(asset => $"MarketData_{asset}").ToList();
        var cachedDataList = await Task.WhenAll(cacheKeys.Select(key => _cache.GetStringAsync(key)));

        // Iterate through each asset in the list
        for (var i = 0; i < assets.Count(); i++)
        {
            var asset = assets.ElementAt(i);
            var cachedData = cachedDataList[i];

            // If the data is present in the cache, deserialize and yield it
            if (cachedData != null)
            {
                await foreach (var dataPoint in DeserializeAndYieldCachedMarketData(cachedData, asset, options))
                {
                    yield return dataPoint;
                }
            }
            else
            {
                // If the data is not in the cache, prepare the API request URL
                var requestUri = BuildRequestUri(asset);

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

                // Deserialize, yield, and cache the JSON time series data
                var marketData = new SortedDictionary<DateOnly, SortedDictionary<string, MarketData>?>();

                // Deserialize, accumulate, and cache the JSON time series data
                var accumulatedMarketData = DeserializeAndYieldMarketDataFromApi(timeSeries, asset, options);

                foreach (var dataPoint in accumulatedMarketData)
                {
                    marketData[dataPoint.Key] = dataPoint.Value;
                    yield return dataPoint;
                }

                // Serialize and store the market data in the cache with the specified expiration time
                var serializedMarketData = JsonSerializer.Serialize(marketData, options);
                await _cache.SetStringAsync(cacheKeys[i], serializedMarketData, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _cacheExpiration });
            }
        }
    }

    /// <summary>
    /// Fetches historical foreign exchange rates for the specified currency pairs from the Alpha Vantage API, returning an IAsyncEnumerable of data points as a KeyValuePair with DateOnly as keys and SortedDictionary with currency pair symbols as keys and decimal exchange rates as values.
    /// </summary>
    /// <param name="currencyPairs">A list of currency pair symbols to fetch historical foreign exchange rates for.</param>
    /// <returns>An IAsyncEnumerable of KeyValuePair with DateOnly as keys and SortedDictionary with currency pair symbols as keys and decimal exchange rates as values.</returns>
    /// <exception cref="FxDataRetrievalException">Thrown when an error occurs during the foreign exchange data retrieval process.</exception>
    /// <example>
    /// <code>
    /// var currencyPairs = new List&lt;string&gt; { "USD/EUR", "USD/GBP" };
    /// var fetcher = new AlphaVantageFetcher(apiKey, cache);
    /// 
    /// await foreach (var dataPoint in fetcher.FetchFxRatesAsync(currencyPairs))
    /// {
    ///     DateOnly date = dataPoint.Key;
    ///     SortedDictionary&lt;string, decimal&gt; fxRates = dataPoint.Value;
    /// 
    ///     Console.WriteLine($"Date: {date}");
    ///     foreach (var pairRate in fxRates)
    ///     {
    ///         string pair = pairRate.Key;
    ///         decimal rate = pairRate.Value;
    /// 
    ///         Console.WriteLine($"{pair} - Rate: {rate}");
    ///     }
    /// }
    /// </code>
    /// </example>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> FetchFxRatesAsync(IEnumerable<string> currencyPairs)
    {
        // Validate the input currency pairs list
        Guard.AgainstEmptyOrNullEnumerable(() => currencyPairs); // Throws ArgumentException

        // Configure the JsonSerializerOptions with custom converter for DateOnly and DateOnlyDictionary
        var options = new JsonSerializerOptions();
        options.Converters.Add(new DateOnlyConverter());
        options.Converters.Add(new DateOnlyDictionaryConverterFactory());

        // Retrieve the fx data from the cache for all currency pairs in parallel
        var cacheKeys = currencyPairs.Select(pair => $"FxRates_{pair}").ToList();
        var cachedDataList = await Task.WhenAll(cacheKeys.Select(key => _cache.GetStringAsync(key)));

        // Iterate through each currency pair in the list
        for (var i = 0; i < currencyPairs.Count(); i++)
        {
            var pair = currencyPairs.ElementAt(i);
            // Split the pair into base and quote currency codes
            var splitPair = pair.Split('_');
            if (splitPair.Length != 2 || !Enum.TryParse<CurrencyCode>(splitPair[1], out var quoteCurrencyCode))
            {
                throw new ArgumentException($"Invalid currency pair: {pair}", nameof(currencyPairs));
            }

            var cachedData = cachedDataList[i];

            // If the data is present in the cache, deserialize and yield it
            if (cachedData != null)
            {
                await foreach (var dataPoint in DeserializeAndYieldCachedFxRates(cachedData, pair, options))
                {
                    yield return new KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>(dataPoint.Key, new SortedDictionary<CurrencyCode, decimal> { { quoteCurrencyCode, dataPoint.Value.Values.First() } });
                }
            }
            else
            {
                // If the data is not in the cache, prepare the API request URL
                var requestUri = BuildFxRequestUri(pair);

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
                if (!root.TryGetProperty("Time Series FX (Daily)", out var timeSeries))
                {
                    throw new MarketDataRetrievalException("The Alpha Vantage API response did not contain the expected 'Time Series FX (Daily)' property.");
                }

                // Deserialize the time series data into a SortedDictionary with DateOnly keys and decimal values
                var fxRates = JsonSerializer.Deserialize<SortedDictionary<DateOnly, decimal>>(timeSeries.GetRawText(), options);

                // Convert SortedDictionary<DateOnly, decimal> to SortedDictionary<CurrencyCode, decimal>
                var currencyCodeFxRates = new SortedDictionary<CurrencyCode, decimal>();
                foreach (var rate in fxRates)
                {
                    currencyCodeFxRates.Add(quoteCurrencyCode, rate.Value);
                }

                // Store the fx data in the cache for future use
                await _cache.SetStringAsync($"FxRates_{pair}", JsonSerializer.Serialize(currencyCodeFxRates, options));

                // Yield the data
                yield return new KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>(DateOnly.FromDateTime(DateTime.Today), currencyCodeFxRates);
            }
        }
    }

    /// <summary>
    /// Builds the request URI for the Alpha Vantage API based on the provided asset symbol.
    /// </summary>
    /// <param name="asset">The asset symbol for which the request URI is being built.</param>
    /// <returns>A string representing the request URI.</returns>
    private string BuildRequestUri(string asset)
    {
        return $"{_apiEndpoint}?function=TIME_SERIES_DAILY_ADJUSTED&symbol={asset}&apikey={_apiKey}&outputsize=full&datatype=json";
    }

    /// <summary>
    /// Constructs the request URL for fetching foreign exchange (FX) rates for a specific currency pair.
    /// </summary>
    /// <param name="pair">The currency pair.</param>
    /// <returns>The request URL as a Uri object.</returns>
    private string BuildFxRequestUri(string pair)
    {
        // Split the currency pair into base currency and quote currency
        var currencies = pair.Split('/');
        var baseCurrency = currencies[0];
        var quoteCurrency = currencies[1];

        // Construct the API endpoint
        return $"{_apiEndpoint}?function=FX_DAILY&from_symbol={baseCurrency}&to_symbol={quoteCurrency}&apikey={_apiKey}";
    }

    /// <summary>
    /// Deserializes and asynchronously yields cached market data for a specific asset.
    /// </summary>
    /// <param name="cachedData">The cached market data in JSON format.</param>
    /// <param name="asset">The asset symbol for which the market data is being deserialized.</param>
    /// <param name="options">The JsonSerializerOptions used to deserialize the market data.</param>
    /// <returns>
    /// An IAsyncEnumerable&lt;KeyValuePair&lt;DateOnly, SortedDictionary&lt;string, MarketData&gt;&gt;&gt; for each data point in the cached market data.
    /// The key is the date of the data point, and the value is a sorted dictionary containing the asset symbol and
    /// its corresponding market data.
    /// </returns>
    private async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<string, MarketData>?>> DeserializeAndYieldCachedMarketData(
        string cachedData, string asset, JsonSerializerOptions? options)
    {
        var cachedMarketData = JsonSerializer.Deserialize<SortedDictionary<DateOnly, MarketData>>(cachedData, options);
        foreach (var dataPoint in cachedMarketData)
        {
            yield return new KeyValuePair<DateOnly, SortedDictionary<string, MarketData>?>(dataPoint.Key, new SortedDictionary<string, MarketData> { { asset, dataPoint.Value } });
        }
    }

    /// <summary>
    /// Deserializes and yields market data from the provided JSON time series element for a specific asset.
    /// </summary>
    /// <param name="timeSeries">The JSON time series element containing the market data.</param>
    /// <param name="asset">The asset symbol for which the market data is being deserialized.</param>
    /// <param name="options">The JsonSerializerOptions used to deserialize the market data.</param>
    /// <returns>
    /// An IAsyncEnumerable that asynchronously yields KeyValuePair&lt;DateOnly, SortedDictionary&lt;string, MarketData&gt;&gt; for each
    /// data point in the time series. The key is the date of the data point, and the value is a sorted dictionary containing
    /// the asset symbol and its corresponding market data.
    /// </returns>
    /// <remarks>
    /// This method catches any FormatException or MarketDataRetrievalException and logs the error (if a logger is available)
    /// while continuing to process the remaining data points.
    /// </remarks>
    private IEnumerable<KeyValuePair<DateOnly, SortedDictionary<string, MarketData>?>> DeserializeAndYieldMarketDataFromApi(
        JsonElement timeSeries, string asset, JsonSerializerOptions? options)
    {
        var marketData = new SortedDictionary<DateOnly, MarketData>();
        foreach (var entry in timeSeries.EnumerateObject())
        {
            var date = DateOnly.Parse(entry.Name);
            var marketDataJson = entry.Value;

            var dataPoint = CreateMarketDataFromJson(date, marketDataJson);
            if (dataPoint != null)
            {
                marketData.Add(date, dataPoint);
                yield return new KeyValuePair<DateOnly, SortedDictionary<string, MarketData>?>(date, new SortedDictionary<string, MarketData> { { asset, dataPoint } });
            }
        }
    }

    /// <summary>
    /// Creates a MarketData instance from the provided JSON element.
    /// </summary>
    /// <param name="marketDataJson">The JSON element containing market data.</param>
    /// <returns>A MarketData instance with the data extracted from the JSON element.</returns>
    /// <exception cref="MarketDataRetrievalException">
    /// Thrown when a required property is missing, a property value is null, or a property value has an invalid format.
    /// </exception>
    private static MarketData CreateMarketDataFromJson(DateOnly date, JsonElement marketDataJson)
    {
        try
        {
            var openString = marketDataJson.GetProperty("1. open").GetString();
            var highString = marketDataJson.GetProperty("2. high").GetString();
            var lowString = marketDataJson.GetProperty("3. low").GetString();
            var closeString = marketDataJson.GetProperty("4. close").GetString();
            var adjustedCloseString = marketDataJson.GetProperty("5. adjusted close").GetString();
            var volumeString = marketDataJson.GetProperty("6. volume").GetString();
            var dividendAmountString = marketDataJson.GetProperty("7. dividend amount").GetString();
            var splitCoefficientString = marketDataJson.GetProperty("8. split coefficient").GetString();

            if (openString == null || highString == null || lowString == null ||
                closeString == null || adjustedCloseString == null || volumeString == null ||
                dividendAmountString == null || splitCoefficientString == null)
            {
                throw new MarketDataRetrievalException("Failed to parse market data from the JSON element: a property value is null.");
            }

            var open = decimal.Parse(openString);
            var high = decimal.Parse(highString);
            var low = decimal.Parse(lowString);
            var close = decimal.Parse(closeString);
            var adjustedClose = decimal.Parse(adjustedCloseString);
            var volume = long.Parse(volumeString);
            var dividendAmount = decimal.Parse(dividendAmountString);
            var splitCoefficient = decimal.Parse(splitCoefficientString);

            return new MarketData(date, open, high, low, close, adjustedClose, volume, dividendAmount, splitCoefficient);
        }
        catch (KeyNotFoundException)
        {
            throw new MarketDataRetrievalException("Failed to parse market data from the JSON element: a required property is missing.");
        }
        catch (FormatException)
        {
            throw new MarketDataRetrievalException("Failed to parse market data from the JSON element: a property value has an invalid format.");
        }
    }

    /// <summary>
    /// Deserializes cached foreign exchange (FX) rates and yields them one by one.
    /// </summary>
    /// <param name="cachedData">The cached data as a string.</param>
    /// <param name="pair">The currency pair.</param>
    /// <param name="options">The JSON serializer options.</param>
    /// <returns>An asynchronous stream of date-FX rates pairs.</returns>
    private async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<string, decimal>>> DeserializeAndYieldCachedFxRates(string cachedData, string pair, JsonSerializerOptions options)
    {
        // Deserialize the cached data
        var fxRates = JsonSerializer.Deserialize<SortedDictionary<DateOnly, decimal>>(cachedData, options);

        // Yield the FX rates one by one
        foreach (var rate in fxRates)
        {
            yield return new KeyValuePair<DateOnly, SortedDictionary<string, decimal>>(rate.Key, new SortedDictionary<string, decimal> { { pair, rate.Value } });
            await Task.Yield(); // This will ensure the method behaves asynchronously
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
        HttpResponseMessage response = null;
        var retryCount = 0;
        const int MaxRetryCount = 3;

        while (retryCount < MaxRetryCount)
        {
            try
            {
                // Wait until the semaphore is available
                await _rateLimitSemaphore.WaitAsync();

                // Make the HTTP GET request
                response = await _httpClient.GetAsync(requestUri);

                // Check if the response status indicates rate limiting
                if (!IsRateLimited(response.StatusCode))
                {
                    // If not rate limited, break the loop and return the response
                    break;
                }
            }
            catch (HttpRequestException ex)
            {
                // Log the exception, for example: _logger.LogError(ex, "An error occurred during the HTTP request.");
            }
            finally
            {
                // Release the semaphore and enforce the rate limit delay
                _rateLimitSemaphore.Release();
                await Task.Delay(_rateLimitDelay);
                retryCount++;
            }
        }

        // If the response is still null after retrying, throw an exception
        if (response == null)
        {
            throw new MarketDataRetrievalException("Failed to fetch data after multiple retries.");
        }

        return response;
    }

    private static bool IsRateLimited(HttpStatusCode statusCode)
    {
        // Adjust this method depending on the API's rate limiting response status codes
        return statusCode == HttpStatusCode.TooManyRequests;
    }

    /// <summary>
    /// Converts an IAsyncEnumerable<T> to a List<T> asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the IAsyncEnumerable.</typeparam>
    /// <param name="items">The IAsyncEnumerable<T> instance to be converted to a List<T>.</param>
    /// <returns>A Task representing the asynchronous operation, with a result of type List<T> containing the elements from the input IAsyncEnumerable.</returns>
    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> items)
    {
        var list = new List<T>();
        await foreach (var item in items)
        {
            list.Add(item);
        }
        return list;
    }
}
