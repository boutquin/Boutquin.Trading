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

using Boutquin.Trading.Domain.Helpers;

/// <summary>
/// Tests for C2: Path traversal protection in MarketDataFileNameHelper.
/// </summary>
public sealed class MarketDataFileNameHelperTests
{
    /// <summary>
    /// C2: Path traversal attack via ticker should produce a safe filename.
    /// </summary>
    [Fact]
    public void SanitizeTickerForFileName_PathTraversal_Sanitized()
    {
        // Arrange — a malicious ticker with path traversal
        var directory = "/data";
        var ticker = "../../../etc/passwd";

        // Act
        var result = MarketDataFileNameHelper.GetCsvFileNameForMarketData(directory, ticker);

        // Assert — result should NOT contain ".." path traversal
        result.Should().NotContain("..");
        // The result should be within the data directory
        Path.GetDirectoryName(result).Should().Be(directory);
    }

    /// <summary>
    /// C2: Tickers with invalid filename chars should produce valid filename.
    /// </summary>
    [Theory]
    [InlineData("AAPL<>", "AAPL")]
    [InlineData("MSFT:Corp", "MSFTCorp")]
    [InlineData("TEST|PIPE", "TESTPIPE")]
    [InlineData("TICK?*ER", "TICKER")]
    public void SanitizeTickerForFileName_InvalidChars_Stripped(string ticker, string expectedSanitized)
    {
        // Arrange
        var directory = "/data";

        // Act
        var result = MarketDataFileNameHelper.GetCsvFileNameForMarketData(directory, ticker);

        // Assert — the filename should use the sanitized ticker
        var expectedFileName = Path.Combine(directory, $"daily_adjusted_{expectedSanitized}.csv");
        result.Should().Be(expectedFileName);
    }
}
