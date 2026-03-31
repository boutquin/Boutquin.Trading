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

namespace Boutquin.Trading.Tests.UnitTests.Domain;

public sealed class CsvEconomicDataTests : IDisposable
{
    private readonly string _tempDir;

    public CsvEconomicDataTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"econ_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // ============================================================
    // CsvEconomicDataStorage tests
    // ============================================================

    [Fact]
    public async Task Storage_SaveSeriesAsync_ShouldWriteCsvWithHeaderAndData()
    {
        var storage = new CsvEconomicDataStorage(_tempDir);
        var data = CreateTestEconomicData(
            (new DateOnly(2020, 1, 2), 1.55m),
            (new DateOnly(2020, 1, 3), 1.57m),
            (new DateOnly(2020, 1, 6), 1.59m));

        await storage.SaveSeriesAsync("DGS10", data);

        var filePath = storage.GetCsvFileName("DGS10");
        File.Exists(filePath).Should().BeTrue();

        var lines = await File.ReadAllLinesAsync(filePath);
        lines[0].Should().Be("Date,Value");
        lines.Should().HaveCount(4); // header + 3 data rows

        // Verify ISO date format and invariant culture decimal separator
        lines[1].Should().Be("2020-01-02,1.55");
    }

    [Fact]
    public async Task Storage_SaveSeriesAsync_ShouldOverwriteExistingFile()
    {
        var storage = new CsvEconomicDataStorage(_tempDir);

        var data1 = CreateTestEconomicData((new DateOnly(2020, 1, 2), 1.0m));
        await storage.SaveSeriesAsync("DGS10", data1);

        var data2 = CreateTestEconomicData(
            (new DateOnly(2021, 6, 1), 2.0m),
            (new DateOnly(2021, 6, 2), 2.1m));
        await storage.SaveSeriesAsync("DGS10", data2);

        var lines = await File.ReadAllLinesAsync(storage.GetCsvFileName("DGS10"));
        lines.Should().HaveCount(3); // header + 2 new rows
    }

    [Fact]
    public async Task Storage_SaveSeriesAsync_EmptyData_ShouldWriteHeaderOnly()
    {
        var storage = new CsvEconomicDataStorage(_tempDir);
        await storage.SaveSeriesAsync("EMPTY", CreateTestEconomicData());

        var lines = await File.ReadAllLinesAsync(storage.GetCsvFileName("EMPTY"));
        lines.Should().HaveCount(1);
        lines[0].Should().Be("Date,Value");
    }

    // ============================================================
    // CsvEconomicDataFetcher tests
    // ============================================================

    [Fact]
    public async Task Fetcher_FetchSeriesAsync_ShouldReadAllRows()
    {
        await WriteEconomicCsv("DGS10", [
            "Date,Value",
            "2020-01-02,1.55",
            "2020-01-03,1.57",
            "2020-01-06,1.59",
        ]);

        var fetcher = new CsvEconomicDataFetcher(_tempDir);
        var results = new List<KeyValuePair<DateOnly, decimal>>();
        await foreach (var kv in fetcher.FetchSeriesAsync("DGS10", null, null))
        {
            results.Add(kv);
        }

        results.Should().HaveCount(3);
        results[0].Key.Should().Be(new DateOnly(2020, 1, 2));
        results[0].Value.Should().Be(1.55m);
    }

    [Fact]
    public async Task Fetcher_FetchSeriesAsync_ShouldFilterByDateRange()
    {
        await WriteEconomicCsv("DGS10", [
            "Date,Value",
            "2020-01-02,1.55",
            "2020-01-03,1.57",
            "2020-01-06,1.59",
            "2020-01-07,1.61",
        ]);

        var fetcher = new CsvEconomicDataFetcher(_tempDir);
        var results = new List<KeyValuePair<DateOnly, decimal>>();
        await foreach (var kv in fetcher.FetchSeriesAsync(
                           "DGS10",
                           new DateOnly(2020, 1, 3),
                           new DateOnly(2020, 1, 6)))
        {
            results.Add(kv);
        }

        results.Should().HaveCount(2);
        results[0].Key.Should().Be(new DateOnly(2020, 1, 3));
        results[1].Key.Should().Be(new DateOnly(2020, 1, 6));
    }

    [Fact]
    public async Task Fetcher_FetchSeriesAsync_MissingFile_ShouldThrowFileNotFound()
    {
        var fetcher = new CsvEconomicDataFetcher(_tempDir);

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchSeriesAsync("NONEXISTENT", null, null))
            {
            }
        };

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task Fetcher_FetchSeriesAsync_MalformedRow_ShouldSkipGracefully()
    {
        await WriteEconomicCsv("BAD", [
            "Date,Value",
            "2020-01-02,1.55",
            "2020-01-03,NOT_A_NUMBER",
            "2020-01-06,1.59",
        ]);

        var fetcher = new CsvEconomicDataFetcher(_tempDir);
        var results = new List<KeyValuePair<DateOnly, decimal>>();
        await foreach (var kv in fetcher.FetchSeriesAsync("BAD", null, null))
        {
            results.Add(kv);
        }

        // Should skip the malformed row and return 2 valid rows
        results.Should().HaveCount(2);
    }

    // ============================================================
    // Roundtrip: Storage -> Fetcher
    // ============================================================

    [Fact]
    public async Task Roundtrip_SaveAndFetch_ShouldPreserveData()
    {
        var storage = new CsvEconomicDataStorage(_tempDir);
        var expected = new[]
        {
            (new DateOnly(2020, 3, 1), 0.65m),
            (new DateOnly(2020, 3, 2), 0.70m),
            (new DateOnly(2020, 3, 3), 0.72m),
        };

        await storage.SaveSeriesAsync("DGS2", CreateTestEconomicData(expected));

        var fetcher = new CsvEconomicDataFetcher(_tempDir);
        var results = new List<KeyValuePair<DateOnly, decimal>>();
        await foreach (var kv in fetcher.FetchSeriesAsync("DGS2", null, null))
        {
            results.Add(kv);
        }

        results.Should().HaveCount(3);
        for (var i = 0; i < expected.Length; i++)
        {
            results[i].Key.Should().Be(expected[i].Item1);
            results[i].Value.Should().Be(expected[i].Item2);
        }
    }

    // ============================================================
    // Helpers
    // ============================================================

    private async Task WriteEconomicCsv(string seriesId, string[] lines)
    {
        var path = Path.Combine(_tempDir, $"fred_{seriesId}.csv");
        await File.WriteAllLinesAsync(path, lines);
    }

    private static async IAsyncEnumerable<KeyValuePair<DateOnly, decimal>> CreateTestEconomicData(
        params (DateOnly Date, decimal Value)[] items)
    {
        foreach (var (date, value) in items)
        {
            yield return new KeyValuePair<DateOnly, decimal>(date, value);
        }

        await Task.CompletedTask;
    }
}
