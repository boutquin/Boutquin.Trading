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

using System.Net;
using Boutquin.Trading.Data.TwelveData;
using Boutquin.Trading.Domain.Exceptions;

namespace Boutquin.Trading.Tests.UnitTests.Data;

public sealed class TwelveDataFetcherTests
{
    private const string ValidApiKey = "test-twelve-data-api-key";

    // Realistic Twelve Data API JSON responses
    private const string TimeSeriesJson = """
        {"values":[{"datetime":"2024-01-15","open":"185.09","high":"186.42","low":"183.55","close":"185.92","volume":"44234500"},{"datetime":"2024-01-16","open":"186.00","high":"187.00","low":"184.00","close":"186.50","volume":"40000000"}],"status":"ok"}
        """;

    private const string DividendsJson = """
        {"dividends":[{"ex_date":"2024-01-15","amount":0.24}],"status":"ok"}
        """;

    private const string SplitsJson = """
        {"splits":[{"date":"2024-01-15","description":"4-for-1 split","ratio":"4:1","from_factor":1,"to_factor":4}],"status":"ok"}
        """;

    private const string EmptyDividendsJson = """
        {"dividends":[],"status":"ok"}
        """;

    private const string EmptySplitsJson = """
        {"splits":[],"status":"ok"}
        """;

    private static HttpClient CreateMultiEndpointClient(
        string timeSeriesResponse,
        string dividendsResponse,
        string splitsResponse)
    {
        var urlResponses = new Dictionary<string, (string Body, HttpStatusCode Status)>
        {
            ["time_series"] = (timeSeriesResponse, HttpStatusCode.OK),
            ["dividends"] = (dividendsResponse, HttpStatusCode.OK),
            ["splits"] = (splitsResponse, HttpStatusCode.OK),
        };
        return new HttpClient(new MockHttpMessageHandler(urlResponses));
    }

    // ============================================================
    // Constructor validation
    // ============================================================

    [Fact]
    public void Constructor_NullApiKey_ShouldThrow()
    {
        var act = () => new TwelveDataFetcher(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyApiKey_ShouldThrow()
    {
        var act = () => new TwelveDataFetcher("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ValidApiKey_ShouldNotThrow()
    {
        using var fetcher = new TwelveDataFetcher(ValidApiKey);
        fetcher.Should().NotBeNull();
    }

    // ============================================================
    // FetchMarketDataAsync — happy path
    // ============================================================

    [Fact]
    public async Task FetchMarketDataAsync_SingleSymbol_ShouldReturnData()
    {
        using var client = CreateMultiEndpointClient(TimeSeriesJson, EmptyDividendsJson, EmptySplitsJson);
        using var fetcher = new TwelveDataFetcher(ValidApiKey, client, "https://api.twelvedata.com");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync([new Asset("AAPL")]))
        {
            results.Add(item);
        }

        results.Should().HaveCount(2);
        results[0].Key.Should().Be(new DateOnly(2024, 1, 15));
        results[0].Value.Should().ContainKey(new Asset("AAPL"));
    }

    [Fact]
    public async Task FetchMarketDataAsync_ShouldMapFieldsCorrectly()
    {
        using var client = CreateMultiEndpointClient(TimeSeriesJson, EmptyDividendsJson, EmptySplitsJson);
        using var fetcher = new TwelveDataFetcher(ValidApiKey, client, "https://api.twelvedata.com");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync([new Asset("AAPL")]))
        {
            results.Add(item);
        }

        var md = results[0].Value[new Asset("AAPL")];
        md.Open.Should().Be(185.09m);
        md.High.Should().Be(186.42m);
        md.Low.Should().Be(183.55m);
        md.Close.Should().Be(185.92m);
        md.Volume.Should().Be(44234500);
        md.AdjustedClose.Should().Be(185.92m); // TwelveData uses close as adjusted
    }

    [Fact]
    public async Task FetchMarketDataAsync_WithDividends_ShouldIncludeDividendData()
    {
        using var client = CreateMultiEndpointClient(TimeSeriesJson, DividendsJson, EmptySplitsJson);
        using var fetcher = new TwelveDataFetcher(ValidApiKey, client, "https://api.twelvedata.com");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync([new Asset("AAPL")]))
        {
            results.Add(item);
        }

        var md = results[0].Value[new Asset("AAPL")];
        md.DividendPerShare.Should().Be(0.24m);
    }

    [Fact]
    public async Task FetchMarketDataAsync_WithSplits_ShouldIncludeSplitCoefficient()
    {
        using var client = CreateMultiEndpointClient(TimeSeriesJson, EmptyDividendsJson, SplitsJson);
        using var fetcher = new TwelveDataFetcher(ValidApiKey, client, "https://api.twelvedata.com");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync([new Asset("AAPL")]))
        {
            results.Add(item);
        }

        var md = results[0].Value[new Asset("AAPL")];
        md.SplitCoefficient.Should().Be(4.0m); // from_factor=1, to_factor=4 → 4/1=4
    }

    // ============================================================
    // Error handling
    // ============================================================

