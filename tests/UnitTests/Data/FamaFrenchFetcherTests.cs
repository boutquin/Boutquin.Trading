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

using System.IO.Compression;
using System.Net;
using Boutquin.Trading.Data.FamaFrench;
using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Helpers;

namespace Boutquin.Trading.Tests.UnitTests.Data;

public sealed class FamaFrenchFetcherTests
{
    private const string BaseUrl = "https://test.example.com/ftp";

    private const string ThreeFactorDailyCsv = """
        This file was created by using the 202601 CRSP database.
        Description line 2.

        ,Mkt-RF,SMB,HML,RF
        19630701,   -0.67,    0.00,   -0.34,    0.01
        19630702,    0.79,   -0.26,    0.26,    0.01
        19630703,    0.63,   -0.17,   -0.09,    0.01

        Copyright 2026 Eugene F. Fama and Kenneth R. French
        """;

    private const string ThreeFactorMonthlyCsv = """
        This file was created using the 202601 CRSP database.

        ,Mkt-RF,SMB,HML,RF
        192607,   2.89,  -2.55,  -2.39,   0.22
        192608,   2.64,  -1.14,   3.81,   0.25
        192609,   0.38,  -1.36,   0.05,   0.23

         Annual Factors: January-December
        ,Mkt-RF,SMB,HML,RF
          1927,  29.44,  -2.20,  -4.58,   3.12
          1928,  35.56,   3.73,  -5.26,   3.56

        Copyright 2026 Eugene F. Fama and Kenneth R. French
        """;

    private const string FiveFactorDailyCsv = """
        Description.

        ,Mkt-RF,SMB,HML,RMW,CMA,RF
        19630701,   -0.67,    0.00,   -0.34,   -0.01,    0.16,    0.01
        19630702,    0.79,   -0.26,    0.26,   -0.07,   -0.20,    0.01

        Copyright 2026 Eugene F. Fama and Kenneth R. French
        """;

    private const string MomentumDailyCsv = """
        Description.
        Missing data are indicated by -99.99 or -999.

        ,Mom
        19261103,   0.54
        19261104,  -0.37
        19261105, -99.99

        Copyright 2026 Eugene F. Fama and Kenneth R. French
        """;

    private const string HeaderOnlyCsv = """
        Description.

        ,Mkt-RF,SMB,HML,RF

        Copyright 2026 Eugene F. Fama and Kenneth R. French
        """;

    private const string NoHeaderCsv = """
        This file has no header line starting with comma.
        Just some random text.
        """;

