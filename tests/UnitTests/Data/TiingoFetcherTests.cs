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
using Boutquin.Trading.Data.Tiingo;
using Boutquin.Trading.Domain.Exceptions;

namespace Boutquin.Trading.Tests.UnitTests.Data;

public sealed class TiingoFetcherTests
{
    private const string ValidApiKey = "test-api-key-0123456789abcdef01234567";

    private const string SingleDayJson = """
        [{"date":"2024-01-15T00:00:00+00:00","open":185.09,"high":186.42,"low":183.55,"close":185.92,"volume":44234500,"adjOpen":184.80,"adjHigh":186.12,"adjLow":183.26,"adjClose":185.62,"adjVolume":44234500,"divCash":0.24,"splitFactor":1.0}]
        """;

    private const string TwoDayJson = """
        [{"date":"2024-01-15T00:00:00+00:00","open":185.09,"high":186.42,"low":183.55,"close":185.92,"volume":44234500,"adjOpen":184.80,"adjHigh":186.12,"adjLow":183.26,"adjClose":185.62,"adjVolume":44234500,"divCash":0.24,"splitFactor":1.0},{"date":"2024-01-16T00:00:00+00:00","open":186.00,"high":187.00,"low":184.00,"close":186.50,"volume":40000000,"adjOpen":185.70,"adjHigh":186.70,"adjLow":183.70,"adjClose":186.20,"adjVolume":40000000,"divCash":0.0,"splitFactor":1.0}]
        """;

    private static HttpClient CreateMockClient(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new MockHttpMessageHandler(responseBody, statusCode);
        return new HttpClient(handler);
    }

