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

public sealed class CsvFactorDataTests : IDisposable
{
    private readonly string _tempDir;

    public CsvFactorDataTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"factor_test_{Guid.NewGuid():N}");
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
    // CsvFactorDataStorage tests
    // ============================================================

    [Fact]
    public async Task Storage_SaveFactorsAsync_ShouldWriteCsvWithFactorHeaders()
    {
        var storage = new CsvFactorDataStorage(_tempDir);
        var data = CreateTestFactorData(
            (new DateOnly(2020, 1, 2), new Dictionary<string, decimal>
            {
                ["Mkt-RF"] = 0.0123m,
                ["SMB"] = -0.0045m,
                ["HML"] = 0.0067m,
            }),
            (new DateOnly(2020, 1, 3), new Dictionary<string, decimal>
            {
                ["Mkt-RF"] = 0.0089m,
                ["SMB"] = 0.0012m,
                ["HML"] = -0.0034m,
            }));

        await storage.SaveFactorsAsync(FamaFrenchDataset.ThreeFactors, "daily", data);

        var filePath = storage.GetCsvFileName(FamaFrenchDataset.ThreeFactors, "daily");
        File.Exists(filePath).Should().BeTrue();

        var lines = await File.ReadAllLinesAsync(filePath);
        lines.Should().HaveCount(3); // header + 2 data rows

        // Header should contain factor names (sorted alphabetically)
        lines[0].Should().Contain("Date");
        lines[0].Should().Contain("HML");
        lines[0].Should().Contain("Mkt-RF");
        lines[0].Should().Contain("SMB");
    }

    [Fact]
    public async Task Storage_SaveFactorsAsync_EmptyData_ShouldNotThrow()
    {
        var storage = new CsvFactorDataStorage(_tempDir);
        var data = CreateTestFactorData();

        // Empty data should not throw — may write nothing or header-only
        var act = async () => await storage.SaveFactorsAsync(FamaFrenchDataset.FiveFactors, "monthly", data);
        await act.Should().NotThrowAsync();
    }

    // ============================================================
    // CsvFactorDataFetcher tests
    // ============================================================

    [Fact]
    public async Task Fetcher_FetchDailyAsync_ShouldReadAllFactors()
    {
        await WriteFactorCsv(FamaFrenchDataset.ThreeFactors, "daily", [
            "Date,HML,Mkt-RF,SMB",
            "2020-01-02,0.0067,0.0123,-0.0045",
            "2020-01-03,-0.0034,0.0089,0.0012",
        ]);

        var fetcher = new CsvFactorDataFetcher(_tempDir);
        var results = new List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>();
        await foreach (var kv in fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, null, null))
        {
            results.Add(kv);
        }

        results.Should().HaveCount(2);
        results[0].Key.Should().Be(new DateOnly(2020, 1, 2));
        results[0].Value.Should().ContainKey("Mkt-RF");
        results[0].Value["Mkt-RF"].Should().Be(0.0123m);
        results[0].Value["HML"].Should().Be(0.0067m);
        results[0].Value["SMB"].Should().Be(-0.0045m);
    }

    [Fact]
    public async Task Fetcher_FetchDailyAsync_ShouldFilterByDateRange()
    {
        await WriteFactorCsv(FamaFrenchDataset.ThreeFactors, "daily", [
            "Date,Mkt-RF,SMB,HML",
            "2020-01-02,0.01,0.002,0.003",
            "2020-01-03,0.02,0.003,0.004",
            "2020-01-06,0.03,0.004,0.005",
            "2020-01-07,0.04,0.005,0.006",
        ]);

        var fetcher = new CsvFactorDataFetcher(_tempDir);
        var results = new List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>();
        await foreach (var kv in fetcher.FetchDailyAsync(
                           FamaFrenchDataset.ThreeFactors,
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
    public async Task Fetcher_FetchDailyAsync_MissingFile_ShouldThrowFileNotFound()
    {
        var fetcher = new CsvFactorDataFetcher(_tempDir);

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchDailyAsync(FamaFrenchDataset.Momentum, null, null))
            {
            }
        };

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task Fetcher_FetchMonthlyAsync_ShouldWork()
    {
        await WriteFactorCsv(FamaFrenchDataset.FiveFactors, "monthly", [
            "Date,CMA,HML,Mkt-RF,RMW,SMB",
            "2020-01-31,0.01,0.02,0.03,0.04,0.05",
        ]);

        var fetcher = new CsvFactorDataFetcher(_tempDir);
        var results = new List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>();
        await foreach (var kv in fetcher.FetchMonthlyAsync(FamaFrenchDataset.FiveFactors, null, null))
        {
            results.Add(kv);
        }

        results.Should().HaveCount(1);
        results[0].Value.Should().HaveCount(5);
    }

    // ============================================================
    // Roundtrip: Storage -> Fetcher
    // ============================================================

    [Fact]
    public async Task Roundtrip_SaveAndFetch_ShouldPreserveData()
    {
        var storage = new CsvFactorDataStorage(_tempDir);
        var expected = new[]
        {
            (new DateOnly(2020, 6, 1), new Dictionary<string, decimal>
            {
                ["Mkt-RF"] = 0.0234m,
                ["SMB"] = -0.0056m,
                ["HML"] = 0.0078m,
            }),
            (new DateOnly(2020, 6, 2), new Dictionary<string, decimal>
            {
                ["Mkt-RF"] = -0.0123m,
                ["SMB"] = 0.0034m,
                ["HML"] = 0.0012m,
            }),
        };

        await storage.SaveFactorsAsync(
            FamaFrenchDataset.ThreeFactors,
            "daily",
            CreateTestFactorData(expected.Select(e => (e.Item1, (Dictionary<string, decimal>)e.Item2)).ToArray()));

        var fetcher = new CsvFactorDataFetcher(_tempDir);
        var results = new List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>();
        await foreach (var kv in fetcher.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, null, null))
        {
            results.Add(kv);
        }

        results.Should().HaveCount(2);
        for (var i = 0; i < expected.Length; i++)
        {
            results[i].Key.Should().Be(expected[i].Item1);
            results[i].Value["Mkt-RF"].Should().Be(expected[i].Item2["Mkt-RF"]);
            results[i].Value["SMB"].Should().Be(expected[i].Item2["SMB"]);
            results[i].Value["HML"].Should().Be(expected[i].Item2["HML"]);
        }
    }

    // ============================================================
    // Helpers
    // ============================================================

    private async Task WriteFactorCsv(FamaFrenchDataset dataset, string frequency, string[] lines)
    {
        var path = Path.Combine(_tempDir, $"ff_{dataset}_{frequency}.csv");
        await File.WriteAllLinesAsync(path, lines);
    }

    private static async IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> CreateTestFactorData(
        params (DateOnly Date, Dictionary<string, decimal> Factors)[] items)
    {
        foreach (var (date, factors) in items)
        {
            yield return new KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>(
                date, new ReadOnlyDictionary<string, decimal>(factors));
        }

        await Task.CompletedTask;
    }
}
