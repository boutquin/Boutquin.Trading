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
using Boutquin.Trading.Data.Fred;
using Boutquin.Trading.Domain.Exceptions;

namespace Boutquin.Trading.Tests.UnitTests.Data;

public sealed class FredFetcherTests
{
    private const string ValidApiKey = "test-fred-api-key-0123456789abcdef";

    private const string HappyPathJson = """
        {
          "observations": [
            {"realtime_start":"2024-01-01","realtime_end":"2024-12-31","date":"2024-01-02","value":"5.33"},
            {"realtime_start":"2024-01-01","realtime_end":"2024-12-31","date":"2024-01-03","value":"."},
            {"realtime_start":"2024-01-01","realtime_end":"2024-12-31","date":"2024-01-04","value":"5.31"}
          ]
        }
        """;

    private const string SingleObservationJson = """
        {
          "observations": [
            {"realtime_start":"2024-01-01","realtime_end":"2024-12-31","date":"2024-06-15","value":"4.25"}
          ]
        }
        """;

    private const string EmptyObservationsJson = """
        {
          "observations": []
        }
        """;

    private const string UnparseableValueJson = """
        {
          "observations": [
            {"realtime_start":"2024-01-01","realtime_end":"2024-12-31","date":"2024-01-02","value":"N/A"},
            {"realtime_start":"2024-01-01","realtime_end":"2024-12-31","date":"2024-01-03","value":"5.33"}
          ]
        }
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

    private static async Task<List<KeyValuePair<DateOnly, decimal>>> CollectAsync(
        IAsyncEnumerable<KeyValuePair<DateOnly, decimal>> stream)
    {
        var results = new List<KeyValuePair<DateOnly, decimal>>();
        await foreach (var item in stream)
        {
            results.Add(item);
        }

        return results;
    }

    [Fact]
    public async Task SingleSeries_ReturnsObservations()
    {
        using var client = CreateMockClient(HappyPathJson);
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        var results = await CollectAsync(fetcher.FetchSeriesAsync("DGS3MO"));

        // Jan 3 has "." (missing) — should be skipped
        results.Should().HaveCount(2);
        results[0].Key.Should().Be(new DateOnly(2024, 1, 2));
        results[1].Key.Should().Be(new DateOnly(2024, 1, 4));
    }

    [Fact]
    public async Task MapsFieldsCorrectly()
    {
        using var client = CreateMockClient(SingleObservationJson);
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        var results = await CollectAsync(fetcher.FetchSeriesAsync("DGS10"));

        results.Should().HaveCount(1);
        results[0].Key.Should().Be(new DateOnly(2024, 6, 15));
        results[0].Value.Should().Be(4.25m);
    }

    [Fact]
    public async Task MissingValues_AreSkipped()
    {
        using var client = CreateMockClient(HappyPathJson);
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        var results = await CollectAsync(fetcher.FetchSeriesAsync("DGS3MO"));

        results.Should().HaveCount(2);
        results.Should().NotContain(kvp => kvp.Key == new DateOnly(2024, 1, 3));
    }

    [Fact]
    public async Task HttpError_ThrowsMarketDataRetrievalException()
    {
        using var client = CreateMockClient("Server Error", HttpStatusCode.InternalServerError);
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        var act = async () => await CollectAsync(fetcher.FetchSeriesAsync("DGS3MO"));

        await act.Should().ThrowAsync<MarketDataRetrievalException>()
            .WithMessage("*HTTP 500*");
    }

    [Fact]
    public async Task InvalidJson_ThrowsMarketDataRetrievalException()
    {
        using var client = CreateMockClient("{not valid json!!!");
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        var act = async () => await CollectAsync(fetcher.FetchSeriesAsync("DGS3MO"));

        await act.Should().ThrowAsync<MarketDataRetrievalException>()
            .WithMessage("*deserialize*");
    }

    [Fact]
    public async Task NullResponse_ThrowsMarketDataRetrievalException()
    {
        using var client = CreateMockClient("{}");
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        var act = async () => await CollectAsync(fetcher.FetchSeriesAsync("DGS3MO"));

        await act.Should().ThrowAsync<MarketDataRetrievalException>()
            .WithMessage("*null*");
    }

    [Fact]
    public async Task EmptyObservations_YieldsNothing()
    {
        using var client = CreateMockClient(EmptyObservationsJson);
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        var results = await CollectAsync(fetcher.FetchSeriesAsync("DGS3MO"));

        results.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NullOrEmptySeriesId_ThrowsArgumentException(string? seriesId)
    {
        using var client = CreateMockClient(EmptyObservationsJson);
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        var act = async () => await CollectAsync(fetcher.FetchSeriesAsync(seriesId!));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CancellationToken_Respected()
    {
        using var client = CreateMockClient(HappyPathJson);
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await CollectAsync(
            fetcher.FetchSeriesAsync("DGS3MO", cancellationToken: cts.Token));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DateRange_IncludedInUrl()
    {
        var handler = CreateMockHandler(SingleObservationJson);
        var client = new HttpClient(handler);
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 12, 31);

        await CollectAsync(fetcher.FetchSeriesAsync("DGS10", start, end));

        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("observation_start=2024-01-01");
        url.Should().Contain("observation_end=2024-12-31");
    }

    [Fact]
    public async Task UrlEncodesSeriesId()
    {
        var handler = CreateMockHandler(EmptyObservationsJson);
        var client = new HttpClient(handler);
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        await CollectAsync(fetcher.FetchSeriesAsync("SERIES WITH SPACES"));

        var url = handler.LastRequest!.RequestUri!.AbsoluteUri;
        url.Should().Contain("series_id=SERIES%20WITH%20SPACES");
    }

    [Fact]
    public void DisposesHttpClient_WhenOwned()
    {
        var fetcher = new FredFetcher(ValidApiKey);
        var act = fetcher.Dispose;

        act.Should().NotThrow();
    }

    [Fact]
    public async Task DoesNotDispose_InjectedClient()
    {
        var handler = CreateMockHandler(SingleObservationJson);
        var client = new HttpClient(handler);
        var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        fetcher.Dispose();

        // Client should still be usable after fetcher disposal since it was injected
        var response = await client.GetAsync("https://api.stlouisfed.org/test");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        client.Dispose();
    }

    [Fact]
    public async Task UnparseableValue_IsSkipped()
    {
        using var client = CreateMockClient(UnparseableValueJson);
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        var results = await CollectAsync(fetcher.FetchSeriesAsync("DGS3MO"));

        results.Should().HaveCount(1);
        results[0].Value.Should().Be(5.33m);
    }

    [Fact]
    public async Task AuthKeyInQueryString()
    {
        var handler = CreateMockHandler(SingleObservationJson);
        var client = new HttpClient(handler);
        using var fetcher = new FredFetcher(ValidApiKey, client, "https://api.stlouisfed.org");

        await CollectAsync(fetcher.FetchSeriesAsync("DGS10"));

        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain($"api_key={ValidApiKey}");
    }

    [Fact]
    public void Constructor_NullApiKey_ThrowsArgumentException()
    {
        var act = () => new FredFetcher(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyApiKey_ThrowsArgumentException()
    {
        var act = () => new FredFetcher("   ");

        act.Should().Throw<ArgumentException>();
    }
}
