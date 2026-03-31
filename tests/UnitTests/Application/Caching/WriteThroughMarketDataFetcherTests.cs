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

using Boutquin.Trading.Application.Caching;
using Boutquin.Trading.Data.CSV;

namespace Boutquin.Trading.Tests.UnitTests.Application.Caching;

/// <summary>
/// Tests for WriteThroughMarketDataFetcher L2 CSV cache decorator.
/// </summary>
public sealed class WriteThroughMarketDataFetcherTests : IDisposable
{
    private static readonly Asset s_aapl = new("AAPL");
    private static readonly Asset s_msft = new("MSFT");
    private static readonly DateOnly s_day1 = new(2024, 1, 15);
    private static readonly DateOnly s_day2 = new(2024, 1, 16);

    private readonly string _tempDir;

    public WriteThroughMarketDataFetcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WriteThroughTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static MarketData CreateMarketData(DateOnly date, decimal close = 150m) =>
        new(date, 100m, 200m, 50m, close, close, 1_000_000, 0m, 1m);

    private static List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>> CreateMarketDataList(Asset asset)
    {
        return
        [
            new(s_day1, new SortedDictionary<Asset, MarketData> { [asset] = CreateMarketData(s_day1, 150m) }),
            new(s_day2, new SortedDictionary<Asset, MarketData> { [asset] = CreateMarketData(s_day2, 155m) })
        ];
    }

    private static List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> CreateFxRatesList()
    {
        return
        [
            new(s_day1, new SortedDictionary<CurrencyCode, decimal> { [CurrencyCode.EUR] = 0.85m }),
            new(s_day2, new SortedDictionary<CurrencyCode, decimal> { [CurrencyCode.EUR] = 0.86m })
        ];
    }

    private void PreCreateMarketDataCsv(Asset asset)
    {
        var filePath = MarketDataFileNameHelper.GetCsvFileNameForMarketData(_tempDir, asset.Ticker);
        File.WriteAllText(filePath,
            "Timestamp,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient\n" +
            "2024-01-15,100,200,50,150,150,1000000,0,1\n" +
            "2024-01-16,100,200,50,155,155,1000000,0,1\n");
    }

    private void PreCreateFxCsv(string pair)
    {
        var filePath = MarketDataFileNameHelper.GetCsvFileNameForFxRateData(_tempDir, pair);
        File.WriteAllText(filePath,
            "Date,Rate\n" +
            "2024-01-15,0.85\n" +
            "2024-01-16,0.86\n");
    }

