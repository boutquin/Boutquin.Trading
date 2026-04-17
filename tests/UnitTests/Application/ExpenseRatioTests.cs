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

using Boutquin.Trading.Recipes.Testing;

using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Tests for ETF expense ratio deduction in BackTest.
/// </summary>
public sealed class ExpenseRatioTests
{
    [Fact]
    public async Task RunAsync_ZeroExpenseRatio_ShouldNotAffectEquityCurve()
    {
        // Arrange
        var (portfolioZero, benchmarkZero, dataset) = CreateSetup();
        var (portfolioBase, benchmarkBase, _) = CreateSetup();

        var btZero = new BackTest(portfolioZero, benchmarkZero, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: 0m);
        var btBase = new BackTest(portfolioBase, benchmarkBase, CurrencyCode.USD);

        // Act
        await btZero.RunAsync(dataset);
        await btBase.RunAsync(dataset);

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
        var (portfolioFee, benchmarkFee, dataset) = CreateSetup();
        var (portfolioNoFee, benchmarkNoFee, _) = CreateSetup();

        var btFee = new BackTest(portfolioFee, benchmarkFee, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: 100m);
        var btNoFee = new BackTest(portfolioNoFee, benchmarkNoFee, CurrencyCode.USD);

        // Act
        await btFee.RunAsync(dataset);
        await btNoFee.RunAsync(dataset);

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
        var (portfolioFee, benchmarkFee, dataset) = CreateSetup();
        var (portfolioNoFee, benchmarkNoFee, _) = CreateSetup();

        var btFee = new BackTest(portfolioFee, benchmarkFee, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: 100m);
        var btNoFee = new BackTest(portfolioNoFee, benchmarkNoFee, CurrencyCode.USD);

        // Act
        await btFee.RunAsync(dataset);
        await btNoFee.RunAsync(dataset);

        // Assert
        var lastDate = portfolioFee.EquityCurve.Keys.Last();
        var drag = portfolioNoFee.EquityCurve[lastDate] - portfolioFee.EquityCurve[lastDate];
        drag.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Constructor_NegativeExpenseRatio_ShouldThrow()
    {
        var (portfolio, benchmark, _) = CreateSetup();
        var act = () => new BackTest(portfolio, benchmark, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: -10m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ExcessiveExpenseRatio_ShouldThrow()
    {
        var (portfolio, benchmark, _) = CreateSetup();
        var act = () => new BackTest(portfolio, benchmark, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: 1001m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativePerAssetExpenseRatio_ShouldThrow()
    {
        var (portfolio, benchmark, _) = CreateSetup();
        var perAsset = new Dictionary<string, decimal> { { "AAPL", -5m } };
        var act = () => new BackTest(portfolio, benchmark, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: 10m, assetExpenseRatiosBps: perAsset);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task RunAsync_ExpenseRatio_ShouldNotApplyToBenchmark()
    {
        // Arrange
        var (portfolioFee, benchmarkFee, dataset) = CreateSetup();
        var (_, benchmarkNoFee, _) = CreateSetup();

        var btFee = new BackTest(portfolioFee, benchmarkFee, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null, annualExpenseRatioBps: 100m);
        var btNoFee = new BackTest(portfolioFee, benchmarkNoFee, CurrencyCode.USD);

        // Act
        await btFee.RunAsync(dataset);
        await btNoFee.RunAsync(dataset);

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
        var (portfolioPerAsset, benchmarkPerAsset, dataset) = CreateSetup();
        var (portfolioDefault, benchmarkDefault, _) = CreateSetup();

        var perAssetRates = new Dictionary<string, decimal> { { "AAPL", 50m } };

        var btPerAsset = new BackTest(portfolioPerAsset, benchmarkPerAsset, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null,
            annualExpenseRatioBps: 10m, assetExpenseRatiosBps: perAssetRates);

        var btDefault = new BackTest(portfolioDefault, benchmarkDefault, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null,
            annualExpenseRatioBps: 10m);

        // Act
        await btPerAsset.RunAsync(dataset);
        await btDefault.RunAsync(dataset);

        // Assert — per-asset (50bps on AAPL) should drag more than uniform 10bps
        var lastDate = portfolioPerAsset.EquityCurve.Keys.Last();
        portfolioPerAsset.EquityCurve[lastDate].Should().BeLessThan(portfolioDefault.EquityCurve[lastDate]);
    }

    [Fact]
    public async Task RunAsync_PerAssetZeroOverride_ShouldExemptAssetFromFee()
    {
        // Arrange — default 50bps, but AAPL explicitly overridden to 0bps
        var (portfolioZeroOverride, benchmarkZeroOverride, dataset) = CreateSetup();
        var (portfolioNoFee, benchmarkNoFee, _) = CreateSetup();

        var perAssetRates = new Dictionary<string, decimal> { { "AAPL", 0m } };

        var btZeroOverride = new BackTest(portfolioZeroOverride, benchmarkZeroOverride, CurrencyCode.USD,
            NullLogger<BackTest>.Instance, 0m, null, null,
            annualExpenseRatioBps: 50m, assetExpenseRatiosBps: perAssetRates);

        var btNoFee = new BackTest(portfolioNoFee, benchmarkNoFee, CurrencyCode.USD);

        // Act
        await btZeroOverride.RunAsync(dataset);
        await btNoFee.RunAsync(dataset);

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
    private static (IPortfolio portfolio, IPortfolio benchmark, FakeBacktestDataset dataset) CreateSetup()
    {
        var assetPortfolio = new Symbol("AAPL");
        var assetBenchmark = new Symbol("SPY");

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

        var portfolioWeights = new Dictionary<Symbol, decimal> { { assetPortfolio, 1.0m } };
        var benchmarkWeights = new Dictionary<Symbol, decimal> { { assetBenchmark, 1.0m } };
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();

        var strategy = new TestStrategy
        {
            Name = "Main",
            Positions = new SortedDictionary<Symbol, int> { { assetPortfolio, 100 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 10_000m } },
            Assets = new Dictionary<Symbol, CurrencyCode> { { assetPortfolio, CurrencyCode.USD } },
            PositionSizer = new FixedWeightPositionSizer(portfolioWeights, CurrencyCode.USD),
            OrderPriceCalculationStrategy = orderPriceCalc
        };

        var bmStrategy = new TestStrategy
        {
            Name = "Benchmark",
            Positions = new SortedDictionary<Symbol, int> { { assetBenchmark, 100 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 10_000m } },
            Assets = new Dictionary<Symbol, CurrencyCode> { { assetBenchmark, CurrencyCode.USD } },
            PositionSizer = new FixedWeightPositionSizer(benchmarkWeights, CurrencyCode.USD),
            OrderPriceCalculationStrategy = orderPriceCalc
        };

        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { { "Main", strategy } }),
            new Dictionary<Symbol, CurrencyCode> { { assetPortfolio, CurrencyCode.USD } },
            handlers, mockBroker.Object, isLive: false);

        var benchmark = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(new Dictionary<string, IStrategy> { { "Benchmark", bmStrategy } }),
            new Dictionary<Symbol, CurrencyCode> { { assetBenchmark, CurrencyCode.USD } },
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

        var prices = new SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>>();
        for (var i = 0; i < dates.Length; i++)
        {
            prices[dates[i]] = new SortedDictionary<Symbol, Bar>
            {
                { assetPortfolio, new Bar(dates[i], aaplPrices[i], aaplPrices[i] + 0.5m, aaplPrices[i] - 0.5m, aaplPrices[i], aaplPrices[i], 1_000_000) },
                { assetBenchmark, new Bar(dates[i], spyPrices[i], spyPrices[i] + 1m, spyPrices[i] - 1m, spyPrices[i], spyPrices[i], 2_000_000) },
            };
        }

        var dataset = new FakeBacktestDataset
        {
            Prices = prices,
            FxRates = [],
        };

        return (portfolio, benchmark, dataset);
    }
}
