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

using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Tests for ETF expense ratio deduction in BackTest.
/// </summary>
public sealed class ExpenseRatioTests
{
    private static readonly DateOnly s_start = new(2024, 1, 2);
    private static readonly DateOnly s_end = new(2024, 1, 8);

    [Fact]
    public async Task RunAsync_ZeroExpenseRatio_ShouldNotAffectEquityCurve()
    {
        // Arrange
        var (portfolioZero, benchmarkZero, fetcher) = CreateSetup();
        var (portfolioBase, benchmarkBase, _) = CreateSetup();

        var btZero = new BackTest(portfolioZero, benchmarkZero, fetcher, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: 0m);
        var btBase = new BackTest(portfolioBase, benchmarkBase, fetcher, CurrencyCode.USD);

        // Act
        await btZero.RunAsync(s_start, s_end);
        await btBase.RunAsync(s_start, s_end);

        // Assert — identical equity curves
        foreach (var date in portfolioZero.EquityCurve.Keys)
        {
            portfolioZero.EquityCurve[date].Should().Be(portfolioBase.EquityCurve[date]);
        }
    }

    [Fact]
    public async Task RunAsync_PositiveExpenseRatio_ShouldReduceEquityCurve()
    {
        // Arrange
        var (portfolioFee, benchmarkFee, fetcher) = CreateSetup();
        var (portfolioNoFee, benchmarkNoFee, _) = CreateSetup();

        var btFee = new BackTest(portfolioFee, benchmarkFee, fetcher, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: 100m);
        var btNoFee = new BackTest(portfolioNoFee, benchmarkNoFee, fetcher, CurrencyCode.USD);

        // Act
        await btFee.RunAsync(s_start, s_end);
        await btNoFee.RunAsync(s_start, s_end);

        // Assert — with-fee equity strictly lower on every date
        foreach (var date in portfolioFee.EquityCurve.Keys)
        {
            portfolioFee.EquityCurve[date].Should().BeLessThan(portfolioNoFee.EquityCurve[date]);
        }
    }

