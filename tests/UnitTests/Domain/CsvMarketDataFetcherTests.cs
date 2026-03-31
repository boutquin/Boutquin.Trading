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
        // OHLC adjusted by factor = AdjustedClose / Close = 185.62 / 185.92
        var factor = 185.62m / 185.92m;
        md.Open.Should().BeApproximately(185.09m * factor, 0.01m);
        md.Close.Should().BeApproximately(185.92m * factor, 0.01m);
        md.AdjustedClose.Should().Be(185.62m);
        md.Volume.Should().Be(44234500L);
    }

    [Fact]
    public async Task FetchMarketDataAsync_AdjustsOhlcToMatchAdjustedCloseScale()
    {
        // When AdjustedClose != Close (due to cumulative dividends/splits),
        // OHLC must be scaled by adjustmentFactor = AdjustedClose / Close.
        // Raw: Open=83.4, High=85.5, Low=83.0, Close=85.0, AdjustedClose=28.17
        // Factor = 28.17 / 85.0 = 0.33141176...
        // Adjusted Open  = 83.4 × factor ≈ 27.64
        // Adjusted High  = 85.5 × factor ≈ 28.34
        // Adjusted Low   = 83.0 × factor ≈ 27.51
        WriteMarketDataCsv("VTI", """
            Timestamp,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient
            2003-01-02,83.4,85.5,83.0,85.0,28.17,5000000,0,1.0
            """);

        var fetcher = new CsvMarketDataFetcher(_testDir);
        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync([new Asset("VTI")], CancellationToken.None))
        {
            results.Add(item);
        }

        var md = results[0].Value[new Asset("VTI")];
        var factor = 28.17m / 85.0m;

        // OHLC should be on the same scale as AdjustedClose
        md.Open.Should().BeApproximately(83.4m * factor, 0.01m);
        md.High.Should().BeApproximately(85.5m * factor, 0.01m);
        md.Low.Should().BeApproximately(83.0m * factor, 0.01m);
        // Close should also be adjusted (Close × factor == AdjustedClose)
        md.Close.Should().BeApproximately(28.17m, 0.01m);
        md.AdjustedClose.Should().Be(28.17m);
    }

    [Fact]
    public async Task FetchMarketDataAsync_NoAdjustmentNeeded_WhenCloseEqualsAdjustedClose()
    {
        // When Close == AdjustedClose, factor = 1.0, OHLC unchanged
        WriteMarketDataCsv("SPY", """
            Timestamp,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient
            2024-01-15,475.00,477.50,474.00,476.50,476.50,30000000,0,1.0
            """);

        var fetcher = new CsvMarketDataFetcher(_testDir);
        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync([new Asset("SPY")], CancellationToken.None))
        {
            results.Add(item);
        }

        var md = results[0].Value[new Asset("SPY")];
        md.Open.Should().Be(475.00m);
        md.High.Should().Be(477.50m);
        md.Low.Should().Be(474.00m);
        md.Close.Should().Be(476.50m);
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
    public async Task FetchMarketDataAsync_MultiSymbol_AggregatesByDate()
    {
        // Two symbols sharing the same dates — each yielded dictionary must contain both symbols
        WriteMarketDataCsv("AAPL", """
            Timestamp,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient
            2024-01-15,185.09,186.42,183.55,185.92,185.92,44234500,0,1.0
            2024-01-16,186.00,187.00,185.00,186.50,186.50,40000000,0,1.0
            """);
        WriteMarketDataCsv("MSFT", """
            Timestamp,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient
            2024-01-15,370.00,372.00,368.00,371.00,371.00,25000000,0,1.0
            2024-01-16,371.50,373.00,370.00,372.50,372.50,22000000,0,1.0
            """);

        var fetcher = new CsvMarketDataFetcher(_testDir);
        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in fetcher.FetchMarketDataAsync(
                           [new Asset("AAPL"), new Asset("MSFT")], CancellationToken.None))
        {
            results.Add(item);
        }

        // Should yield exactly 2 date entries (one per date), not 4 (one per row)
        results.Should().HaveCount(2);

        // Each date entry should contain both symbols
        results[0].Key.Should().Be(new DateOnly(2024, 1, 15));
        results[0].Value.Should().ContainKey(new Asset("AAPL"));
        results[0].Value.Should().ContainKey(new Asset("MSFT"));
        results[0].Value.Should().HaveCount(2);

        results[1].Key.Should().Be(new DateOnly(2024, 1, 16));
        results[1].Value.Should().ContainKey(new Asset("AAPL"));
        results[1].Value.Should().ContainKey(new Asset("MSFT"));
        results[1].Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task FetchFxRatesAsync_MultiPair_AggregatesByDate()
    {
        // Two currency pairs sharing the same dates — each yielded dictionary must contain both
        WriteFxRateCsv("USD_EUR", """
            Timestamp,Rate
            2024-01-15,0.9123
            2024-01-16,0.9150
            """);
        WriteFxRateCsv("USD_GBP", """
            Timestamp,Rate
            2024-01-15,0.7890
            2024-01-16,0.7910
            """);

        var fetcher = new CsvMarketDataFetcher(_testDir);
        var results = new List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>();
        await foreach (var item in fetcher.FetchFxRatesAsync(["USD_EUR", "USD_GBP"], CancellationToken.None))
        {
            results.Add(item);
        }

        // Should yield exactly 2 date entries, not 4
        results.Should().HaveCount(2);

        // Each date entry should contain both currency codes
        results[0].Key.Should().Be(new DateOnly(2024, 1, 15));
        results[0].Value.Should().ContainKey(CurrencyCode.EUR);
        results[0].Value.Should().ContainKey(CurrencyCode.GBP);
        results[0].Value.Should().HaveCount(2);

        results[1].Key.Should().Be(new DateOnly(2024, 1, 16));
        results[1].Value.Should().ContainKey(CurrencyCode.EUR);
        results[1].Value.Should().ContainKey(CurrencyCode.GBP);
        results[1].Value.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_NullDirectory_ThrowsArgumentNullException()
    {
        var act = () => new CsvMarketDataFetcher(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
