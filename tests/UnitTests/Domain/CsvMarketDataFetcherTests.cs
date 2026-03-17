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

using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Helpers;

namespace Boutquin.Trading.Tests.UnitTests.Domain;

/// <summary>
/// Tests for <see cref="CsvMarketDataFetcher"/> (R2I-08).
/// </summary>
public sealed class CsvMarketDataFetcherTests : IDisposable
{
    private readonly string _testDir;

    public CsvMarketDataFetcherTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"csv_fetcher_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    private void WriteMarketDataCsv(string ticker, string content)
    {
        var filePath = MarketDataFileNameHelper.GetCsvFileNameForMarketData(_testDir, ticker);
        File.WriteAllText(filePath, content);
    }

    private void WriteFxRateCsv(string pair, string content)
    {
        var filePath = MarketDataFileNameHelper.GetCsvFileNameForFxRateData(_testDir, pair);
        File.WriteAllText(filePath, content);
    }

    [Fact]
    public async Task FetchMarketDataAsync_ValidCsv_ReturnsData()
    {
        WriteMarketDataCsv("AAPL", """
            Timestamp,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient
            2024-01-15,185.09,186.42,183.55,185.92,185.62,44234500,0.24,1.0
            """);

        var fetcher = new CsvMarketDataFetcher(_testDir);
        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync([new Asset("AAPL")], CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().HaveCount(1);
        results[0].Key.Should().Be(new DateOnly(2024, 1, 15));
        var md = results[0].Value[new Asset("AAPL")];
        md.Open.Should().Be(185.09m);
        md.Close.Should().Be(185.92m);
        md.Volume.Should().Be(44234500L);
    }

    [Fact]
    public async Task FetchMarketDataAsync_InvariantCultureParsing_HandlesDecimalPoint()
    {
        // R2I-07: Verify InvariantCulture parsing works with decimal points
        WriteMarketDataCsv("TEST", """
            Timestamp,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient
            2024-01-15,0.0001,0.0002,0.00005,0.00015,0.00015,1000000,0,1.0
            """);

        var fetcher = new CsvMarketDataFetcher(_testDir);
        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync([new Asset("TEST")], CancellationToken.None))
        {
            results.Add(item);
        }

        results[0].Value[new Asset("TEST")].Open.Should().Be(0.0001m);
    }

    [Fact]
    public async Task FetchMarketDataAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var fetcher = new CsvMarketDataFetcher(_testDir);

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchMarketDataAsync([new Asset("MISSING")], CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task FetchMarketDataAsync_MalformedRow_ThrowsMarketDataRetrievalException()
    {
        WriteMarketDataCsv("BAD", """
            Timestamp,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient
            not-a-date,bad,data
            """);

        var fetcher = new CsvMarketDataFetcher(_testDir);

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchMarketDataAsync([new Asset("BAD")], CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<MarketDataRetrievalException>();
    }

    [Fact]
    public async Task FetchMarketDataAsync_EmptyFile_ReturnsNothing()
    {
        WriteMarketDataCsv("EMPTY", """
            Timestamp,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient
            """);

        var fetcher = new CsvMarketDataFetcher(_testDir);
        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync([new Asset("EMPTY")], CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchMarketDataAsync_CancellationToken_ThrowsOperationCanceled()
    {
        WriteMarketDataCsv("AAPL", """
            Timestamp,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient
            2024-01-15,185.09,186.42,183.55,185.92,185.62,44234500,0.24,1.0
            """);

        var fetcher = new CsvMarketDataFetcher(_testDir);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchMarketDataAsync([new Asset("AAPL")], cts.Token))
            {
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FetchMarketDataAsync_NullSymbols_ThrowsArgumentNullException()
    {
        var fetcher = new CsvMarketDataFetcher(_testDir);

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchMarketDataAsync(null!, CancellationToken.None))
            {
            }
        };
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task FetchMarketDataAsync_EmptySymbols_ThrowsArgumentException()
    {
        var fetcher = new CsvMarketDataFetcher(_testDir);

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchMarketDataAsync([], CancellationToken.None))
            {
            }
        };
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task FetchFxRatesAsync_ValidCsv_ReturnsRates()
    {
        WriteFxRateCsv("USD_EUR", """
            Timestamp,Rate
            2024-01-15,0.9123
            """);

        var fetcher = new CsvMarketDataFetcher(_testDir);
        var results = new List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>();
        await foreach (var item in fetcher.FetchFxRatesAsync(["USD_EUR"], CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().HaveCount(1);
        results[0].Value[CurrencyCode.EUR].Should().Be(0.9123m);
    }

    [Fact]
    public async Task FetchFxRatesAsync_InvalidPairFormat_ThrowsArgumentException()
    {
        var fetcher = new CsvMarketDataFetcher(_testDir);

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchFxRatesAsync(["INVALID"], CancellationToken.None))
            {
            }
        };
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullDirectory_ThrowsArgumentNullException()
    {
        var act = () => new CsvMarketDataFetcher(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
