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
/// Tests for WriteThroughFactorDataFetcher L2 CSV cache decorator.
/// </summary>
public sealed class WriteThroughFactorDataFetcherTests : IDisposable
{
    private readonly string _tempDir;

    public WriteThroughFactorDataFetcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WriteThroughFFTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> CreateFactorData()
    {
        return
        [
            new(new DateOnly(2024, 1, 15), new Dictionary<string, decimal> { ["Mkt-RF"] = 0.5m, ["SMB"] = 0.1m }),
            new(new DateOnly(2024, 1, 16), new Dictionary<string, decimal> { ["Mkt-RF"] = 0.3m, ["SMB"] = -0.1m }),
            new(new DateOnly(2024, 1, 17), new Dictionary<string, decimal> { ["Mkt-RF"] = 0.7m, ["SMB"] = 0.2m })
        ];
    }

    private void PreCreateDailyCsv(FamaFrenchDataset dataset)
    {
        var filePath = Path.Combine(_tempDir, $"ff_{dataset}_daily.csv");
        File.WriteAllText(filePath,
            "Date,Mkt-RF,SMB\n" +
            "2024-01-15,0.5,0.1\n" +
            "2024-01-16,0.3,-0.1\n" +
            "2024-01-17,0.7,0.2\n");
    }

    [Fact]
    public async Task FetchDailyAsync_L2Hit_ReadsFromCsv_NeverCallsApi()
    {
        PreCreateDailyCsv(FamaFrenchDataset.ThreeFactors);
        var apiFetcher = new Mock<IFactorDataFetcher>();

        using var sut = new WriteThroughFactorDataFetcher(apiFetcher.Object, _tempDir);
        var result = await sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();

        result.Should().HaveCount(3);
        result[0].Value["Mkt-RF"].Should().Be(0.5m);
        apiFetcher.Verify(f => f.FetchDailyAsync(It.IsAny<FamaFrenchDataset>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FetchDailyAsync_L2Miss_CallsApiAndWritesCsv()
    {
        var apiFetcher = new Mock<IFactorDataFetcher>();
        apiFetcher.Setup(f => f.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateFactorData().ToAsyncEnumerable());

        using var sut = new WriteThroughFactorDataFetcher(apiFetcher.Object, _tempDir);
        var result = await sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();

        result.Should().HaveCount(3);
        File.Exists(Path.Combine(_tempDir, "ff_ThreeFactors_daily.csv")).Should().BeTrue();
    }

    [Fact]
    public async Task FetchDailyAsync_RoundTrip_SecondCallReadsCsv()
    {
        var apiFetcher = new Mock<IFactorDataFetcher>();
        apiFetcher.Setup(f => f.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateFactorData().ToAsyncEnumerable());

        using var sut = new WriteThroughFactorDataFetcher(apiFetcher.Object, _tempDir);
        await sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();
        var result2 = await sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();

        result2.Should().HaveCount(3);
        apiFetcher.Verify(f => f.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FetchMonthlyAsync_L2Miss_CallsApiAndWritesCsv()
    {
        var apiFetcher = new Mock<IFactorDataFetcher>();
        apiFetcher.Setup(f => f.FetchMonthlyAsync(FamaFrenchDataset.FiveFactors, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateFactorData().ToAsyncEnumerable());

        using var sut = new WriteThroughFactorDataFetcher(apiFetcher.Object, _tempDir);
        var result = await sut.FetchMonthlyAsync(FamaFrenchDataset.FiveFactors).ToListAsync();

        result.Should().HaveCount(3);
        File.Exists(Path.Combine(_tempDir, "ff_FiveFactors_monthly.csv")).Should().BeTrue();
    }

    [Fact]
    public void Constructor_NullInner_ThrowsArgumentNullException()
    {
        var act = () => new WriteThroughFactorDataFetcher(null!, _tempDir);
        act.Should().Throw<ArgumentNullException>();
    }
}
