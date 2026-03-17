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
/// Tests for <see cref="CsvSymbolReader"/> (R2I-11).
/// </summary>
public sealed class CsvSymbolReaderTests : IDisposable
{
    private readonly string _testDir;

    public CsvSymbolReaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"csv_symbol_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    private string WriteSymbolFile(string fileName, string content)
    {
        var filePath = Path.Combine(_testDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    [Fact]
    public async Task ReadSymbolsAsync_ValidFile_ReturnsSymbols()
    {
        var path = WriteSymbolFile("symbols.csv", "AAPL\nMSFT\nGOOG");
        var reader = new CsvSymbolReader(path);

        var symbols = await reader.ReadSymbolsAsync(CancellationToken.None);

        symbols.Should().HaveCount(3);
        symbols.Select(s => s.Ticker).Should().BeEquivalentTo(["AAPL", "MSFT", "GOOG"]);
    }

    [Fact]
    public async Task ReadSymbolsAsync_EmptyFile_ReturnsEmpty()
    {
        var path = WriteSymbolFile("empty.csv", "");
        var reader = new CsvSymbolReader(path);

        var symbols = await reader.ReadSymbolsAsync(CancellationToken.None);

        symbols.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadSymbolsAsync_CancellationToken_ThrowsOperationCanceled()
    {
        var path = WriteSymbolFile("symbols.csv", "AAPL\nMSFT");
        var reader = new CsvSymbolReader(path);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => reader.ReadSymbolsAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_NullFilePath_ThrowsArgumentNullException()
    {
        var act = () => new CsvSymbolReader(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_MissingFile_ThrowsFileNotFoundException()
    {
        var act = () => new CsvSymbolReader(Path.Combine(_testDir, "nonexistent.csv"));
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public async Task ReadSymbolsAsync_SingleLine_ReturnsSingleSymbol()
    {
        var path = WriteSymbolFile("single.csv", "VTI");
        var reader = new CsvSymbolReader(path);

        var symbols = await reader.ReadSymbolsAsync(CancellationToken.None);

        symbols.Should().HaveCount(1);
        symbols.First().Ticker.Should().Be("VTI");
    }
}