    private static MockHttpMessageHandler CreateMockHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new MockHttpMessageHandler(responseBody, statusCode);
    }

    [Fact]
    public async Task SingleSymbol_ReturnsMarketData()
    {
        using var client = CreateMockClient(SingleDayJson);
        using var fetcher = new TiingoFetcher(ValidApiKey, client, "https://api.tiingo.com");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync(new[] { new Asset("AAPL") }, CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().HaveCount(1);
        results[0].Key.Should().Be(new DateOnly(2024, 1, 15));
        results[0].Value.Should().ContainKey(new Asset("AAPL"));
    }

    [Fact]
    public async Task MapsFieldsCorrectly()
    {
        using var client = CreateMockClient(SingleDayJson);
        using var fetcher = new TiingoFetcher(ValidApiKey, client, "https://api.tiingo.com");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync(new[] { new Asset("AAPL") }, CancellationToken.None))
        {
            results.Add(item);
        }

        var md = results[0].Value[new Asset("AAPL")];
        md.Open.Should().Be(185.09m);
        md.High.Should().Be(186.42m);
        md.Low.Should().Be(183.55m);
        md.Close.Should().Be(185.92m);
        md.AdjustedClose.Should().Be(185.62m);
        md.Volume.Should().Be(44234500L);
        md.DividendPerShare.Should().Be(0.24m);
        md.SplitCoefficient.Should().Be(1.0m);
    }

    [Fact]
    public async Task MultipleSymbols_YieldsAllDates()
    {
        using var client = CreateMockClient(TwoDayJson);
        using var fetcher = new TiingoFetcher(ValidApiKey, client, "https://api.tiingo.com");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync(new[] { new Asset("AAPL") }, CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().HaveCount(2);
        results[0].Key.Should().Be(new DateOnly(2024, 1, 15));
        results[1].Key.Should().Be(new DateOnly(2024, 1, 16));
    }

    [Fact]
    public async Task EmptyResponse_YieldsNothing()
    {
        using var client = CreateMockClient("[]");
        using var fetcher = new TiingoFetcher(ValidApiKey, client, "https://api.tiingo.com");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync(new[] { new Asset("AAPL") }, CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task HttpError_ThrowsMarketDataRetrievalException()
    {
        using var client = CreateMockClient("Not Found", HttpStatusCode.NotFound);
        using var fetcher = new TiingoFetcher(ValidApiKey, client, "https://api.tiingo.com");

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchMarketDataAsync(new[] { new Asset("INVALID") }, CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<MarketDataRetrievalException>()
            .WithMessage("*HTTP 404*");
    }

    [Fact]
    public async Task InvalidJson_ThrowsMarketDataRetrievalException()
    {
        using var client = CreateMockClient("{not valid json!!!");
        using var fetcher = new TiingoFetcher(ValidApiKey, client, "https://api.tiingo.com");

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchMarketDataAsync(new[] { new Asset("AAPL") }, CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<MarketDataRetrievalException>()
            .WithMessage("*deserialize*");
    }

    [Fact]
    public void Constructor_NullApiKey_ThrowsArgumentException()
    {
        var act = () => new TiingoFetcher(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyApiKey_ThrowsArgumentException()
    {
        var act = () => new TiingoFetcher("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FetchFxRatesAsync_ThrowsNotSupportedException()
    {
        using var client = CreateMockClient("[]");
        using var fetcher = new TiingoFetcher(ValidApiKey, client, "https://api.tiingo.com");

        var act = () => fetcher.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Dispose_OwnsClient_DisposesHttpClient()
    {
        // When no HttpClient is injected, TiingoFetcher creates and owns one.
        // Calling Dispose should not throw.
        var fetcher = new TiingoFetcher(ValidApiKey);
        var act = fetcher.Dispose;

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_InjectedClient_DoesNotDispose()
    {
        var handler = CreateMockHandler(SingleDayJson);
        var client = new HttpClient(handler);
        var fetcher = new TiingoFetcher(ValidApiKey, client, "https://api.tiingo.com");

        fetcher.Dispose();

        // Client should still be usable after fetcher disposal since it was injected
        var response = await client.GetAsync("https://api.tiingo.com/test");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        client.Dispose();
    }

    [Fact]
    public async Task FetchMarketDataAsync_ApiKeyInHeader_NotInUrl()
    {
        var handler = CreateMockHandler(SingleDayJson);
        var client = new HttpClient(handler);
        using var fetcher = new TiingoFetcher(ValidApiKey, client, "https://api.tiingo.com");

        await foreach (var _ in fetcher.FetchMarketDataAsync(new[] { new Asset("AAPL") }, CancellationToken.None))
        {
        }

        handler.LastRequest!.RequestUri!.ToString().Should().NotContain(ValidApiKey);
        handler.LastRequest.Headers.GetValues("Authorization").Should().Contain($"Token {ValidApiKey}");
    }

    [Fact]
    public async Task FetchMarketDataAsync_DateParsing_HandlesUtcFormats()
    {
        // The fixture uses +00:00 offset — verify it parses to the correct DateOnly
        using var client = CreateMockClient(SingleDayJson);
        using var fetcher = new TiingoFetcher(ValidApiKey, client, "https://api.tiingo.com");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync(new[] { new Asset("AAPL") }, CancellationToken.None))
        {
            results.Add(item);
        }

        results[0].Key.Should().Be(new DateOnly(2024, 1, 15));
        results[0].Value[new Asset("AAPL")].Timestamp.Should().Be(new DateOnly(2024, 1, 15));
    }

    // R2I-03: Verify per-request headers don't mutate the injected HttpClient
    [Fact]
    public async Task FetchMarketDataAsync_DoesNotMutateInjectedClientHeaders()
    {
        var handler = CreateMockHandler(SingleDayJson);
        var client = new HttpClient(handler);
        using var fetcher = new TiingoFetcher(ValidApiKey, client, "https://api.tiingo.com");

        await foreach (var _ in fetcher.FetchMarketDataAsync(new[] { new Asset("AAPL") }, CancellationToken.None))
        {
        }

        // The injected client's DefaultRequestHeaders should NOT have Authorization
        client.DefaultRequestHeaders.Contains("Authorization").Should().BeFalse(
            "auth headers should be per-request, not on the shared client");
        // But the request itself should have it
        handler.LastRequest!.Headers.GetValues("Authorization").Should().Contain($"Token {ValidApiKey}");

        client.Dispose();
    }
}
