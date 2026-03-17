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

using Microsoft.Extensions.Logging;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Tests for D9: Backtest.AnalyzePerformanceMetrics with empty equity curve.
/// </summary>
public sealed class BacktestTests
{
    /// <summary>
    /// D9: Calling AnalyzePerformanceMetrics before RunAsync (empty equity curve)
    /// should throw InvalidOperationException, not crash with divide-by-zero.
    /// </summary>
    [Fact]
    public void Backtest_AnalyzePerformanceMetrics_EmptyCurve_Throws()
    {
        // Arrange — create backtest with mocked dependencies (equity curve will be empty)
        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());
        portfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy>());

        var benchmarkPortfolio = new Mock<IPortfolio>();
        benchmarkPortfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());
        benchmarkPortfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy>());

        var fetcher = new Mock<IMarketDataFetcher>();

        var backtest = new BackTest(portfolio.Object, benchmarkPortfolio.Object, fetcher.Object, CurrencyCode.USD);

        // Act & Assert — should throw, not divide by zero
        var act = backtest.AnalyzePerformanceMetrics;
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// H3: Verifies that RunAsync fetches FX rates for benchmark portfolio asset currencies too.
    /// </summary>
    [Fact]
    public async Task RunAsync_ShouldFetchFxRatesForBenchmarkAssets()
    {
        // Arrange
        var portfolioStrategy = new Mock<IStrategy>();
        portfolioStrategy.Setup(s => s.Assets).Returns(new Dictionary<Asset, CurrencyCode>
        {
            [new Asset("AAPL")] = CurrencyCode.USD
        });

        var benchmarkStrategy = new Mock<IStrategy>();
        benchmarkStrategy.Setup(s => s.Assets).Returns(new Dictionary<Asset, CurrencyCode>
        {
            [new Asset("EWG")] = CurrencyCode.EUR  // Benchmark has EUR-denominated asset
        });

        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy> { ["Main"] = portfolioStrategy.Object });
        portfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());

        var benchmarkPortfolio = new Mock<IPortfolio>();
        benchmarkPortfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy> { ["BM"] = benchmarkStrategy.Object });
        benchmarkPortfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());

        IEnumerable<string>? capturedPairs = null;
        var fetcher = new Mock<IMarketDataFetcher>();
        fetcher.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>());
        fetcher.Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, CancellationToken>((pairs, _) => capturedPairs = pairs.ToList())
            .Returns(AsyncEnumerable.Empty<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>());

        var backtest = new BackTest(portfolio.Object, benchmarkPortfolio.Object, fetcher.Object, CurrencyCode.USD);

        // Act
        try { await backtest.RunAsync(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)).ConfigureAwait(false); }
        catch (InvalidOperationException) { /* Expected — empty equity curve */ }

        // Assert — currency pairs should include EUR from benchmark
        capturedPairs.Should().NotBeNull();
        capturedPairs.Should().Contain("USD_EUR");
    }

    /// <summary>
    /// L7: Verifies that RunAsync throws ArgumentException with nameof(startDate) when startDate >= endDate.
    /// </summary>
    [Fact]
    public async Task RunAsync_StartDateAfterEndDate_ShouldThrowWithParamName()
    {
        // Arrange
        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy>());
        portfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());

        var benchmarkPortfolio = new Mock<IPortfolio>();
        benchmarkPortfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy>());
        benchmarkPortfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());

        var fetcher = new Mock<IMarketDataFetcher>();
        var backtest = new BackTest(portfolio.Object, benchmarkPortfolio.Object, fetcher.Object, CurrencyCode.USD);

        // Act
        var act = () => backtest.RunAsync(new DateOnly(2025, 1, 1), new DateOnly(2024, 1, 1));

        // Assert — ParamName should be "startDate"
        await act.Should().ThrowAsync<ArgumentException>()
            .Where(e => e.ParamName == "startDate").ConfigureAwait(false);
    }

    /// <summary>
    /// M32: Verifies that RunAsync logs a warning when no FX conversion rates are loaded.
    /// </summary>
    [Fact]
    public async Task RunAsync_NoFxRates_ShouldLogWarning()
    {
        // Arrange
        var portfolioStrategy = new Mock<IStrategy>();
        portfolioStrategy.Setup(s => s.Assets).Returns(new Dictionary<Asset, CurrencyCode>
        {
            [new Asset("AAPL")] = CurrencyCode.USD
        });

        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy> { ["Main"] = portfolioStrategy.Object });
        portfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());

        var benchmarkPortfolio = new Mock<IPortfolio>();
        benchmarkPortfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy>());
        benchmarkPortfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());

        var fetcher = new Mock<IMarketDataFetcher>();
        fetcher.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>());
        // Return empty FX rates
        fetcher.Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>());

        var loggerMock = new Mock<ILogger<BackTest>>();
        var backtest = new BackTest(portfolio.Object, benchmarkPortfolio.Object, fetcher.Object, CurrencyCode.USD, loggerMock.Object);

        // Act
        try { await backtest.RunAsync(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)).ConfigureAwait(false); }
        catch (InvalidOperationException) { /* Expected — empty equity curve */ }

        // Assert — logger should have been called with Warning level
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No FX conversion rates loaded")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
