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
using Boutquin.Trading.Data.Fred.Responses;
using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Interfaces;

namespace Boutquin.Trading.Data.Fred;

/// <summary>
/// Fetches economic time series data from the FRED (Federal Reserve Economic Data) REST API.
/// Returns raw values as FRED provides them (e.g., yields in percent like 5.33, not decimal 0.0533).
/// The caller is responsible for unit transformations.
///
/// Note: FRED requires the API key as a query parameter — it does not support header-based auth.
/// This is the documented API contract. Ensure keys are not logged in production.
/// </summary>
public sealed class FredFetcher : IEconomicDataFetcher, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly string _apiEndpoint;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FredFetcher(
        string apiKey,
        HttpClient? httpClient = null,
        string apiEndpoint = "https://api.stlouisfed.org")
    {
        Guard.AgainstNullOrWhiteSpace(() => apiKey);
        Guard.AgainstNullOrWhiteSpace(() => apiEndpoint);

        _ownsClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient();
        _apiEndpoint = apiEndpoint.TrimEnd('/');
        _apiKey = apiKey;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, decimal>> FetchSeriesAsync(
        string seriesId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(seriesId))
        {
            throw new ArgumentException("Series ID must not be null or whitespace.", nameof(seriesId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var url = BuildUrl(seriesId, startDate, endDate);

        FredObservationsResponse? response;
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new MarketDataRetrievalException(
                    $"FRED API returned HTTP {(int)httpResponse.StatusCode} for series '{seriesId}'.");
            }

            var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            response = JsonSerializer.Deserialize<FredObservationsResponse>(json, s_jsonOptions);
        }
        catch (MarketDataRetrievalException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new MarketDataRetrievalException(
                $"Failed to fetch data from FRED for series '{seriesId}'.", ex);
        }
        catch (JsonException ex)
        {
            throw new MarketDataRetrievalException(
                $"Failed to deserialize FRED response for series '{seriesId}'.", ex);
        }

        if (response?.Observations == null)
        {
            throw new MarketDataRetrievalException(
                $"FRED returned null response for series '{seriesId}'.");
        }

        foreach (var obs in response.Observations)
        {
            // FRED uses "." as the missing-data sentinel
            if (obs.Value == ".")
            {
                continue;
            }

            if (!DateOnly.TryParseExact(obs.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                continue;
            }

            if (!decimal.TryParse(obs.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            yield return new KeyValuePair<DateOnly, decimal>(date, value);
        }
    }

    private string BuildUrl(string seriesId, DateOnly? startDate, DateOnly? endDate)
    {
        var url = $"{_apiEndpoint}/fred/series/observations?series_id={Uri.EscapeDataString(seriesId)}&api_key={_apiKey}&file_type=json";

        if (startDate.HasValue)
        {
            url += $"&observation_start={startDate.Value:yyyy-MM-dd}";
        }

        if (endDate.HasValue)
        {
            url += $"&observation_end={endDate.Value:yyyy-MM-dd}";
        }

        return url;
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}
