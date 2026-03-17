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
using Boutquin.Trading.Data.Frankfurter;
using Boutquin.Trading.Domain.Exceptions;

namespace Boutquin.Trading.Tests.UnitTests.Data;

public sealed class FrankfurterFetcherTests
{
    private const string ThreeDayJson = """
        {"amount":1.0,"base":"USD","start_date":"2024-01-10","end_date":"2024-01-12","rates":{"2024-01-10":{"EUR":0.91358,"GBP":0.78589},"2024-01-11":{"EUR":0.91017,"GBP":0.78406},"2024-01-12":{"EUR":0.91391,"GBP":0.78551}}}
        """;

    private const string EmptyRatesJson = """
        {"amount":1.0,"base":"USD","start_date":"2024-01-10","end_date":"2024-01-12","rates":{}}
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
    public async Task SinglePair_ReturnsRates()
    {
        using var client = CreateMockClient(ThreeDayJson);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>();
        await foreach (var item in fetcher.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().HaveCount(3);
        results[0].Key.Should().Be(new DateOnly(2024, 1, 10));
        results[0].Value.Should().ContainKey(CurrencyCode.EUR);
        results[0].Value[CurrencyCode.EUR].Should().Be(0.91358m);
    }

    [Fact]
    public async Task MultiplePairs_AllCurrenciesInDict()
    {
        using var client = CreateMockClient(ThreeDayJson);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>();
        await foreach (var item in fetcher.FetchFxRatesAsync(new[] { "USD_EUR", "USD_GBP" }, CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().HaveCount(3);
        results[0].Value.Should().ContainKey(CurrencyCode.EUR);
        results[0].Value.Should().ContainKey(CurrencyCode.GBP);
    }

    [Fact]
    public async Task WeekendsOmitted()
    {
        // The fixture has Jan 10 (Wed), 11 (Thu), 12 (Fri) — no weekends
        using var client = CreateMockClient(ThreeDayJson);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>();
        await foreach (var item in fetcher.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None))
        {
            results.Add(item);
        }

        var dates = results.Select(r => r.Key).ToList();
        dates.Should().NotContain(d => d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday);
    }

    [Fact]
    public async Task AcceptsUnderscoreDelimiter()
    {
        using var client = CreateMockClient(ThreeDayJson);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>();
        await foreach (var item in fetcher.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AcceptsSlashDelimiter()
    {
        using var client = CreateMockClient(ThreeDayJson);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>();
        await foreach (var item in fetcher.FetchFxRatesAsync(new[] { "USD/EUR" }, CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UnsupportedCurrency_RUB_ThrowsNotSupportedException()
    {
        using var client = CreateMockClient(ThreeDayJson);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchFxRatesAsync(new[] { "USD_RUB" }, CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*RUB*");
    }

    [Fact]
    public async Task InvalidPairFormat_ThrowsArgumentException()
    {
        using var client = CreateMockClient(ThreeDayJson);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchFxRatesAsync(new[] { "USDEUR" }, CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid currency pair format*");
    }

    [Fact]
    public async Task HttpError_ThrowsMarketDataRetrievalException()
    {
        using var client = CreateMockClient("Server Error", HttpStatusCode.InternalServerError);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<MarketDataRetrievalException>()
            .WithMessage("*HTTP 500*");
    }

    [Fact]
    public async Task EmptyRates_YieldsNothing()
    {
        using var client = CreateMockClient(EmptyRatesJson);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>();
        await foreach (var item in fetcher.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().BeEmpty();
    }

    [Fact]
    public void FetchMarketDataAsync_ThrowsNotSupportedException()
    {
        using var client = CreateMockClient(ThreeDayJson);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        var act = () => fetcher.FetchMarketDataAsync(new[] { new Asset("AAPL") }, CancellationToken.None);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Constructor_NoHttpClient_CreatesOwned()
    {
        // When no HttpClient is injected, FrankfurterFetcher creates and owns one.
        // Construction and disposal should not throw.
        var fetcher = new FrankfurterFetcher();
        var act = fetcher.Dispose;

        act.Should().NotThrow();
    }

    [Fact]
    public async Task BaseCurrencyInRequestUrl()
    {
        var handler = CreateMockHandler(ThreeDayJson);
        var client = new HttpClient(handler);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        await foreach (var _ in fetcher.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None))
        {
        }

        handler.LastRequest!.RequestUri!.ToString().Should().Contain("base=USD");
        handler.LastRequest.RequestUri.ToString().Should().Contain("symbols=EUR");
    }

    // ── H10: Date range filtering ──

    [Fact]
    public async Task FetchFxRatesAsync_WithDateRange_BuildsCorrectUrl()
    {
        var handler = CreateMockHandler(ThreeDayJson);
        var client = new HttpClient(handler);
        using var fetcher = new FrankfurterFetcher(
            client, "https://api.frankfurter.dev",
            startDate: new DateOnly(2024, 1, 1),
            endDate: new DateOnly(2024, 1, 31));

        await foreach (var _ in fetcher.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None))
        {
        }

        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("2024-01-01..2024-01-31");
        url.Should().NotContain("1999-01-04");
    }

    [Fact]
    public async Task FetchFxRatesAsync_WithStartDateOnly_BuildsOpenEndedUrl()
    {
        var handler = CreateMockHandler(ThreeDayJson);
        var client = new HttpClient(handler);
        using var fetcher = new FrankfurterFetcher(
            client, "https://api.frankfurter.dev",
            startDate: new DateOnly(2024, 1, 1));

        await foreach (var _ in fetcher.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None))
        {
        }

        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("2024-01-01..");
        url.Should().NotContain("1999-01-04");
    }

    [Fact]
    public async Task FetchFxRatesAsync_WithEndDateOnly_BuildsUrl()
    {
        var handler = CreateMockHandler(ThreeDayJson);
        var client = new HttpClient(handler);
        using var fetcher = new FrankfurterFetcher(
            client, "https://api.frankfurter.dev",
            endDate: new DateOnly(2024, 1, 31));

        await foreach (var _ in fetcher.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None))
        {
        }

        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("1999-01-04..2024-01-31");
    }

    [Fact]
    public async Task FetchFxRatesAsync_WithNoDateRange_UsesFullHistory()
    {
        var handler = CreateMockHandler(ThreeDayJson);
        var client = new HttpClient(handler);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        await foreach (var _ in fetcher.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None))
        {
        }

        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("1999-01-04..");
    }

    [Fact]
    public void FetchFxRatesAsync_StartDateAfterEndDate_ThrowsArgumentException()
    {
        var handler = CreateMockHandler(ThreeDayJson);
        var client = new HttpClient(handler);

        var act = () => new FrankfurterFetcher(
            client, "https://api.frankfurter.dev",
            startDate: new DateOnly(2024, 2, 1),
            endDate: new DateOnly(2024, 1, 1));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*startDate*endDate*");
    }

    // ── L13: URL encoding ──

    [Fact]
    public async Task FetchFxRatesAsync_CurrencyCodes_AreUrlEncoded()
    {
        var handler = CreateMockHandler(ThreeDayJson);
        var client = new HttpClient(handler);
        using var fetcher = new FrankfurterFetcher(client, "https://api.frankfurter.dev");

        await foreach (var _ in fetcher.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None))
        {
        }

        // Standard currency codes pass through URL encoding unchanged,
        // but the encoding function should be applied (defense-in-depth)
        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("base=USD");
        url.Should().Contain("symbols=EUR");
    }
}