    /// <summary>
    /// L2 hit: Pre-created CSV file — decorator reads from CSV, never calls API fetcher.
    /// </summary>
    [Fact]
    public async Task FetchMarketDataAsync_L2Hit_ReadsFromCsv_NeverCallsApi()
    {
        // Arrange
        PreCreateMarketDataCsv(s_aapl);
        var apiFetcher = new Mock<IMarketDataFetcher>();

        using var sut = new WriteThroughMarketDataFetcher(apiFetcher.Object, _tempDir);

        // Act
        var result = await sut.FetchMarketDataAsync([s_aapl], CancellationToken.None).ToListAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Value[s_aapl].Close.Should().Be(150m);
        apiFetcher.Verify(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// L2 miss + write-through: No CSV exists — decorator calls API, writes CSV, returns data.
    /// </summary>
    [Fact]
    public async Task FetchMarketDataAsync_L2Miss_CallsApiAndWritesCsv()
    {
        // Arrange
        var apiFetcher = new Mock<IMarketDataFetcher>();
        apiFetcher.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateMarketDataList(s_aapl).ToAsyncEnumerable());

        using var sut = new WriteThroughMarketDataFetcher(apiFetcher.Object, _tempDir);

        // Act
        var result = await sut.FetchMarketDataAsync([s_aapl], CancellationToken.None).ToListAsync();

        // Assert — data returned
        result.Should().HaveCount(2);
        // Assert — CSV written
        var csvPath = MarketDataFileNameHelper.GetCsvFileNameForMarketData(_tempDir, s_aapl.Ticker);
        File.Exists(csvPath).Should().BeTrue();
        apiFetcher.Verify(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Round-trip: Write-through creates CSV, subsequent call reads from CSV, data matches.
    /// </summary>
    [Fact]
    public async Task FetchMarketDataAsync_RoundTrip_SecondCallReadsCsv()
    {
        // Arrange
        var apiFetcher = new Mock<IMarketDataFetcher>();
        apiFetcher.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateMarketDataList(s_aapl).ToAsyncEnumerable());

        using var sut = new WriteThroughMarketDataFetcher(apiFetcher.Object, _tempDir);

        // Act — first call: API miss, writes CSV
        var result1 = await sut.FetchMarketDataAsync([s_aapl], CancellationToken.None).ToListAsync();
        // Act — second call: should read from CSV (L2 hit)
        var result2 = await sut.FetchMarketDataAsync([s_aapl], CancellationToken.None).ToListAsync();

        // Assert
        result1.Should().HaveCount(2);
        result2.Should().HaveCount(2);
        result2[0].Value[s_aapl].Close.Should().Be(result1[0].Value[s_aapl].Close);
        // API called only once (first miss)
        apiFetcher.Verify(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Partial cache: Some symbols cached (CSV exists), some not — API called only for missing.
    /// </summary>
    [Fact]
    public async Task FetchMarketDataAsync_PartialCache_ApiCalledOnlyForMissing()
    {
        // Arrange — pre-create CSV for AAPL only
        PreCreateMarketDataCsv(s_aapl);

        var apiFetcher = new Mock<IMarketDataFetcher>();
        apiFetcher.Setup(f => f.FetchMarketDataAsync(
                It.Is<IEnumerable<Asset>>(s => s.Any(a => a.Ticker == "MSFT")),
                It.IsAny<CancellationToken>()))
            .Returns(CreateMarketDataList(s_msft).ToAsyncEnumerable());

        using var sut = new WriteThroughMarketDataFetcher(apiFetcher.Object, _tempDir);

        // Act
        var result = await sut.FetchMarketDataAsync([s_aapl, s_msft], CancellationToken.None).ToListAsync();

        // Assert — got data for both symbols
        result.Should().NotBeEmpty();
        // API was called only for MSFT (missing)
        apiFetcher.Verify(f => f.FetchMarketDataAsync(
            It.Is<IEnumerable<Asset>>(s => s.Count() == 1 && s.First().Ticker == "MSFT"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// FX rate L2 hit: Pre-created CSV file — reads from CSV.
    /// </summary>
    [Fact]
    public async Task FetchFxRatesAsync_L2Hit_ReadsFromCsv()
    {
        // Arrange
        PreCreateFxCsv("USD_EUR");
        var apiFetcher = new Mock<IMarketDataFetcher>();

        using var sut = new WriteThroughMarketDataFetcher(apiFetcher.Object, _tempDir);

        // Act
        var result = await sut.FetchFxRatesAsync(["USD_EUR"], CancellationToken.None).ToListAsync();

        // Assert
        result.Should().HaveCount(2);
        apiFetcher.Verify(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// FX rate L2 miss: No CSV — calls API, writes CSV.
    /// </summary>
    [Fact]
    public async Task FetchFxRatesAsync_L2Miss_CallsApiAndWritesCsv()
    {
        // Arrange
        var apiFetcher = new Mock<IMarketDataFetcher>();
        apiFetcher.Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateFxRatesList().ToAsyncEnumerable());

        using var sut = new WriteThroughMarketDataFetcher(apiFetcher.Object, _tempDir);

        // Act
        var result = await sut.FetchFxRatesAsync(["USD_EUR"], CancellationToken.None).ToListAsync();

        // Assert
        result.Should().HaveCount(2);
        var csvPath = MarketDataFileNameHelper.GetCsvFileNameForFxRateData(_tempDir, "USD_EUR");
        File.Exists(csvPath).Should().BeTrue();
    }

    /// <summary>
    /// Constructor throws on null inner fetcher.
    /// </summary>
    [Fact]
    public void Constructor_NullInner_ThrowsArgumentNullException()
    {
        var act = () => new WriteThroughMarketDataFetcher(null!, _tempDir);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Constructor throws on null data directory.
    /// </summary>
    [Fact]
    public void Constructor_NullDataDirectory_ThrowsArgumentNullException()
    {
        var inner = new Mock<IMarketDataFetcher>();
        var act = () => new WriteThroughMarketDataFetcher(inner.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
