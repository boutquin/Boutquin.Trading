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

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Tests for the burn-in period feature in BackTest.RunAsync.
/// QSTrader-inspired: allows indicators to warm up before official performance tracking begins.
/// </summary>
public sealed class BurnInPeriodTests
{
    private static readonly Asset s_spy = new("SPY");

    // 10 trading days: 2024-01-02 through 2024-01-15 (skipping weekends)
    private static readonly DateOnly[] s_tradingDays =
    [
        new(2024, 1, 2), new(2024, 1, 3), new(2024, 1, 4), new(2024, 1, 5),
        new(2024, 1, 8), new(2024, 1, 9), new(2024, 1, 10), new(2024, 1, 11),
        new(2024, 1, 12), new(2024, 1, 15)
    ];

    private static readonly DateOnly s_startDate = new(2024, 1, 2);
    private static readonly DateOnly s_endDate = new(2024, 1, 15);

    /// <summary>
    /// No burnInEndDate. Verify UpdateEquityCurve called for all 10 days.
    /// </summary>
    [Fact]
    public async Task RunAsync_WithNoBurnIn_RecordsAllEquityCurvePoints()
    {
        // Arrange
        var (portfolioMock, benchmarkMock, fetcherMock) = BuildMocks();

        var backtest = new BackTest(
            portfolioMock.Object,
            benchmarkMock.Object,
            fetcherMock.Object,
            CurrencyCode.USD);

        // Act
        await backtest.RunAsync(s_startDate, s_endDate);

        // Assert — UpdateEquityCurve called once per trading day for the portfolio
        portfolioMock.Verify(
            p => p.UpdateEquityCurve(It.IsAny<DateOnly>()),
            Times.Exactly(s_tradingDays.Length));
    }

    /// <summary>
    /// Set burnInEndDate = 2024-01-08 (skip first 5 days).
    /// Verify UpdateEquityCurve called only for the last 5 days (2024-01-09 through 2024-01-15).
    /// </summary>
    [Fact]
    public async Task RunAsync_WithBurnIn_SkipsEquityCurveDuringBurnIn()
    {
        // Arrange
        var burnInEndDate = new DateOnly(2024, 1, 8);
        var (portfolioMock, benchmarkMock, fetcherMock) = BuildMocks();

        var backtest = new BackTest(
            portfolioMock.Object,
            benchmarkMock.Object,
            fetcherMock.Object,
            CurrencyCode.USD);

        // Act
        await backtest.RunAsync(s_startDate, s_endDate, burnInEndDate: burnInEndDate);

        // Assert — only days after 2024-01-08: 2024-01-09, 10, 11, 12, 15 = 5 days
        var postBurnInDays = s_tradingDays.Where(d => d > burnInEndDate).ToArray();
        postBurnInDays.Should().HaveCount(5);

        portfolioMock.Verify(
            p => p.UpdateEquityCurve(It.IsAny<DateOnly>()),
            Times.Exactly(postBurnInDays.Length));

        // Verify each post-burn-in day was called
        foreach (var day in postBurnInDays)
        {
            portfolioMock.Verify(p => p.UpdateEquityCurve(day), Times.Once);
        }

        // Verify burn-in days were NOT called
        var burnInDays = s_tradingDays.Where(d => d <= burnInEndDate).ToArray();
        foreach (var day in burnInDays)
        {
            portfolioMock.Verify(p => p.UpdateEquityCurve(day), Times.Never);
        }
    }

