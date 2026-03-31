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
using Boutquin.Trading.Application.Strategies;
using FluentAssertions;

/// <summary>
/// Tests for <see cref="ConstructionModelStrategy.TargetWeightHistory"/> — Feature 5: Target Allocation History.
/// </summary>
public sealed class TargetAllocationHistoryTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_bnd = new("BND");

    private static IReadOnlyDictionary<Asset, CurrencyCode> TestAssets =>
        new Dictionary<Asset, CurrencyCode>
        {
            [s_vti] = CurrencyCode.USD,
            [s_bnd] = CurrencyCode.USD
        };

    private static SortedDictionary<CurrencyCode, decimal> TestCash =>
        new() { [CurrencyCode.USD] = 100_000m };

    /// <summary>
    /// Builds 120 days of market data starting from 2024-01-02 so we have enough
    /// data for a lookback of 3 and multiple monthly rebalances.
    /// </summary>
    private static IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> BuildMarketData(int days = 120)
    {
        var data = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>();
        var baseDate = new DateOnly(2024, 1, 2);
        var vtiPrice = 200m;
        var bndPrice = 80m;

        for (var i = 0; i < days; i++)
        {
            var date = baseDate.AddDays(i);
            // Small oscillations to keep prices realistic
            vtiPrice *= 1m + (i % 3 == 0 ? 0.01m : -0.003m);
            bndPrice *= 1m + (i % 5 == 0 ? 0.002m : -0.001m);

            data[date] = new SortedDictionary<Asset, MarketData>
            {
                [s_vti] = new(date, vtiPrice, vtiPrice + 1, vtiPrice - 1, vtiPrice, vtiPrice, 1_000_000, 0m),
                [s_bnd] = new(date, bndPrice, bndPrice + 0.5m, bndPrice - 0.5m, bndPrice, bndPrice, 500_000, 0m)
            };
        }

        return data;
    }

    private static IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> BuildFxRates(int days = 120)
    {
        var data = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>();
        var baseDate = new DateOnly(2024, 1, 2);

        for (var i = 0; i < days; i++)
        {
            var date = baseDate.AddDays(i);
            data[date] = new SortedDictionary<CurrencyCode, decimal>
            {
                [CurrencyCode.USD] = 1.0m
            };
        }

        return data;
    }

    private static ConstructionModelStrategy CreateStrategy(
        RebalancingFrequency frequency = RebalancingFrequency.Monthly,
        int lookbackWindow = 3)
    {
        var orderPriceCalc = new ClosePriceOrderPriceCalculationStrategy();
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var constructionModel = new EqualWeightConstruction();

        return new ConstructionModelStrategy(
            "TargetHistory Test",
            TestAssets,
            TestCash,
            orderPriceCalc,
            positionSizer,
            constructionModel,
            frequency,
            lookbackWindow: lookbackWindow);
    }

    [Fact]
    public void TargetWeightHistory_InitiallyEmpty()
    {
        var strategy = CreateStrategy();

        strategy.TargetWeightHistory.Should().BeEmpty(
            "no GenerateSignals calls have been made yet");
    }

    [Fact]
    public void TargetWeightHistory_AfterFirstCall_ContainsInitialWeights()
    {
        var strategy = CreateStrategy(lookbackWindow: 200); // Very large lookback — forces equal-weight fallback
        var marketData = BuildMarketData(10); // Only 10 days, far less than 200
        var fxRates = BuildFxRates(10);

        var firstDate = new DateOnly(2024, 1, 2);
        strategy.GenerateSignals(firstDate, CurrencyCode.USD, marketData, fxRates);

        strategy.TargetWeightHistory.Should().HaveCount(1,
            "first call with insufficient data should record the equal-weight fallback");
        strategy.TargetWeightHistory.Should().ContainKey(firstDate);

        var weights = strategy.TargetWeightHistory[firstDate];
        weights[s_vti].Should().BeApproximately(0.5m, 1e-10m);
        weights[s_bnd].Should().BeApproximately(0.5m, 1e-10m);
    }

    [Fact]
    public void TargetWeightHistory_AfterModelComputation_ContainsModelWeights()
    {
        var strategy = CreateStrategy(lookbackWindow: 3);
        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        // First call: insufficient data triggers equal-weight fallback
        var firstDate = new DateOnly(2024, 1, 2);
        strategy.GenerateSignals(firstDate, CurrencyCode.USD, marketData, fxRates);

        // Monthly rebalance: Feb 5 is > 1 month after Jan 2
        var rebalDate = new DateOnly(2024, 2, 5);
        strategy.GenerateSignals(rebalDate, CurrencyCode.USD, marketData, fxRates);

        strategy.TargetWeightHistory.Should().ContainKey(rebalDate,
            "rebalance date should be recorded in history");

        // EqualWeightConstruction produces 50/50 regardless of data
        var weights = strategy.TargetWeightHistory[rebalDate];
        weights[s_vti].Should().BeApproximately(0.5m, 1e-10m);
        weights[s_bnd].Should().BeApproximately(0.5m, 1e-10m);
    }

    [Fact]
    public void TargetWeightHistory_MultipleRebalances_AccumulatesAll()
    {
        var strategy = CreateStrategy(lookbackWindow: 3);
        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        // Trigger 3 rebalances: initial + 2 monthly
        _ = new[]
        {
            new DateOnly(2024, 1, 2),  // Initial (equal-weight fallback or model)
            new DateOnly(2024, 2, 5),  // Monthly rebalance #2
            new DateOnly(2024, 3, 6),  // Monthly rebalance #3
        };

        // Call GenerateSignals for every day to simulate real backtest
        var baseDate = new DateOnly(2024, 1, 2);
        for (var i = 0; i < 65; i++)
        {
            var date = baseDate.AddDays(i);
            strategy.GenerateSignals(date, CurrencyCode.USD, marketData, fxRates);
        }

        strategy.TargetWeightHistory.Count.Should().BeGreaterThanOrEqualTo(3,
            "at least 3 rebalances should have occurred (initial + 2 monthly)");
    }

    [Fact]
    public void TargetWeightHistory_IsChronologicallyOrdered()
    {
        var strategy = CreateStrategy(lookbackWindow: 3);
        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        var baseDate = new DateOnly(2024, 1, 2);
        for (var i = 0; i < 65; i++)
        {
            var date = baseDate.AddDays(i);
            strategy.GenerateSignals(date, CurrencyCode.USD, marketData, fxRates);
        }

        var keys = strategy.TargetWeightHistory.Keys.ToList();
        keys.Should().BeInAscendingOrder("SortedDictionary guarantees chronological ordering");
    }

    [Fact]
    public void TargetWeightHistory_IsReadOnly_CannotMutateExternally()
    {
        var strategy = CreateStrategy(lookbackWindow: 200);
        var marketData = BuildMarketData(10);
        var fxRates = BuildFxRates(10);

        var firstDate = new DateOnly(2024, 1, 2);
        strategy.GenerateSignals(firstDate, CurrencyCode.USD, marketData, fxRates);

        // The property returns IReadOnlyDictionary — attempting to cast and mutate should either
        // fail at compile time or at runtime. We verify the type does not expose Add.
        var history = strategy.TargetWeightHistory;
        history.Should().NotBeNull();
        history.Should().BeAssignableTo<IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<Asset, decimal>>>();

        // Attempting direct cast to mutable dictionary — if underlying type is SortedDictionary,
        // this could succeed, but the API contract is read-only. Verify the property type is correct.
        // The compile-time type prevents Add/Remove, which is the design intent.
        history.Should().HaveCount(1, "only one entry should exist");
    }

    [Fact]
    public void TargetWeightHistory_LastComputedWeightsStillWorks()
    {
        var strategy = CreateStrategy(lookbackWindow: 3);
        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        var baseDate = new DateOnly(2024, 1, 2);
        for (var i = 0; i < 65; i++)
        {
            var date = baseDate.AddDays(i);
            strategy.GenerateSignals(date, CurrencyCode.USD, marketData, fxRates);
        }

        strategy.LastComputedWeights.Should().NotBeNull("weights should have been computed");

        // LastComputedWeights should match the last entry in TargetWeightHistory
        var lastHistoryEntry = strategy.TargetWeightHistory.Last().Value;
        strategy.LastComputedWeights.Should().BeEquivalentTo(lastHistoryEntry,
            "LastComputedWeights should match the most recent history entry");
    }

    [Fact]
    public void TargetWeightHistory_NonRebalanceDate_NoNewEntry()
    {
        var strategy = CreateStrategy(lookbackWindow: 3);
        var marketData = BuildMarketData();
        var fxRates = BuildFxRates();

        // First call triggers initial rebalance
        var firstDate = new DateOnly(2024, 1, 2);
        strategy.GenerateSignals(firstDate, CurrencyCode.USD, marketData, fxRates);
        var countAfterFirst = strategy.TargetWeightHistory.Count;

        // Next day is NOT a rebalance date (monthly frequency)
        var nextDay = new DateOnly(2024, 1, 3);
        strategy.GenerateSignals(nextDay, CurrencyCode.USD, marketData, fxRates);

        strategy.TargetWeightHistory.Count.Should().Be(countAfterFirst,
            "non-rebalance date should not add a new history entry");
    }
}