    [Fact]
    public async Task RunAsync_HighExpenseRatio_ShouldProduceMeasurableDrag()
    {
        // Arrange — 100bps annual
        var (portfolioFee, benchmarkFee, fetcher) = CreateSetup();
        var (portfolioNoFee, benchmarkNoFee, _) = CreateSetup();

        var btFee = new BackTest(portfolioFee, benchmarkFee, fetcher, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: 100m);
        var btNoFee = new BackTest(portfolioNoFee, benchmarkNoFee, fetcher, CurrencyCode.USD);

        // Act
        await btFee.RunAsync(s_start, s_end);
        await btNoFee.RunAsync(s_start, s_end);

        // Assert
        var lastDate = portfolioFee.EquityCurve.Keys.Last();
        var drag = portfolioNoFee.EquityCurve[lastDate] - portfolioFee.EquityCurve[lastDate];
        drag.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Constructor_NegativeExpenseRatio_ShouldThrow()
    {
        var (portfolio, benchmark, fetcher) = CreateSetup();
        var act = () => new BackTest(portfolio, benchmark, fetcher, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: -10m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ExcessiveExpenseRatio_ShouldThrow()
    {
        var (portfolio, benchmark, fetcher) = CreateSetup();
        var act = () => new BackTest(portfolio, benchmark, fetcher, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: 1001m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativePerAssetExpenseRatio_ShouldThrow()
    {
        var (portfolio, benchmark, fetcher) = CreateSetup();
        var perAsset = new Dictionary<string, decimal> { { "AAPL", -5m } };
        var act = () => new BackTest(portfolio, benchmark, fetcher, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: 10m, assetExpenseRatiosBps: perAsset);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task RunAsync_ExpenseRatio_ShouldNotApplyToBenchmark()
    {
        // Arrange
        var (portfolioFee, benchmarkFee, fetcherFee) = CreateSetup();
        var (_, benchmarkNoFee, _) = CreateSetup();

        var btFee = new BackTest(portfolioFee, benchmarkFee, fetcherFee, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: 100m);
        var btNoFee = new BackTest(portfolioFee, benchmarkNoFee, fetcherFee, CurrencyCode.USD);

        // Act
        await btFee.RunAsync(s_start, s_end);
        await btNoFee.RunAsync(s_start, s_end);

        // Assert — benchmark equity curves should be identical (no expense applied)
        foreach (var date in benchmarkFee.EquityCurve.Keys)
        {
            benchmarkFee.EquityCurve[date].Should().Be(benchmarkNoFee.EquityCurve[date]);
        }
    }

    [Fact]
    public async Task RunAsync_PerAssetExpenseRatio_ShouldOverrideDefault()
    {
        // Arrange — default 10bps, AAPL override 50bps
        var (portfolioPerAsset, benchmarkPerAsset, fetcher) = CreateSetup();
        var (portfolioDefault, benchmarkDefault, _) = CreateSetup();

        var perAssetRates = new Dictionary<string, decimal> { { "AAPL", 50m } };

        var btPerAsset = new BackTest(portfolioPerAsset, benchmarkPerAsset, fetcher, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null,
            annualExpenseRatioBps: 10m, assetExpenseRatiosBps: perAssetRates);

        var btDefault = new BackTest(portfolioDefault, benchmarkDefault, fetcher, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null,
            annualExpenseRatioBps: 10m);

        // Act
        await btPerAsset.RunAsync(s_start, s_end);
        await btDefault.RunAsync(s_start, s_end);

        // Assert — per-asset (50bps on AAPL) should drag more than uniform 10bps
        var lastDate = portfolioPerAsset.EquityCurve.Keys.Last();
        portfolioPerAsset.EquityCurve[lastDate].Should().BeLessThan(portfolioDefault.EquityCurve[lastDate]);
    }

    [Fact]
    public async Task RunAsync_PerAssetZeroOverride_ShouldExemptAssetFromFee()
    {
        // Arrange — default 50bps, but AAPL explicitly overridden to 0bps
        var (portfolioZeroOverride, benchmarkZeroOverride, fetcher) = CreateSetup();
        var (portfolioNoFee, benchmarkNoFee, _) = CreateSetup();

        var perAssetRates = new Dictionary<string, decimal> { { "AAPL", 0m } };

        var btZeroOverride = new BackTest(portfolioZeroOverride, benchmarkZeroOverride, fetcher, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null,
            annualExpenseRatioBps: 50m, assetExpenseRatiosBps: perAssetRates);

        var btNoFee = new BackTest(portfolioNoFee, benchmarkNoFee, fetcher, CurrencyCode.USD);

        // Act
        await btZeroOverride.RunAsync(s_start, s_end);
        await btNoFee.RunAsync(s_start, s_end);

        // Assert — AAPL position fee is 0 (overridden), only cash fee at default 50bps applies.
        // Cash drag should be very small compared to position-level fee, so equity curves
        // should be much closer than if the default 50bps applied to positions.
        var lastDate = portfolioZeroOverride.EquityCurve.Keys.Last();
        var dragWithZeroOverride = portfolioNoFee.EquityCurve[lastDate] - portfolioZeroOverride.EquityCurve[lastDate];

        // The drag should be positive (cash fee still applies) but very small
        dragWithZeroOverride.Should().BeGreaterThanOrEqualTo(0m);
        // Cash is ~10000, position is ~10000 (100 shares * ~100). With zero override on position,
        // only cash portion (10000 * 50bps/252/day * 5 days ≈ $0.10) is charged.
        dragWithZeroOverride.Should().BeLessThan(1m, "only cash-level fee should apply when position fee is zero-rated");
    }

    /// <summary>
    /// Creates portfolio (AAPL) + benchmark (SPY) with different price series
    /// to ensure non-zero tracking error for InformationRatio calculation.
    /// </summary>
    private static (IPortfolio portfolio, IPortfolio benchmark, IMarketDataFetcher fetcher) CreateSetup()
    {
        var assetPortfolio = new Asset("AAPL");
        var assetBenchmark = new Asset("SPY");

        var mockBroker = new Mock<IBrokerage>();
        mockBroker.Setup(b => b.SubmitOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handlers = new Dictionary<Type, IEventHandler>
        {
            { typeof(OrderEvent), new OrderEventHandler() },
            { typeof(MarketEvent), new MarketEventHandler() },
            { typeof(FillEvent), new FillEventHandler() },
            { typeof(SignalEvent), new SignalEventHandler() }
        };

        var portfolioWeights = new Dictionary<Asset, decimal> { { assetPortfolio, 1.0m } };
        var benchmarkWeights = new Dictionary<Asset, decimal> { { assetBenchmark, 1.0m } };
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();

        var strategy = new TestStrategy
        {
            Name = "Main",
            Positions = new SortedDictionary<Asset, int> { { assetPortfolio, 100 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 10_000m } },
            Assets = new Dictionary<Asset, CurrencyCode> { { assetPortfolio, CurrencyCode.USD } },
            PositionSizer = new FixedWeightPositionSizer(portfolioWeights, CurrencyCode.USD),
            OrderPriceCalculationStrategy = orderPriceCalc
        };

        var bmStrategy = new TestStrategy
        {
            Name = "Benchmark",
            Positions = new SortedDictionary<Asset, int> { { assetBenchmark, 100 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 10_000m } },
            Assets = new Dictionary<Asset, CurrencyCode> { { assetBenchmark, CurrencyCode.USD } },
            PositionSizer = new FixedWeightPositionSizer(benchmarkWeights, CurrencyCode.USD),
            OrderPriceCalculationStrategy = orderPriceCalc
        };

        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { { "Main", strategy } }),
            new Dictionary<Asset, CurrencyCode> { { assetPortfolio, CurrencyCode.USD } },
            handlers, mockBroker.Object, isLive: false);

        var benchmark = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { { "Benchmark", bmStrategy } }),
            new Dictionary<Asset, CurrencyCode> { { assetBenchmark, CurrencyCode.USD } },
            handlers, mockBroker.Object, isLive: false);

        // Different price series → non-zero tracking error
        var dates = new[]
        {
            new DateOnly(2024, 1, 2), new DateOnly(2024, 1, 3),
            new DateOnly(2024, 1, 4), new DateOnly(2024, 1, 5),
            new DateOnly(2024, 1, 8)
        };
        var aaplPrices = new[] { 100.00m, 100.50m, 99.80m, 100.20m, 100.60m };
        var spyPrices = new[] { 450.00m, 451.00m, 449.50m, 450.50m, 452.00m };

        var fetcher = new Mock<IMarketDataFetcher>();
        fetcher.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateTwoAssetStream(assetPortfolio, aaplPrices, assetBenchmark, spyPrices, dates));
        fetcher.Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>());

        return (portfolio, benchmark, fetcher.Object);
    }

    private static async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>> CreateTwoAssetStream(
        Asset asset1, decimal[] prices1, Asset asset2, decimal[] prices2, DateOnly[] dates)
    {
        for (var i = 0; i < dates.Length; i++)
        {
            var dict = new SortedDictionary<Asset, MarketData>
            {
                {
                    asset1, new MarketData(dates[i], prices1[i], prices1[i] + 0.5m, prices1[i] - 0.5m,
                        prices1[i], prices1[i], 1_000_000, 0m, 1m)
                },
                {
                    asset2, new MarketData(dates[i], prices2[i], prices2[i] + 1m, prices2[i] - 1m,
                        prices2[i], prices2[i], 2_000_000, 0m, 1m)
                }
            };
            yield return new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(dates[i], dict);
        }

        await Task.CompletedTask;
    }
}