    private static byte[] CreateZipWithCsv(string csvContent)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("data.csv");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(csvContent);
        }

        return memoryStream.ToArray();
    }

    private static byte[] CreateEmptyZip()
    {
        using var memoryStream = new MemoryStream();
        using (var _ = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // no entries
        }

        return memoryStream.ToArray();
    }

    private static HttpClient CreateMockClient(byte[] responseBytes, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new MockHttpMessageHandler(responseBytes, statusCode);
        return new HttpClient(handler);
    }

    private static MockHttpMessageHandler CreateMockHandler(byte[] responseBytes, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new MockHttpMessageHandler(responseBytes, statusCode);
    }

    private static async Task<List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>> CollectAsync(
        IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> stream)
    {
        var results = new List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>();
        await foreach (var item in stream)
        {
            results.Add(item);
        }

        return results;
    }

    // --- Happy path tests ---

    [Fact]
    public async Task FetchDailyAsync_ThreeFactors_ReturnsCorrectFactors()
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorDailyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var results = await CollectAsync(fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors));

        results.Should().HaveCount(3);

        results[0].Key.Should().Be(new DateOnly(1963, 7, 1));
        results[0].Value[FamaFrenchConstants.MarketExcessReturn].Should().Be(-0.67m);
        results[0].Value[FamaFrenchConstants.SmallMinusBig].Should().Be(0.00m);
        results[0].Value[FamaFrenchConstants.HighMinusLow].Should().Be(-0.34m);
        results[0].Value[FamaFrenchConstants.RiskFreeRate].Should().Be(0.01m);

        results[1].Key.Should().Be(new DateOnly(1963, 7, 2));
        results[1].Value[FamaFrenchConstants.MarketExcessReturn].Should().Be(0.79m);

        results[2].Key.Should().Be(new DateOnly(1963, 7, 3));
    }

    [Fact]
    public async Task FetchDailyAsync_FiveFactors_ReturnsAllSixColumns()
    {
        var zipBytes = CreateZipWithCsv(FiveFactorDailyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var results = await CollectAsync(fetcher.FetchDailyAsync(FamaFrenchDataset.FiveFactors));

        results.Should().HaveCount(2);
        results[0].Value.Should().HaveCount(6);
        results[0].Value[FamaFrenchConstants.RobustMinusWeak].Should().Be(-0.01m);
        results[0].Value[FamaFrenchConstants.ConservativeMinusAggressive].Should().Be(0.16m);
    }

    [Fact]
    public async Task FetchDailyAsync_Momentum_ReturnsSingleFactor()
    {
        var zipBytes = CreateZipWithCsv(MomentumDailyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var results = await CollectAsync(fetcher.FetchDailyAsync(FamaFrenchDataset.Momentum));

        // Third row has -99.99, should be skipped
        results.Should().HaveCount(2);
        results[0].Value.Should().HaveCount(1);
        results[0].Value[FamaFrenchConstants.Momentum].Should().Be(0.54m);
        results[1].Value[FamaFrenchConstants.Momentum].Should().Be(-0.37m);
    }

    // --- Monthly tests ---

    [Fact]
    public async Task FetchMonthlyAsync_ThreeFactors_ExcludesAnnualSection()
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorMonthlyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var results = await CollectAsync(fetcher.FetchMonthlyAsync(FamaFrenchDataset.ThreeFactors));

        // Only 3 monthly rows, not the annual section
        results.Should().HaveCount(3);
        results[0].Value[FamaFrenchConstants.MarketExcessReturn].Should().Be(2.89m);
    }

    [Fact]
    public async Task FetchMonthlyAsync_DateUsesLastDayOfMonth()
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorMonthlyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var results = await CollectAsync(fetcher.FetchMonthlyAsync(FamaFrenchDataset.ThreeFactors));

        // 192607 → July 31, 1926
        results[0].Key.Should().Be(new DateOnly(1926, 7, 31));
        // 192608 → August 31, 1926
        results[1].Key.Should().Be(new DateOnly(1926, 8, 31));
        // 192609 → September 30, 1926
        results[2].Key.Should().Be(new DateOnly(1926, 9, 30));
    }

    // --- Date parsing ---

    [Fact]
    public async Task FetchDailyAsync_DateParsesCorrectly()
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorDailyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var results = await CollectAsync(fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors));

        results[0].Key.Should().Be(new DateOnly(1963, 7, 1));
        results[1].Key.Should().Be(new DateOnly(1963, 7, 2));
        results[2].Key.Should().Be(new DateOnly(1963, 7, 3));
    }

    // --- Missing values ---

    [Fact]
    public async Task FetchDailyAsync_MissingValues_RowSkipped()
    {
        var zipBytes = CreateZipWithCsv(MomentumDailyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var results = await CollectAsync(fetcher.FetchDailyAsync(FamaFrenchDataset.Momentum));

        results.Should().HaveCount(2);
        results.Should().NotContain(kvp => kvp.Key == new DateOnly(1926, 11, 5));
    }

    [Fact]
    public async Task FetchDailyAsync_MissingValueMinus999_RowSkipped()
    {
        const string csv = """
            Description.

            ,Mom
            19261103,   0.54
            19261104,  -999

            Copyright 2026 Eugene F. Fama and Kenneth R. French
            """;
        var zipBytes = CreateZipWithCsv(csv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var results = await CollectAsync(fetcher.FetchDailyAsync(FamaFrenchDataset.Momentum));

        results.Should().HaveCount(1);
        results[0].Value[FamaFrenchConstants.Momentum].Should().Be(0.54m);
    }

    // --- Copyright footer ---

    [Fact]
    public async Task FetchDailyAsync_CopyrightFooter_Excluded()
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorDailyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var results = await CollectAsync(fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors));

        // No result should have a date that corresponds to the copyright line
        results.Should().HaveCount(3);
    }

    // --- Description header ---

    [Fact]
    public async Task FetchDailyAsync_DescriptionHeader_Skipped()
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorDailyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var results = await CollectAsync(fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors));

        // Description lines before header should not interfere
        results.Should().HaveCount(3);
        results[0].Key.Should().Be(new DateOnly(1963, 7, 1));
    }

    // --- Leading whitespace ---

    [Fact]
    public async Task FetchDailyAsync_LeadingWhitespace_TrimmedCorrectly()
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorDailyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var results = await CollectAsync(fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors));

        // Values like "   -0.67" should parse to -0.67m
        results[0].Value[FamaFrenchConstants.MarketExcessReturn].Should().Be(-0.67m);
    }

    // --- Error handling ---

    [Fact]
    public async Task FetchDailyAsync_HttpError_ThrowsMarketDataRetrievalException()
    {
        var handler = new MockHttpMessageHandler("Server Error", HttpStatusCode.InternalServerError);
        var client = new HttpClient(handler);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var act = async () => await CollectAsync(
            fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors));

        await act.Should().ThrowAsync<MarketDataRetrievalException>();
    }

    [Fact]
    public async Task FetchDailyAsync_InvalidZip_ThrowsMarketDataRetrievalException()
    {
        var handler = new MockHttpMessageHandler(new byte[] { 0x00, 0x01, 0x02, 0x03 });
        var client = new HttpClient(handler);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var act = async () => await CollectAsync(
            fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors));

        await act.Should().ThrowAsync<MarketDataRetrievalException>();
    }

    [Fact]
    public async Task FetchDailyAsync_EmptyZip_ThrowsMarketDataRetrievalException()
    {
        var emptyZip = CreateEmptyZip();
        using var client = CreateMockClient(emptyZip);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var act = async () => await CollectAsync(
            fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors));

        await act.Should().ThrowAsync<MarketDataRetrievalException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public async Task FetchDailyAsync_NoHeaderLine_ThrowsMarketDataRetrievalException()
    {
        var zipBytes = CreateZipWithCsv(NoHeaderCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var act = async () => await CollectAsync(
            fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors));

        await act.Should().ThrowAsync<MarketDataRetrievalException>()
            .WithMessage("*header*");
    }

    // --- Empty CSV ---

    [Fact]
    public async Task FetchDailyAsync_EmptyCsv_YieldsNothing()
    {
        var zipBytes = CreateZipWithCsv(HeaderOnlyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var results = await CollectAsync(fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors));

        results.Should().BeEmpty();
    }

    // --- Cancellation ---

    [Fact]
    public async Task FetchDailyAsync_CancellationToken_Respected()
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorDailyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await CollectAsync(
            fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, cancellationToken: cts.Token));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- Date range filtering ---

    [Fact]
    public async Task FetchDailyAsync_DateRange_FiltersCorrectly()
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorDailyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var start = new DateOnly(1963, 7, 2);
        var end = new DateOnly(1963, 7, 2);

        var results = await CollectAsync(
            fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, start, end));

        results.Should().HaveCount(1);
        results[0].Key.Should().Be(new DateOnly(1963, 7, 2));
    }

    // --- Invalid dataset ---

    [Fact]
    public async Task FetchDailyAsync_InvalidDataset_ThrowsArgumentOutOfRange()
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorDailyCsv);
        using var client = CreateMockClient(zipBytes);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        var act = async () => await CollectAsync(
            fetcher.FetchDailyAsync((FamaFrenchDataset)999));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    // --- Dispose pattern ---

    [Fact]
    public void DisposesHttpClient_WhenOwned()
    {
        var fetcher = new FamaFrenchFetcher();
        var act = fetcher.Dispose;

        act.Should().NotThrow();
    }

    [Fact]
    public async Task DoesNotDispose_InjectedClient()
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorDailyCsv);
        var handler = CreateMockHandler(zipBytes);
        var client = new HttpClient(handler);
        var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        fetcher.Dispose();

        // Client should still be usable after fetcher disposal since it was injected
        var response = await client.GetAsync($"{BaseUrl}/test");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        client.Dispose();
    }

    // --- URL construction ---

    [Theory]
    [InlineData(FamaFrenchDataset.ThreeFactors, "F-F_Research_Data_Factors_daily_CSV.zip")]
    [InlineData(FamaFrenchDataset.FiveFactors, "F-F_Research_Data_5_Factors_2x3_daily_CSV.zip")]
    [InlineData(FamaFrenchDataset.Momentum, "F-F_Momentum_Factor_daily_CSV.zip")]
    public async Task FetchDailyAsync_UrlConstructedCorrectly(FamaFrenchDataset dataset, string expectedSuffix)
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorDailyCsv);
        var handler = CreateMockHandler(zipBytes);
        var client = new HttpClient(handler);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        // Use try-catch since some datasets may not match the CSV, but we only care about the URL
        try
        {
            await CollectAsync(fetcher.FetchDailyAsync(dataset));
        }
        catch
        {
            // Ignored — we only care about the URL
        }

        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain(expectedSuffix);
    }

    [Theory]
    [InlineData(FamaFrenchDataset.ThreeFactors, "F-F_Research_Data_Factors_CSV.zip")]
    [InlineData(FamaFrenchDataset.FiveFactors, "F-F_Research_Data_5_Factors_2x3_CSV.zip")]
    [InlineData(FamaFrenchDataset.Momentum, "F-F_Momentum_Factor_CSV.zip")]
    public async Task FetchMonthlyAsync_UrlConstructedCorrectly(FamaFrenchDataset dataset, string expectedSuffix)
    {
        var zipBytes = CreateZipWithCsv(ThreeFactorMonthlyCsv);
        var handler = CreateMockHandler(zipBytes);
        var client = new HttpClient(handler);
        using var fetcher = new FamaFrenchFetcher(client, BaseUrl);

        try
        {
            await CollectAsync(fetcher.FetchMonthlyAsync(dataset));
        }
        catch
        {
            // Ignored — we only care about the URL
        }

        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain(expectedSuffix);
        url.Should().NotContain("_daily");
    }
}
