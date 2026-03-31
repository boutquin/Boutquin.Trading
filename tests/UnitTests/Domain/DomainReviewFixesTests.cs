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

using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Helpers;

/// <summary>
/// TDD tests for domain review fixes (spec: review-fixes-domain).
/// Each nested class targets one finding from the deep code review.
/// </summary>
public sealed class DomainReviewFixesTests
{

    // ══════════════════════════════════════════════════════════════════
    // M8 — AnnualizedReturn wipeout guard
    // ══════════════════════════════════════════════════════════════════

    public sealed class M8Tests
    {
        [Fact]
        public void AnnualizedReturn_WithTotalWipeout_ThrowsCalculationException()
        {
            // Arrange — a single return of -100% produces cumulative return of -1.0
            var dailyReturns = new[] { -1.0m };

            // Act
            var act = () => dailyReturns.AnnualizedReturn();

            // Assert
            act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
        }

        [Fact]
        public void AnnualizedReturn_WithWorseThanWipeout_ThrowsCalculationException()
        {
            // Arrange — a return of -1.5 produces cumulative return < -1
            var dailyReturns = new[] { -1.5m };

            // Act
            var act = () => dailyReturns.AnnualizedReturn();

            // Assert
            act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // M12 — VaR confidenceLevel bounds
    // ══════════════════════════════════════════════════════════════════

    public sealed class M12Tests
    {
        private static readonly decimal[] s_sampleReturns = [0.01m, -0.02m, 0.005m, -0.01m, 0.015m];

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(-0.5)]
        [InlineData(1.5)]
        public void HistoricalVaR_InvalidConfidenceLevel_ThrowsArgumentOutOfRangeException(double cl)
        {
            var act = () => s_sampleReturns.HistoricalVaR((decimal)cl);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(-0.5)]
        [InlineData(1.5)]
        public void ParametricVaR_InvalidConfidenceLevel_ThrowsArgumentOutOfRangeException(double cl)
        {
            var act = () => s_sampleReturns.ParametricVaR((decimal)cl);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // M13 — CsvMarketDataStorage constructor try-catch
    // ══════════════════════════════════════════════════════════════════

    public sealed class M13Tests
    {
        [Fact]
        public void CsvMarketDataStorage_Constructor_InvalidDirectory_ThrowsMarketDataStorageException()
        {
            // Arrange — embedded null byte fails on all platforms
            var invalidPath = "path\0invalid";

            // Act
            var act = () => new CsvMarketDataStorage(invalidPath);

            // Assert
            act.Should().Throw<MarketDataStorageException>();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // M23 — RiskEvaluation.Rejected reason validation
    // ══════════════════════════════════════════════════════════════════

    public sealed class M23Tests
    {
        [Fact]
        public void RiskEvaluation_Rejected_NullReason_ThrowsArgumentException()
        {
            var act = () => RiskEvaluation.Rejected(null!);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void RiskEvaluation_Rejected_EmptyReason_ThrowsArgumentException()
        {
            var act = () => RiskEvaluation.Rejected("");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void RiskEvaluation_Rejected_WhitespaceReason_ThrowsArgumentException()
        {
            var act = () => RiskEvaluation.Rejected("   ");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void RiskEvaluation_Rejected_ValidReason_Succeeds()
        {
            var result = RiskEvaluation.Rejected("drawdown exceeded");
            result.IsAllowed.Should().BeFalse();
            result.RejectionReason.Should().Be("drawdown exceeded");
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // M25 — MarketDataFileNameHelper null validation
    // ══════════════════════════════════════════════════════════════════

    public sealed class M25Tests
    {
        [Fact]
        public void GetCsvFileNameForMarketData_NullDirectory_ThrowsArgumentNullException()
        {
            var act = () => MarketDataFileNameHelper.GetCsvFileNameForMarketData(null!, "AAPL");
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetCsvFileNameForMarketData_NullTicker_ThrowsArgumentNullException()
        {
            var act = () => MarketDataFileNameHelper.GetCsvFileNameForMarketData("/data", null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetCsvFileNameForFxRateData_NullDirectory_ThrowsArgumentNullException()
        {
            var act = () => MarketDataFileNameHelper.GetCsvFileNameForFxRateData(null!, "USD/EUR");
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetCsvFileNameForFxRateData_NullCurrencyPair_ThrowsArgumentNullException()
        {
            var act = () => MarketDataFileNameHelper.GetCsvFileNameForFxRateData("/data", null!);
            act.Should().Throw<ArgumentNullException>();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // M26 — MarketDataFileNameHelper empty after sanitize
    // ══════════════════════════════════════════════════════════════════

    public sealed class M26Tests
    {
        [Fact]
        public void GetCsvFileNameForMarketData_TickerAllInvalidChars_ThrowsArgumentException()
        {
            var act = () => MarketDataFileNameHelper.GetCsvFileNameForMarketData("/data", "<>:*?");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void GetCsvFileNameForMarketData_TickerDotsOnly_ThrowsArgumentException()
        {
            var act = () => MarketDataFileNameHelper.GetCsvFileNameForMarketData("/data", "..");
            act.Should().Throw<ArgumentException>();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // M27 — Currency pair incomplete sanitization
    // ══════════════════════════════════════════════════════════════════

    public sealed class M27Tests
    {
        [Fact]
        public void GetCsvFileNameForFxRateData_CurrencyPairWithInvalidChars_StripsInvalidChars()
        {
            var result = MarketDataFileNameHelper.GetCsvFileNameForFxRateData("/data", "USD/<EUR>");
            // Should not contain < or >
            result.Should().NotContain("<");
            result.Should().NotContain(">");
            // Slash replaced with underscore, invalid chars stripped
            Path.GetFileName(result).Should().StartWith("daily_fx_");
        }

        [Fact]
        public void GetCsvFileNameForFxRateData_CurrencyPairAllInvalidChars_ThrowsArgumentException()
        {
            var act = () => MarketDataFileNameHelper.GetCsvFileNameForFxRateData("/data", "<>:*?");
            act.Should().Throw<ArgumentException>();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // L2 — MarketData.AdjustForSplit unchecked long cast
    // ══════════════════════════════════════════════════════════════════

    public sealed class L2Tests
    {
        [Fact]
        public void AdjustForSplit_LargeVolume_OverflowThrowsOverflowException()
        {
            // Arrange — Volume at long.MaxValue, split ratio of 2
            var md = new MarketData(
                Timestamp: new DateOnly(2024, 1, 15),
                Open: 100m, High: 110m, Low: 90m, Close: 105m,
                AdjustedClose: 105m, Volume: long.MaxValue,
                DividendPerShare: 0m, SplitCoefficient: 1m);

            // Act
            var act = () => md.AdjustForSplit(2m);

            // Assert
            act.Should().Throw<OverflowException>();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // L8 — MarketDataProcessor double-enumeration risk
    // ══════════════════════════════════════════════════════════════════

    public sealed class L8Tests
    {
        [Fact]
        public async Task MarketDataProcessor_ProcessAndStore_MaterializesSymbolsOnce()
        {
            // Arrange — a counting enumerable that tracks how many times it's enumerated
            var enumerationCount = 0;
            var assets = new[] { new Asset("AAPL") };

            IEnumerable<Asset> CountingEnumerable()
            {
                Interlocked.Increment(ref enumerationCount);
                foreach (var a in assets)
                {
                    yield return a;
                }
            }

            var mockFetcher = new Mock<IMarketDataFetcher>();
            mockFetcher.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
                .Returns(Enumerable.Empty<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>().ToAsyncEnumerable());

            var mockStorage = new Mock<IMarketDataStorage>();

            var processor = new MarketDataProcessor(mockFetcher.Object, mockStorage.Object);

            // Act
            await processor.ProcessAndStoreMarketDataAsync(CountingEnumerable(), CancellationToken.None);

            // Assert — should only enumerate once (materialized to list)
            enumerationCount.Should().Be(1);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // H1 — Double Path.Combine in CsvMarketDataStorage
    // ══════════════════════════════════════════════════════════════════

    public sealed class H1Tests
    {
        [Fact]
        public async Task CsvMarketDataStorage_SaveSingleDataPoint_UsesCorrectFilePath()
        {
            // Arrange — use a relative-style directory name to catch double-combine
            var tempDir = Path.Combine(Path.GetTempPath(), "h1_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var storage = new CsvMarketDataStorage(tempDir);
                var asset = new Asset("AAPL");
                var data = new MarketData(
                    new DateOnly(2024, 1, 15), 150m, 155m, 149m, 152m, 152m, 1000000, 0m, 1m);
                var dataPoint = new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(
                    new DateOnly(2024, 1, 15),
                    new SortedDictionary<Asset, MarketData> { { asset, data } });

                // Act
                await storage.SaveMarketDataAsync(dataPoint, CancellationToken.None);

                // Assert — file should be at tempDir/daily_adjusted_AAPL.csv
                var expectedPath = Path.Combine(tempDir, "daily_adjusted_AAPL.csv");
                File.Exists(expectedPath).Should().BeTrue($"File should exist at {expectedPath}");

                // Verify the file contains data (header + 1 row)
                var lines = await File.ReadAllLinesAsync(expectedPath);
                lines.Length.Should().Be(2, "should have header + 1 data line");
                lines[0].Should().StartWith("Timestamp,");

                // Verify only expected files exist in tempDir (no nested dirs from doubled path)
                var allFiles = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
                allFiles.Length.Should().Be(1, "only one file should be created");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task CsvMarketDataStorage_SaveBatchDataPoints_UsesCorrectFilePath()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "h1_batch_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var storage = new CsvMarketDataStorage(tempDir);
                var asset = new Asset("MSFT");
                var data = new MarketData(
                    new DateOnly(2024, 1, 15), 300m, 310m, 295m, 305m, 305m, 2000000, 0m, 1m);
                var dataPoints = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>
                {
                    new(new DateOnly(2024, 1, 15),
                        new SortedDictionary<Asset, MarketData> { { asset, data } }),
                };

                // Act
                await storage.SaveMarketDataAsync(dataPoints, CancellationToken.None);

                // Assert
                var expectedPath = Path.Combine(tempDir, "daily_adjusted_MSFT.csv");
                File.Exists(expectedPath).Should().BeTrue($"File should exist at {expectedPath}");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // M21 — BrinsonFachlerResult uses Asset keys
    // ══════════════════════════════════════════════════════════════════

    public sealed class M21Tests
    {
        [Fact]
        public void BrinsonFachlerResult_UsesAssetKeys()
        {
            var asset = new Asset("Equity");
            var effects = new Dictionary<Asset, decimal> { { asset, 0.01m } };

            var result = new Boutquin.Trading.Domain.Analytics.BrinsonFachlerResult(
                0.01m, 0.02m, 0.003m, 0.033m,
                effects, effects, effects);

            result.AssetAllocationEffects.Should().ContainKey(asset);
            result.AssetAllocationEffects[asset].Should().Be(0.01m);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // L3 — CorrelationAnalysisResult uses Asset for AssetNames
    // ══════════════════════════════════════════════════════════════════

    public sealed class L3Tests
    {
        [Fact]
        public void CorrelationAnalysisResult_UsesAssetForAssetNames()
        {
            var assets = new List<Asset> { new("A"), new("B") };
            var matrix = new decimal[2, 2] { { 1m, 0.5m }, { 0.5m, 1m } };

            var result = new Boutquin.Trading.Domain.Analytics.CorrelationAnalysisResult(
                matrix, assets, 1.2m);

            result.AssetNames.Should().HaveCount(2);
            result.AssetNames[0].Should().Be(new Asset("A"));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // M19 — Nullable inner market data removed
    // ══════════════════════════════════════════════════════════════════

    public sealed class M19Tests
    {
        [Fact]
        public void IPortfolio_HistoricalMarketData_IsNonNullable()
        {
            // Verify via reflection that the generic argument is non-nullable
            var prop = typeof(IPortfolio).GetProperty("HistoricalMarketData");
            prop.Should().NotBeNull();

            // The property type should be SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
            var propType = prop!.PropertyType;
            var innerType = propType.GetGenericArguments()[1];
            innerType.Should().Be(typeof(SortedDictionary<Asset, MarketData>));
        }

        [Fact]
        public void MarketEvent_Data_IsNonNullable()
        {
            // MarketEvent.HistoricalMarketData should be non-nullable
            var prop = typeof(MarketEvent).GetProperty("HistoricalMarketData");
            prop.Should().NotBeNull();

            var propType = prop!.PropertyType;
            propType.Should().Be(typeof(SortedDictionary<Asset, MarketData>));
        }
    }
}
