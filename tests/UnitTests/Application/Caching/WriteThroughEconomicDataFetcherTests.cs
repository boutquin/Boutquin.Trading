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

namespace Boutquin.Trading.Tests.UnitTests.Application.Caching;

/// <summary>
/// Tests for WriteThroughEconomicDataFetcher L2 CSV cache decorator.
/// </summary>
public sealed class WriteThroughEconomicDataFetcherTests : IDisposable
{
    private readonly string _tempDir;

    public WriteThroughEconomicDataFetcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WriteThroughEconTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static List<KeyValuePair<DateOnly, decimal>> CreateSeriesData()
    {
        return
        [
            new(new DateOnly(2024, 1, 15), 4.25m),
            new(new DateOnly(2024, 1, 16), 4.30m),
            new(new DateOnly(2024, 1, 17), 4.28m)
        ];
    }

    private void PreCreateCsv(string seriesId)
    {
        var filePath = Path.Combine(_tempDir, $"fred_{seriesId}.csv");
        File.WriteAllText(filePath,
            "Date,Value\n" +
            "2024-01-15,4.25\n" +
            "2024-01-16,4.30\n" +
            "2024-01-17,4.28\n");
    }

    [Fact]
    public async Task FetchSeriesAsync_L2Hit_ReadsFromCsv_NeverCallsApi()
    {
        PreCreateCsv("DGS10");
        var apiFetcher = new Mock<IEconomicDataFetcher>();

        using var sut = new WriteThroughEconomicDataFetcher(apiFetcher.Object, _tempDir);
        var result = await sut.FetchSeriesAsync("DGS10").ToListAsync();

        result.Should().HaveCount(3);
        apiFetcher.Verify(f => f.FetchSeriesAsync(It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FetchSeriesAsync_L2Miss_CallsApiAndWritesCsv()
    {
        var apiFetcher = new Mock<IEconomicDataFetcher>();
        apiFetcher.Setup(f => f.FetchSeriesAsync("DGS10", It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateSeriesData().ToAsyncEnumerable());

        using var sut = new WriteThroughEconomicDataFetcher(apiFetcher.Object, _tempDir);
        var result = await sut.FetchSeriesAsync("DGS10").ToListAsync();

        result.Should().HaveCount(3);
        File.Exists(Path.Combine(_tempDir, "fred_DGS10.csv")).Should().BeTrue();
    }

    [Fact]
    public async Task FetchSeriesAsync_RoundTrip_SecondCallReadsCsv()
    {
        var apiFetcher = new Mock<IEconomicDataFetcher>();
        apiFetcher.Setup(f => f.FetchSeriesAsync("DGS10", It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateSeriesData().ToAsyncEnumerable());

        using var sut = new WriteThroughEconomicDataFetcher(apiFetcher.Object, _tempDir);
        await sut.FetchSeriesAsync("DGS10").ToListAsync();
        var result2 = await sut.FetchSeriesAsync("DGS10").ToListAsync();

        result2.Should().HaveCount(3);
        apiFetcher.Verify(f => f.FetchSeriesAsync("DGS10", It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_NullInner_ThrowsArgumentNullException()
    {
        var act = () => new WriteThroughEconomicDataFetcher(null!, _tempDir);
        act.Should().Throw<ArgumentNullException>();
    }
}