    /// <summary>
    /// Verify that HandleEventAsync and ProcessPendingOrdersAsync are called for ALL days
    /// including burn-in period (orders must still execute for positions to build up).
    /// </summary>
    [Fact]
    public async Task RunAsync_WithBurnIn_StillProcessesOrdersDuringBurnIn()
    {
        // Arrange
        var burnInEndDate = new DateOnly(2024, 1, 8);
        var (portfolioMock, benchmarkMock, fetcherMock) = BuildMocks();

        var backtest = new BackTest(
            portfolioMock.Object,
            benchmarkMock.Object,
            fetcherMock.Object,
            CurrencyCode.USD);

        // Act
        await backtest.RunAsync(s_startDate, s_endDate, burnInEndDate: burnInEndDate);

        // Assert — HandleEventAsync and ProcessPendingOrdersAsync called for ALL 10 days
        portfolioMock.Verify(
            p => p.HandleEventAsync(It.IsAny<IFinancialEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(s_tradingDays.Length));

        portfolioMock.Verify(
            p => p.ProcessPendingOrdersAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<SortedDictionary<Asset, MarketData>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(s_tradingDays.Length));
    }

    /// <summary>
    /// burnInEndDate less than startDate → throws ArgumentException.
    /// </summary>
    [Fact]
    public async Task RunAsync_BurnInEndDateBeforeStartDate_ThrowsArgumentException()
    {
        // Arrange
        var (portfolioMock, benchmarkMock, fetcherMock) = BuildMocks();
        var backtest = new BackTest(
            portfolioMock.Object,
            benchmarkMock.Object,
            fetcherMock.Object,
            CurrencyCode.USD);

        var burnInEndDate = new DateOnly(2024, 1, 1); // Before startDate

        // Act & Assert
        var act = async () => await backtest.RunAsync(s_startDate, s_endDate, burnInEndDate: burnInEndDate);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// burnInEndDate greater than or equal to endDate → throws ArgumentException.
    /// </summary>
    [Fact]
    public async Task RunAsync_BurnInEndDateAfterEndDate_ThrowsArgumentException()
    {
        // Arrange
        var (portfolioMock, benchmarkMock, fetcherMock) = BuildMocks();
        var backtest = new BackTest(
            portfolioMock.Object,
            benchmarkMock.Object,
            fetcherMock.Object,
            CurrencyCode.USD);

        var burnInEndDate = new DateOnly(2024, 1, 16); // After endDate

        // Act & Assert
        var act = async () => await backtest.RunAsync(s_startDate, s_endDate, burnInEndDate: burnInEndDate);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// burnInEndDate equal to startDate → throws ArgumentException (must be strictly between).
    /// </summary>
    [Fact]
    public async Task RunAsync_BurnInEndDateEqualsStartDate_ThrowsArgumentException()
    {
        // Arrange
        var (portfolioMock, benchmarkMock, fetcherMock) = BuildMocks();
        var backtest = new BackTest(
            portfolioMock.Object,
            benchmarkMock.Object,
            fetcherMock.Object,
            CurrencyCode.USD);

        var burnInEndDate = s_startDate; // Equal to startDate

        // Act & Assert
        var act = async () => await backtest.RunAsync(s_startDate, s_endDate, burnInEndDate: burnInEndDate);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (Mock<IPortfolio> Portfolio, Mock<IPortfolio> Benchmark, Mock<IMarketDataFetcher> Fetcher) BuildMocks()
    {
        var portfolioMock = BuildPortfolioMock(upAmount: 200m, downAmount: -100m);
        var benchmarkMock = BuildPortfolioMock(upAmount: 150m, downAmount: -80m);
        var fetcherMock = BuildFetcherMock();

        return (portfolioMock, benchmarkMock, fetcherMock);
    }

    private static Mock<IPortfolio> BuildPortfolioMock(decimal upAmount, decimal downAmount)
    {
        var mock = new Mock<IPortfolio>();

        var assetCurrencies = new Dictionary<Asset, CurrencyCode> { { s_spy, CurrencyCode.USD } };

        // Strategy mock with SPY asset
        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Assets).Returns(assetCurrencies);

        var strategies = new Dictionary<string, IStrategy> { { "Test", strategyMock.Object } };
        mock.Setup(p => p.Strategies).Returns(strategies);
        mock.Setup(p => p.BaseCurrency).Returns(CurrencyCode.USD);

        // Equity curve — populated during UpdateEquityCurve calls
        var equityCurve = new SortedDictionary<DateOnly, decimal>();
        mock.Setup(p => p.EquityCurve).Returns(equityCurve);

        // Simulate UpdateEquityCurve by adding entries with realistic volatility
        // Alternating up/down to avoid zero downside deviation (Sortino ratio guard)
        // Different amounts per portfolio to avoid zero active return std dev (Information Ratio guard)
        var callCount = 0;
        mock.Setup(p => p.UpdateEquityCurve(It.IsAny<DateOnly>()))
            .Callback<DateOnly>(date =>
            {
                callCount++;
                var change = callCount % 2 == 0 ? upAmount : downAmount;
                var lastValue = equityCurve.Count > 0 ? equityCurve.Values.Last() : 100_000m;
                equityCurve[date] = lastValue + change;
            });

        // HandleEventAsync returns completed task
        mock.Setup(p => p.HandleEventAsync(It.IsAny<IFinancialEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // ProcessPendingOrdersAsync returns completed task
        mock.Setup(p => p.ProcessPendingOrdersAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<SortedDictionary<Asset, MarketData>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return mock;
    }

    private static Mock<IMarketDataFetcher> BuildFetcherMock()
    {
        var mock = new Mock<IMarketDataFetcher>();

        // Build market data for each trading day
        var marketDataPairs = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        foreach (var day in s_tradingDays)
        {
            var dayData = new SortedDictionary<Asset, MarketData>
            {
                {
                    s_spy,
                    new MarketData(
                        Timestamp: day,
                        Open: 100m,
                        High: 101m,
                        Low: 99m,
                        Close: 100.50m,
                        AdjustedClose: 100.50m,
                        Volume: 1_000_000,
                        DividendPerShare: 0,
                        SplitCoefficient: 1)
                }
            };
            marketDataPairs.Add(new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(day, dayData));
        }

        mock.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(marketDataPairs.ToAsyncEnumerable());

        // No FX rates needed for single-currency portfolio
        mock.Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>());

        return mock;
    }
}
