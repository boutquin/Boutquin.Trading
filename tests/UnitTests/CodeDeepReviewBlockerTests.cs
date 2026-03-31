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

using Boutquin.Trading.Application.Analytics;
using Boutquin.Trading.Application.Configuration;
using Boutquin.Trading.Application.DownsideRisk;
using Boutquin.Trading.Application.Regime;
using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Trading.Tests.UnitTests;

/// <summary>
/// TDD tests for the 12 required-severity blockers from the 2026-03-26 code-deep review.
/// Each test reproduces the undesirable behavior, then the fix makes it pass.
/// </summary>
public sealed class CodeDeepReviewBlockerTests
{
    // ══════════════════════════════════════════════════════════════════
    // BLOCKER #1: MarketData.AdjustForSplit truncates fractional volume
    // on reverse splits (should round, not truncate)
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void AdjustForSplit_ReverseSplit_RoundsVolumeInsteadOfTruncating()
    {
        // Arrange — volume 500, ratio 1/3 → 500 * (1/3) = 166.666...
        // Truncation gives 166; Math.Round(AwayFromZero) gives 167
        var md = new MarketData(
            Timestamp: new DateOnly(2024, 1, 15),
            Open: 30m, High: 35m, Low: 28m, Close: 33m,
            AdjustedClose: 33m, Volume: 500,
            DividendPerShare: 0m, SplitCoefficient: 1m);

        // Act — reverse split: 3 shares become 1 (ratio = 1/3)
        var result = md.AdjustForSplit(1m / 3m);

        // Assert — Volume * (1/3) = 166.666... should round to 167, not truncate to 166
        result.Volume.Should().Be(167);
    }

    // ══════════════════════════════════════════════════════════════════
    // BLOCKER #2: CAGR does not guard against negative cumulative return
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void CAGR_NegativeCumulativeReturn_ThrowsCalculationException()
    {
        // Arrange — returns that produce cumulative return < 0 (net < -100%)
        // (1 + (-1.5)) = -0.5 → negative cumulative
        var dailyReturns = new[] { -1.5m, 0.01m };

        // Act
        var act = () => dailyReturns.CompoundAnnualGrowthRate();

        // Assert — should throw CalculationException, not silently return 0
        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }

