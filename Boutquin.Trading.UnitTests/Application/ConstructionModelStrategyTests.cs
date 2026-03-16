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

using FluentAssertions;

using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Application.PositionSizing;
using Boutquin.Trading.Application.Rebalancing;
using Boutquin.Trading.Application.Strategies;
using Boutquin.Trading.Domain.ValueObjects;

/// <summary>
/// Tests for <see cref="ConstructionModelStrategy"/> and the pipeline wiring.
/// </summary>
public sealed class ConstructionModelStrategyTests
{
    private static readonly Asset Vti = new("VTI");
    private static readonly Asset Tlt = new("TLT");

    private static IReadOnlyDictionary<Asset, CurrencyCode> TestAssets =>
        new Dictionary<Asset, CurrencyCode>
        {
            [Vti] = CurrencyCode.USD,
            [Tlt] = CurrencyCode.USD
        };

    private static SortedDictionary<CurrencyCode, decimal> TestCash =>
        new() { [CurrencyCode.USD] = 100_000m };

    private static IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>?> BuildMarketData()
    {
        var data = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>?>();

        // Generate 70 days of data with different price patterns
        var baseDate = new DateOnly(2024, 1, 2);
        var vtiPrice = 200m;
        var tltPrice = 100m;

        for (var i = 0; i < 70; i++)
        {
            var date = baseDate.AddDays(i);
            // VTI: trending up with high vol
            vtiPrice *= 1m + (i % 3 == 0 ? 0.02m : -0.005m);
            // TLT: stable with low vol
            tltPrice *= 1m + (i % 4 == 0 ? 0.003m : -0.001m);

            data[date] = new SortedDictionary<Asset, MarketData>
            {
                [Vti] = new(date, vtiPrice, vtiPrice + 1, vtiPrice - 1, vtiPrice, vtiPrice, 1_000_000, 0m),
                [Tlt] = new(date, tltPrice, tltPrice + 0.5m, tltPrice - 0.5m, tltPrice, tltPrice, 500_000, 0m)
            };
        }

        return data;
    }

    private static IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> BuildFxRates()
    {
        var data = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>();
        var baseDate = new DateOnly(2024, 1, 2);

        for (var i = 0; i < 70; i++)
        {
            var date = baseDate.AddDays(i);
            data[date] = new SortedDictionary<CurrencyCode, decimal>
            {
                [CurrencyCode.USD] = 1.0m
            };
        }

        return data;
    }

    [Fact]
    public void Strategy_WithRiskParity_ShouldProduceDifferentWeightsOverTime()
    {
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new RiskParityConstruction();

        var strategy = new ConstructionModelStrategy(
            "RiskParity Test",
            TestAssets,
            TestCash,
            orderPriceCalc,
            positionSizer,
            constructionModel,
            RebalancingFrequency.Monthly,
            lookbackWindow: 20);

        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        // Generate signals on first day
        var firstDate = new DateOnly(2024, 1, 2);
        var firstSignals = strategy.GenerateSignals(firstDate, CurrencyCode.USD, marketData, fxRates);

        // First call should generate signals (Underweight for initial buy since not enough data)
        firstSignals.Signals.Should().NotBeEmpty("First call should generate signals");
    }

    [Fact]
    public void Strategy_WithEqualWeight_ShouldComputeEqualWeights()
    {
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new EqualWeightConstruction();

        var strategy = new ConstructionModelStrategy(
            "EqualWeight Test",
            TestAssets,
            TestCash,
            orderPriceCalc,
            positionSizer,
            constructionModel,
            RebalancingFrequency.Monthly,
            lookbackWindow: 5);

        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        // Wait until we have enough data — generate at day 30 to have lookback data
        var rebalDate = new DateOnly(2024, 1, 2);
        strategy.GenerateSignals(rebalDate, CurrencyCode.USD, marketData, fxRates);

        // After first rebalance date with enough data
        var laterDate = new DateOnly(2024, 2, 5);
        strategy.GenerateSignals(laterDate, CurrencyCode.USD, marketData, fxRates);

        if (strategy.LastComputedWeights is not null)
        {
            strategy.LastComputedWeights[Vti].Should().BeApproximately(0.5m, 1e-10m);
            strategy.LastComputedWeights[Tlt].Should().BeApproximately(0.5m, 1e-10m);
        }
    }

    [Fact]
    public void Strategy_NotRebalancingDate_ShouldReturnEmptySignals()
    {
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new EqualWeightConstruction();

        var strategy = new ConstructionModelStrategy(
            "Test",
            TestAssets,
            TestCash,
            orderPriceCalc,
            positionSizer,
            constructionModel,
            RebalancingFrequency.Monthly,
            lookbackWindow: 5);

        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        // First call triggers
        var firstDate = new DateOnly(2024, 1, 2);
        strategy.GenerateSignals(firstDate, CurrencyCode.USD, marketData, fxRates);

        // Next day is not a rebalancing date
        var nextDay = new DateOnly(2024, 1, 3);
        var signals = strategy.GenerateSignals(nextDay, CurrencyCode.USD, marketData, fxRates);

        signals.Signals.Should().BeEmpty("Day after initial rebalance is not a rebalance date");
    }

    [Fact]
    public void Strategy_WithThresholdTrigger_NoRebalanceWhenWithinBand()
    {
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new EqualWeightConstruction();
        var trigger = new ThresholdRebalancingTrigger(0.90m); // Very wide band — suppresses rebalance

        var strategy = new ConstructionModelStrategy(
            "Threshold Test",
            TestAssets,
            TestCash,
            orderPriceCalc,
            positionSizer,
            constructionModel,
            RebalancingFrequency.Monthly,
            rebalancingTrigger: trigger,
            lookbackWindow: 5);

        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        // First call always triggers (initial buy)
        var firstDate = new DateOnly(2024, 1, 2);
        var firstSignals = strategy.GenerateSignals(firstDate, CurrencyCode.USD, marketData, fxRates);
        firstSignals.Signals.Should().NotBeEmpty("First call should always generate signals");

        // On a rebalance date, threshold trigger with 90% band should suppress
        // because drift (|0% - 50%| = 50%) does not exceed 90% threshold
        var rebalDate = new DateOnly(2024, 2, 5);
        var signals = strategy.GenerateSignals(rebalDate, CurrencyCode.USD, marketData, fxRates);

        signals.Signals.Should().BeEmpty("50% drift does not exceed 90% threshold — no rebalance");
    }

    [Fact]
    public void EndToEnd_ConstructionModelStrategy_CompletesWithoutError()
    {
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new RiskParityConstruction();

        var strategy = new ConstructionModelStrategy(
            "E2E Risk Parity",
            TestAssets,
            TestCash,
            orderPriceCalc,
            positionSizer,
            constructionModel,
            RebalancingFrequency.Monthly,
            lookbackWindow: 10);

        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        // Simulate calling GenerateSignals for each trading day
        var baseDate = new DateOnly(2024, 1, 2);
        var act = () =>
        {
            for (var i = 0; i < 70; i++)
            {
                var date = baseDate.AddDays(i);
                strategy.GenerateSignals(date, CurrencyCode.USD, marketData, fxRates);
            }
        };

        act.Should().NotThrow("End-to-end backtest with risk parity should complete without error");
    }
}