    [Fact]
    public async Task FetchMarketDataAsync_HttpError_ShouldThrowMarketDataRetrievalException()
    {
        var urlResponses = new Dictionary<string, (string Body, HttpStatusCode Status)>
        {
            ["time_series"] = ("{}", HttpStatusCode.InternalServerError),
            ["dividends"] = (EmptyDividendsJson, HttpStatusCode.OK),
            ["splits"] = (EmptySplitsJson, HttpStatusCode.OK),
        };
        using var client = new HttpClient(new MockHttpMessageHandler(urlResponses));
        using var fetcher = new TwelveDataFetcher(ValidApiKey, client, "https://api.twelvedata.com");

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchMarketDataAsync([new Asset("AAPL")]))
            {
            }
        };

        await act.Should().ThrowAsync<MarketDataRetrievalException>();
    }

    [Fact]
    public async Task FetchMarketDataAsync_InvalidJson_ShouldThrowMarketDataRetrievalException()
    {
        var urlResponses = new Dictionary<string, (string Body, HttpStatusCode Status)>
        {
            ["time_series"] = ("NOT VALID JSON", HttpStatusCode.OK),
            ["dividends"] = (EmptyDividendsJson, HttpStatusCode.OK),
            ["splits"] = (EmptySplitsJson, HttpStatusCode.OK),
        };
        using var client = new HttpClient(new MockHttpMessageHandler(urlResponses));
        using var fetcher = new TwelveDataFetcher(ValidApiKey, client, "https://api.twelvedata.com");

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchMarketDataAsync([new Asset("AAPL")]))
            {
            }
        };

        await act.Should().ThrowAsync<MarketDataRetrievalException>();
    }

    [Fact]
    public async Task FetchMarketDataAsync_NullSymbols_ShouldThrow()
    {
        using var client = CreateMultiEndpointClient(TimeSeriesJson, EmptyDividendsJson, EmptySplitsJson);
        using var fetcher = new TwelveDataFetcher(ValidApiKey, client, "https://api.twelvedata.com");

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchMarketDataAsync(null!))
            {
            }
        };

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task FetchMarketDataAsync_EmptySymbols_ShouldThrow()
    {
        using var client = CreateMultiEndpointClient(TimeSeriesJson, EmptyDividendsJson, EmptySplitsJson);
        using var fetcher = new TwelveDataFetcher(ValidApiKey, client, "https://api.twelvedata.com");

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchMarketDataAsync(Array.Empty<Asset>()))
            {
            }
        };

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ============================================================
    // FX rates not supported
    // ============================================================

    [Fact]
    public void FetchFxRatesAsync_ShouldThrowNotSupported()
    {
        using var client = CreateMultiEndpointClient(TimeSeriesJson, EmptyDividendsJson, EmptySplitsJson);
        using var fetcher = new TwelveDataFetcher(ValidApiKey, client, "https://api.twelvedata.com");

        var act = () => fetcher.FetchFxRatesAsync(["USDCAD"]);
        act.Should().Throw<NotSupportedException>();
    }

    // ============================================================
    // Exchange parameter
    // ============================================================

    [Fact]
    public async Task FetchMarketDataAsync_WithExchange_ShouldIncludeExchangeInUrl()
    {
        var handler = new MockHttpMessageHandler(new Dictionary<string, (string Body, HttpStatusCode Status)>
        {
            ["time_series"] = (TimeSeriesJson, HttpStatusCode.OK),
            ["dividends"] = (EmptyDividendsJson, HttpStatusCode.OK),
            ["splits"] = (EmptySplitsJson, HttpStatusCode.OK),
        });
        using var client = new HttpClient(handler);
        using var fetcher = new TwelveDataFetcher(ValidApiKey, client, "https://api.twelvedata.com", exchange: "TSX");

        await foreach (var _ in fetcher.FetchMarketDataAsync([new Asset("AC")]))
        {
        }

        handler.AllRequests.Should().Contain(r =>
            r.RequestUri != null && r.RequestUri.ToString().Contains("exchange=TSX"));
    }

    // ============================================================
    // Dispose
    // ============================================================

    [Fact]
    public void Dispose_OwnedClient_ShouldNotThrow()
    {
        var fetcher = new TwelveDataFetcher(ValidApiKey);
        var act = fetcher.Dispose;
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_InjectedClient_ShouldNotDisposeClient()
    {
        using var client = CreateMultiEndpointClient(TimeSeriesJson, EmptyDividendsJson, EmptySplitsJson);
        var fetcher = new TwelveDataFetcher(ValidApiKey, client, "https://api.twelvedata.com");
        fetcher.Dispose();

        // Client should still be usable after fetcher disposal
        var act = () => client.BaseAddress;
        act.Should().NotThrow();
    }

    // ============================================================
    // Dividend/Split failures are non-fatal
    // ============================================================

    [Fact]
    public async Task FetchMarketDataAsync_DividendEndpointFails_ShouldStillReturnData()
    {
        var urlResponses = new Dictionary<string, (string Body, HttpStatusCode Status)>
        {
            ["time_series"] = (TimeSeriesJson, HttpStatusCode.OK),
            ["dividends"] = ("{}", HttpStatusCode.InternalServerError),
            ["splits"] = (EmptySplitsJson, HttpStatusCode.OK),
        };
        using var client = new HttpClient(new MockHttpMessageHandler(urlResponses));
        using var fetcher = new TwelveDataFetcher(ValidApiKey, client, "https://api.twelvedata.com");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync([new Asset("AAPL")]))
        {
            results.Add(item);
        }

        // Time series should still be returned even though dividends failed
        results.Should().HaveCount(2);
    }
}