    // ══════════════════════════════════════════════════════════════════
    // BLOCKER #3: DrawdownAnalyzer uses calendar days instead of
    // trading days (equity curve entry count)
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void DrawdownAnalyzer_DurationUsesTradingDays_NotCalendarDays()
    {
        // Arrange — equity curve with a weekend gap:
        // Fri Jan 5 = 100 (peak), Mon Jan 8 = 90 (trough), Tue Jan 9 = 100 (recovery)
        // Calendar days: Jan 5 to Jan 9 = 4 days
        // Trading days (entry indices): peak at index 0, recovery at index 2 → 2 trading days
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2024, 1, 5), 100m }, // Friday (peak)
            { new DateOnly(2024, 1, 8), 90m },  // Monday (trough, weekend gap)
            { new DateOnly(2024, 1, 9), 100m }, // Tuesday (recovery)
        };

        // Act
        var periods = DrawdownAnalyzer.AnalyzeDrawdownPeriods(equityCurve);

        // Assert — duration should be 2 trading days (entry count), not 4 calendar days
        periods.Should().HaveCount(1);
        periods[0].DurationDays.Should().Be(2, "duration should count trading days (entry indices), not calendar days");
        periods[0].RecoveryDays.Should().Be(1, "recovery should count trading days from trough to recovery");
    }

    // ══════════════════════════════════════════════════════════════════
    // BLOCKER #4: CVaRRiskMeasure._zeta leaks between optimization runs
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void CVaRRiskMeasure_Reset_RestoresInitialState()
    {
        // Arrange
        var cvar = new CVaRRiskMeasure(0.95m);

        var weights = new[] { 0.5m, 0.5m };
        var scenarios = new[]
        {
            new[] { -0.10m, 0.05m },
            new[] { -0.20m, -0.15m },
            new[] { 0.05m, 0.10m },
            new[] { -0.30m, -0.25m },
        };

        // Run first "optimization" — modifies _zeta internally
        for (var i = 0; i < 50; i++)
        {
            cvar.Evaluate(weights, scenarios, 0.1m);
        }

        var (firstResult, _) = cvar.Evaluate(weights, scenarios, 0.1m);

        // Act — reset and run second identical optimization
        cvar.Reset();
        for (var i = 0; i < 50; i++)
        {
            cvar.Evaluate(weights, scenarios, 0.1m);
        }

        var (secondResult, _) = cvar.Evaluate(weights, scenarios, 0.1m);

        // Assert — both runs should converge to same result
        secondResult.Should().BeApproximately(firstResult, 1e-10m,
            "Reset() should restore _zeta to initial state so independent runs converge identically");
    }

    // ══════════════════════════════════════════════════════════════════
    // BLOCKER #5: GrowthInflationRegimeClassifier._priorRegime leak
    // (already has Reset() — verify it works correctly)
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void RegimeClassifier_Reset_ClearsPriorRegime()
    {
        // Arrange — classify to establish prior regime
        var classifier = new GrowthInflationRegimeClassifier(deadband: 0.01m);
        classifier.Classify(0.05m, 0.05m); // Rising/Rising

        // Ambiguous signals use prior → Rising/Rising
        var withPrior = classifier.Classify(0.005m, 0.005m);
        withPrior.Should().Be(EconomicRegime.RisingGrowthRisingInflation);

        // Act
        classifier.Reset();

        // Same ambiguous signals without prior → Falling/Falling
        var afterReset = classifier.Classify(0.005m, 0.005m);

        // Assert
        afterReset.Should().Be(EconomicRegime.FallingGrowthFallingInflation,
            "Reset() should clear prior regime so ambiguous signals don't inherit old state");
    }

    // ══════════════════════════════════════════════════════════════════
    // BLOCKER #6 + #7: TwelveDataFetcher disposal + cancellation
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TwelveDataFetcher_CancelledToken_PropagatesOperationCanceledException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"values":[{"datetime":"2024-01-15","open":"100","high":"110","low":"90","close":"105","volume":"1000000"}]}""",
                    System.Text.Encoding.UTF8, "application/json")
            });

        using var httpClient = new HttpClient(handler);
        using var fetcher = new Boutquin.Trading.Data.TwelveData.TwelveDataFetcher("test-key", httpClient, "https://api.example.com");

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchMarketDataAsync(
                [new Asset("AAPL")], cts.Token))
            {
            }
        };

        // Assert — should throw OperationCanceledException, not silently return empty
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ══════════════════════════════════════════════════════════════════
    // BLOCKER #8: CsvMarketDataFetcher zero rawClose guard
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CsvMarketDataFetcher_ZeroRawClose_ThrowsMarketDataRetrievalException()
    {
        // Arrange — CSV with rawClose = 0
        var tempDir = Path.Combine(Path.GetTempPath(), $"csv_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var csvContent = "Date,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient\n"
                + "2024-01-15,100,110,90,0,105,1000000,0,1\n";
            var fileName = MarketDataFileNameHelper.GetCsvFileNameForMarketData(tempDir, "TEST");
            await File.WriteAllTextAsync(fileName, csvContent);

            var fetcher = new CsvMarketDataFetcher(tempDir);

            // Act
            var act = async () =>
            {
                await foreach (var _ in fetcher.FetchMarketDataAsync([new Asset("TEST")], CancellationToken.None))
                {
                }
            };

            // Assert
            await act.Should().ThrowAsync<MarketDataRetrievalException>();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // BLOCKER #9: FredFetcher API key not URL-encoded
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FredFetcher_ApiKeyWithSpecialChars_IsUrlEncoded()
    {
        // Arrange
        var specialKey = "abc+def=ghi&jkl";
        string? capturedUrl = null;

        var handler = new MockHttpMessageHandler(request =>
        {
            capturedUrl = request.RequestUri?.ToString();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"observations":[]}""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        using var fetcher = new Boutquin.Trading.Data.Fred.FredFetcher(specialKey, httpClient, "https://api.example.com");

        // Act
        await foreach (var _ in fetcher.FetchSeriesAsync("TEST_SERIES"))
        {
        }

        // Assert
        capturedUrl.Should().NotBeNull();
        capturedUrl.Should().NotContain("abc+def=ghi&jkl",
            "raw special characters should be URL-encoded");
        capturedUrl.Should().Contain(Uri.EscapeDataString(specialKey));
    }

    // ══════════════════════════════════════════════════════════════════
    // BLOCKER #10: IRiskManager wired into OrderEventHandler
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OrderEventHandler_WithRiskManager_RejectsOrderWhenRiskRuleFails()
    {
        // Arrange
        var mockPortfolio = new Mock<IPortfolio>();
        mockPortfolio.Setup(p => p.SubmitOrderAsync(It.IsAny<OrderEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockRiskManager = new Mock<IRiskManager>();
        mockRiskManager.Setup(r => r.Evaluate(It.IsAny<Order>(), It.IsAny<IPortfolio>()))
            .Returns(RiskEvaluation.Rejected("Max drawdown exceeded"));

        var handler = new OrderEventHandler(
            NullLogger<OrderEventHandler>.Instance,
            mockRiskManager.Object);

        var orderEvent = new OrderEvent(
            new DateOnly(2024, 1, 15), "TestStrategy", new Asset("AAPL"),
            TradeAction.Buy, OrderType.Market, 100);

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, orderEvent, CancellationToken.None);

        // Assert — order should NOT be submitted when risk check fails
        mockPortfolio.Verify(
            p => p.SubmitOrderAsync(It.IsAny<OrderEvent>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Order should be rejected before submission when risk rule fails");
    }

    [Fact]
    public async Task OrderEventHandler_WithRiskManager_SubmitsOrderWhenRiskRulePasses()
    {
        // Arrange
        var mockPortfolio = new Mock<IPortfolio>();
        mockPortfolio.Setup(p => p.SubmitOrderAsync(It.IsAny<OrderEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockRiskManager = new Mock<IRiskManager>();
        mockRiskManager.Setup(r => r.Evaluate(It.IsAny<Order>(), It.IsAny<IPortfolio>()))
            .Returns(RiskEvaluation.Allowed);

        var handler = new OrderEventHandler(
            NullLogger<OrderEventHandler>.Instance,
            mockRiskManager.Object);

        var orderEvent = new OrderEvent(
            new DateOnly(2024, 1, 15), "TestStrategy", new Asset("AAPL"),
            TradeAction.Buy, OrderType.Market, 100);

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, orderEvent, CancellationToken.None);

        // Assert — order should be submitted
        mockPortfolio.Verify(
            p => p.SubmitOrderAsync(It.IsAny<OrderEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OrderEventHandler_WithoutRiskManager_SubmitsOrderDirectly()
    {
        // Arrange — backward-compatible constructor (no risk manager)
        var mockPortfolio = new Mock<IPortfolio>();
        mockPortfolio.Setup(p => p.SubmitOrderAsync(It.IsAny<OrderEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new OrderEventHandler();

        var orderEvent = new OrderEvent(
            new DateOnly(2024, 1, 15), "TestStrategy", new Asset("AAPL"),
            TradeAction.Buy, OrderType.Market, 100);

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, orderEvent, CancellationToken.None);

        // Assert — should submit without risk check (backward compatibility)
        mockPortfolio.Verify(
            p => p.SubmitOrderAsync(It.IsAny<OrderEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ══════════════════════════════════════════════════════════════════
    // BLOCKER #11: Unused BacktestOptions properties removed
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void BacktestOptions_OnlyHasWiredProperties()
    {
        var optionsType = typeof(BacktestOptions);
        var propertyNames = optionsType.GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // Only ConstructionModel is consumed by ServiceCollectionExtensions
        propertyNames.Should().Contain("ConstructionModel");

        // These were unused illusion-of-control properties — should be removed
        propertyNames.Should().NotContain("StartDate");
        propertyNames.Should().NotContain("EndDate");
        propertyNames.Should().NotContain("BaseCurrency");
        propertyNames.Should().NotContain("RebalancingFrequency");
        propertyNames.Should().NotContain("RiskFreeRate");
        propertyNames.Should().NotContain("BurnInEndDate");
        propertyNames.Should().NotContain("CashBufferPercent");
        propertyNames.Should().NotContain("MinimumTradeValue");
    }

    // ══════════════════════════════════════════════════════════════════
    // BLOCKER #12: ExchangeExtensions.GetClosingTime day.Date
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetClosingTime_NonMidnightDateTime_UsesDatePortion()
    {
        // Arrange
        var city = new City("New York", TimeZoneCode.EST, CountryCode.US);
        var exchange = new Exchange(ExchangeCode.XNYS, "New York Stock Exchange", city);
        exchange.ExchangeSchedules.Add(
            new ExchangeSchedule(ExchangeCode.XNYS, DayOfWeek.Monday,
                new TimeSpan(9, 30, 0), new TimeSpan(16, 0, 0)));

        // day with existing time component (10:30 AM Monday)
        var dayWithTime = new DateTime(2024, 1, 15, 10, 30, 0);

        // Act
        var closingTime = exchange.GetClosingTime(dayWithTime);

        // Assert — should be 16:00, NOT 10:30 + 16:00 = next day 02:30
        closingTime.Should().NotBeNull();
        closingTime!.Value.Hour.Should().Be(16);
        closingTime.Value.Minute.Should().Be(0);
    }

    // ══════════════════════════════════════════════════════════════════
    // Helper: MockHttpMessageHandler
    // ══════════════════════════════════════════════════════════════════

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_handler(request));
        }
    }
}
