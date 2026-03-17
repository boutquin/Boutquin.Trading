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

using Boutquin.Trading.Domain.Helpers;

namespace Boutquin.Trading.Tests.UnitTests.Domain;

/// <summary>
/// Tests for <see cref="CsvMarketDataStorage"/> (R2I-09).
/// </summary>
public sealed class CsvMarketDataStorageTests : IDisposable
{
    private readonly string _testDir;

    public CsvMarketDataStorageTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"csv_storage_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    private static KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>> CreateDataPoint(
        string ticker, DateOnly date, decimal close = 100.5m)
    {
        var md = new MarketData(date, 100m, 105m, 95m, close, close, 1000000L, 0.5m, 1.0m);
        return new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(
            date, new SortedDictionary<Asset, MarketData> { [new Asset(ticker)] = md });
    }

    [Fact]
    public async Task SaveMarketDataAsync_SinglePoint_WritesHeaderAndData()
    {
        var storage = new CsvMarketDataStorage(_testDir);
        var dataPoint = CreateDataPoint("AAPL", new DateOnly(2024, 1, 15));

        await storage.SaveMarketDataAsync(dataPoint, CancellationToken.None);

        var filePath = MarketDataFileNameHelper.GetCsvFileNameForMarketData(_testDir, "AAPL");
        File.Exists(filePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("Timestamp,Open,High,Low,Close");
        content.Should().Contain("01/15/2024"); // DateOnly default format or InvariantCulture
    }

    [Fact]
    public async Task SaveMarketDataAsync_InvariantCulture_UsesDecimalPoint()
    {
        // R2I-07: Verify InvariantCulture formatting — decimals must use "." not ","
        var storage = new CsvMarketDataStorage(_testDir);
        var md = new MarketData(new DateOnly(2024, 1, 15), 123.45m, 125m, 120m, 124.56m, 124.56m, 1000, 0.75m, 1.0m);
        var dataPoint = new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(
            new DateOnly(2024, 1, 15),
            new SortedDictionary<Asset, MarketData> { [new Asset("TEST")] = md });

        await storage.SaveMarketDataAsync(dataPoint, CancellationToken.None);

        var filePath = MarketDataFileNameHelper.GetCsvFileNameForMarketData(_testDir, "TEST");
        var content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("123.45");
        content.Should().Contain("124.56");
        content.Should().Contain("0.75");
    }

    [Fact]
    public async Task SaveMarketDataAsync_BatchMode_WritesMultipleRows()
    {
        var storage = new CsvMarketDataStorage(_testDir);
        var dataPoints = new[]
        {
            CreateDataPoint("AAPL", new DateOnly(2024, 1, 15), 185m),
            CreateDataPoint("AAPL", new DateOnly(2024, 1, 16), 186m),
        };

        await storage.SaveMarketDataAsync(dataPoints, CancellationToken.None);

        var filePath = MarketDataFileNameHelper.GetCsvFileNameForMarketData(_testDir, "AAPL");
        var lines = await File.ReadAllLinesAsync(filePath);
        // Header + 2 data rows
        lines.Should().HaveCount(3);
    }

    [Fact]
    public async Task SaveMarketDataAsync_EmptyDataPoint_ThrowsArgumentException()
    {
        var storage = new CsvMarketDataStorage(_testDir);
        var empty = new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(
            new DateOnly(2024, 1, 15), new SortedDictionary<Asset, MarketData>());

        var act = () => storage.SaveMarketDataAsync(empty, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveMarketDataAsync_NullBatchInput_ThrowsArgumentNullException()
    {
        var storage = new CsvMarketDataStorage(_testDir);

        var act = () => storage.SaveMarketDataAsync(
            (IEnumerable<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>)null!,
            CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveMarketDataAsync_CancellationToken_ThrowsOperationCanceled()
    {
        var storage = new CsvMarketDataStorage(_testDir);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var dataPoint = CreateDataPoint("AAPL", new DateOnly(2024, 1, 15));

        var act = () => storage.SaveMarketDataAsync(dataPoint, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_NullDirectory_ThrowsArgumentNullException()
    {
        var act = () => new CsvMarketDataStorage(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_CreatesDirectoryIfNotExists()
    {
        var newDir = Path.Combine(_testDir, "subdir");
        _ = new CsvMarketDataStorage(newDir);
        Directory.Exists(newDir).Should().BeTrue();
    }
}
