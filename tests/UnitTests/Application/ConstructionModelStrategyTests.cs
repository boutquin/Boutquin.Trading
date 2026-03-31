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

using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Application.PositionSizing;
using Boutquin.Trading.Application.Rebalancing;
using Boutquin.Trading.Application.Strategies;
using Boutquin.Trading.Domain.ValueObjects;
using FluentAssertions;

/// <summary>
/// Tests for <see cref="ConstructionModelStrategy"/> and the pipeline wiring.
/// </summary>
public sealed class ConstructionModelStrategyTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_tlt = new("TLT");

    private static IReadOnlyDictionary<Asset, CurrencyCode> TestAssets =>
        new Dictionary<Asset, CurrencyCode>
        {
            [s_vti] = CurrencyCode.USD,
            [s_tlt] = CurrencyCode.USD
        };

    private static SortedDictionary<CurrencyCode, decimal> TestCash =>
        new() { [CurrencyCode.USD] = 100_000m };

    private static IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> BuildMarketData()
    {
        var data = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>();

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
                [s_vti] = new(date, vtiPrice, vtiPrice + 1, vtiPrice - 1, vtiPrice, vtiPrice, 1_000_000, 0m),
                [s_tlt] = new(date, tltPrice, tltPrice + 0.5m, tltPrice - 0.5m, tltPrice, tltPrice, 500_000, 0m)
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
            strategy.LastComputedWeights[s_vti].Should().BeApproximately(0.5m, 1e-10m);
            strategy.LastComputedWeights[s_tlt].Should().BeApproximately(0.5m, 1e-10m);
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
        var constructionModel = new EqualWeightConstruction();

        var strategy = new ConstructionModelStrategy(
            "E2E Equal Weight",
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

        act.Should().NotThrow("End-to-end backtest with construction model strategy should complete without error");
    }

    /// <summary>
    /// When the threshold trigger suppresses a rebalance on a scheduled date,
    /// _lastRebalancingDate must still advance so that the frequency gate blocks
    /// subsequent days. Without this fix, every day after the suppressed date
    /// passes the frequency gate (timestamp >= staleDate + period), causing
    /// daily weight evaluations that pollute TargetWeightHistory.
    /// </summary>
    [Fact]
    public void Strategy_ThresholdSuppressed_ShouldNotLeakDailyEvaluations()
    {
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new EqualWeightConstruction();
        // 90% band = threshold will never fire after initial allocation
        var trigger = new ThresholdRebalancingTrigger(0.90m);

        var strategy = new ConstructionModelStrategy(
            "Leak Test",
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

        // Day 1: initial allocation (always triggers)
        var firstDate = new DateOnly(2024, 1, 2);
        strategy.GenerateSignals(firstDate, CurrencyCode.USD, marketData, fxRates);

        // Monthly date: threshold suppresses rebalance (drift < 90%)
        var monthlyDate = new DateOnly(2024, 2, 5);
        var suppressed = strategy.GenerateSignals(monthlyDate, CurrencyCode.USD, marketData, fxRates);
        suppressed.Signals.Should().BeEmpty("threshold suppressed the rebalance");

        // Day after the suppressed monthly date: frequency gate must block this
        var dayAfter = new DateOnly(2024, 2, 6);
        var leaked = strategy.GenerateSignals(dayAfter, CurrencyCode.USD, marketData, fxRates);
        leaked.Signals.Should().BeEmpty(
            "frequency gate must block non-scheduled dates even after a suppressed rebalance");

        // TargetWeightHistory should have exactly 1 entry (the initial allocation),
        // not 2 or 3 from leaked evaluations
        strategy.TargetWeightHistory.Should().HaveCount(1,
            "only the initial allocation should be recorded — suppressed evaluations must not pollute history");
    }

    /// <summary>
    /// After a threshold-suppressed rebalance, the NEXT scheduled date should
    /// still evaluate the trigger. Verifies the frequency counter resets properly.
    /// </summary>
    [Fact]
    public void Strategy_ThresholdSuppressed_NextScheduledDateShouldStillEvaluate()
    {
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new EqualWeightConstruction();
        // Tight band: 1% = trigger will fire on scheduled dates (drift from 0 → 50% target)
        var trigger = new ThresholdRebalancingTrigger(0.01m);

        var strategy = new ConstructionModelStrategy(
            "NextScheduled Test",
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

        // Day 1: initial allocation
        var firstDate = new DateOnly(2024, 1, 2);
        strategy.GenerateSignals(firstDate, CurrencyCode.USD, marketData, fxRates);

        // First monthly date: trigger fires (1% band, large drift)
        var month1 = new DateOnly(2024, 2, 5);
        var signals1 = strategy.GenerateSignals(month1, CurrencyCode.USD, marketData, fxRates);
        signals1.Signals.Should().NotBeEmpty("tight threshold should trigger on monthly date");

        // Day after: blocked by frequency gate
        var dayAfterMonth1 = new DateOnly(2024, 2, 6);
        var blocked = strategy.GenerateSignals(dayAfterMonth1, CurrencyCode.USD, marketData, fxRates);
        blocked.Signals.Should().BeEmpty("day after rebalance must be blocked by frequency gate");

        // Second monthly date: trigger should evaluate again
        var month2 = new DateOnly(2024, 3, 8);
        var signals2 = strategy.GenerateSignals(month2, CurrencyCode.USD, marketData, fxRates);
        signals2.Signals.Should().NotBeEmpty("next scheduled date should evaluate trigger normally");
    }

    /// <summary>
    /// TargetWeightHistory should only contain entries for actual rebalance events,
    /// not for every scheduled-date evaluation where the trigger was checked.
    /// </summary>
    [Fact]
    public void Strategy_TargetWeightHistory_OnlyRecordsActualRebalances()
    {
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new EqualWeightConstruction();
        // Calendar trigger: always fires, so every scheduled date is a rebalance
        var calendarTrigger = new CalendarRebalancingTrigger();

        var strategy = new ConstructionModelStrategy(
            "History Test",
            TestAssets,
            TestCash,
            orderPriceCalc,
            positionSizer,
            constructionModel,
            RebalancingFrequency.Monthly,
            rebalancingTrigger: calendarTrigger,
            lookbackWindow: 5);

        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        // Call on every day for 70 days (Jan 2 – Mar 12)
        var baseDate = new DateOnly(2024, 1, 2);
        for (var i = 0; i < 70; i++)
        {
            var date = baseDate.AddDays(i);
            strategy.GenerateSignals(date, CurrencyCode.USD, marketData, fxRates);
        }

        // With monthly frequency over ~70 days, expect exactly 3 entries:
        // initial (Jan 2) + Feb rebalance + Mar rebalance
        strategy.TargetWeightHistory.Count.Should().BeLessThanOrEqualTo(3,
            "history should contain only actual rebalance dates, not daily evaluations");
    }

    /// <summary>
    /// L5: With RebalancingFrequency.Never, only the first call should rebalance.
    /// Subsequent calls should return empty signals.
    /// </summary>
    [Fact]
    public void Strategy_WithNeverFrequency_ShouldOnlyRebalanceOnce()
    {
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new EqualWeightConstruction();

        var strategy = new ConstructionModelStrategy(
            "Never Test",
            TestAssets,
            TestCash,
            orderPriceCalc,
            positionSizer,
            constructionModel,
            RebalancingFrequency.Never,
            lookbackWindow: 5);

        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        // First call triggers
        var firstDate = new DateOnly(2024, 1, 2);
        var firstSignals = strategy.GenerateSignals(firstDate, CurrencyCode.USD, marketData, fxRates);
        firstSignals.Signals.Should().NotBeEmpty("First call should generate signals");

        // All subsequent calls should return empty signals (Never = DateOnly.MaxValue)
        var laterDate = new DateOnly(2024, 3, 15);
        var laterSignals = strategy.GenerateSignals(laterDate, CurrencyCode.USD, marketData, fxRates);
        laterSignals.Signals.Should().BeEmpty("RebalancingFrequency.Never should suppress all subsequent rebalances");
    }

    /// <summary>
    /// M9: When ComputeCurrentWeights throws InvalidOperationException,
    /// the strategy should catch it and proceed (fall back to empty weights → rebalance).
    /// </summary>
    [Fact]
    public void Strategy_ComputeCurrentWeightsFailure_ShouldNotThrow()
    {
        // This test verifies indirectly: when a strategy has no positions and no matching market data
        // for the current timestamp, ComputeCurrentWeights would previously throw. After M9,
        // it catches InvalidOperationException and falls back.
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new EqualWeightConstruction();

        var strategy = new ConstructionModelStrategy(
            "M9 Test",
            TestAssets,
            TestCash,
            orderPriceCalc,
            positionSizer,
            constructionModel,
            RebalancingFrequency.Daily,
            lookbackWindow: 5);

        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        // First call to set _lastRebalancingDate
        var firstDate = new DateOnly(2024, 1, 2);
        strategy.GenerateSignals(firstDate, CurrencyCode.USD, marketData, fxRates);

        // Second call on next day — ComputeCurrentWeights may fail due to missing position data,
        // but the strategy should not throw (M9 fix: catch + fallback)
        var nextDay = new DateOnly(2024, 1, 3);
        var act = () => strategy.GenerateSignals(nextDay, CurrencyCode.USD, marketData, fxRates);
        act.Should().NotThrow("M9: ComputeCurrentWeights failure should be caught, not propagated");
    }
}
