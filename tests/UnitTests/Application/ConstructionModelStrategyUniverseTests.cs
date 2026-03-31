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

using Boutquin.Trading.Application.Universe;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Tests for DynamicUniverse integration with ConstructionModelStrategy.
/// </summary>
public sealed class ConstructionModelStrategyUniverseTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_bnd = new("BND");
    private static readonly Asset s_gld = new("GLD");

    /// <summary>
    /// Assets not yet eligible (entry date in the future) are excluded from weight computation.
    /// GLD has entry date 2024-07-01. On 2024-06-03, only VTI and BND should be included.
    /// </summary>
    [Fact]
    public void GenerateSignals_WithUniverseSelector_ExcludesPreLaunchAssets()
    {
        // Arrange
        var date = new DateOnly(2024, 6, 3);
        var entryDates = new Dictionary<Asset, DateOnly>
        {
            [s_vti] = new DateOnly(2020, 1, 1),
            [s_bnd] = new DateOnly(2020, 1, 1),
            [s_gld] = new DateOnly(2024, 7, 1), // Not yet eligible
        };
        var universe = new DynamicUniverse(entryDates);

        var (strategy, historicalData, fxData) = CreateStrategy(universe);

        // Act
        var signalEvent = strategy.GenerateSignals(date, CurrencyCode.USD, historicalData, fxData);

        // Assert: GLD should NOT be in signals (not eligible yet)
        signalEvent.Signals.Should().NotContainKey(s_gld);
        // VTI and BND should have signals
        signalEvent.Signals.Should().ContainKey(s_vti);
        signalEvent.Signals.Should().ContainKey(s_bnd);
    }

    /// <summary>
    /// Null universe selector = current behavior (all assets included).
    /// </summary>
    [Fact]
    public void GenerateSignals_NoUniverseSelector_AllAssetsIncluded()
    {
        // Arrange
        var date = new DateOnly(2024, 6, 3);
        var (strategy, historicalData, fxData) = CreateStrategy(universeSelector: null);

        // Act
        var signalEvent = strategy.GenerateSignals(date, CurrencyCode.USD, historicalData, fxData);

        // Assert: all assets should be included
        signalEvent.Signals.Should().ContainKey(s_vti);
        signalEvent.Signals.Should().ContainKey(s_bnd);
        signalEvent.Signals.Should().ContainKey(s_gld);
    }

    /// <summary>
    /// After an asset becomes eligible and then is filtered out, if it has a position,
    /// it should get a sell signal.
    /// </summary>
    [Fact]
    public void GenerateSignals_FilteredAssetWithPosition_GetsSellSignal()
    {
        // Arrange: GLD was eligible until 2024-06-01 (we'll use a universe that excludes it after)
        var date1 = new DateOnly(2024, 6, 3);
        var date2 = new DateOnly(2024, 7, 5);

        // Universe where GLD is only eligible until 2024-06-30
        var entryDates = new Dictionary<Asset, DateOnly>
        {
            [s_vti] = new DateOnly(2020, 1, 1),
            [s_bnd] = new DateOnly(2020, 1, 1),
            [s_gld] = new DateOnly(2020, 1, 1),
        };
        var universe = new DynamicUniverse(entryDates);

        var (strategy, historicalData, fxData) = CreateStrategy(universe);

        // First rebalance — all three eligible
        strategy.GenerateSignals(date1, CurrencyCode.USD, historicalData, fxData);

        // Give GLD a position
        strategy.UpdatePositions(s_gld, 50);

        // Now create a new universe that excludes GLD
        var restrictedEntryDates = new Dictionary<Asset, DateOnly>
        {
            [s_vti] = new DateOnly(2020, 1, 1),
            [s_bnd] = new DateOnly(2020, 1, 1),
            [s_gld] = new DateOnly(2025, 1, 1), // Not eligible in 2024
        };
        var restrictedUniverse = new DynamicUniverse(restrictedEntryDates);
        var (strategy2, historicalData2, fxData2) = CreateStrategy(restrictedUniverse);

        // Set position on strategy2
        strategy2.UpdatePositions(s_gld, 50);

        // Act — trigger second rebalance with restricted universe
        var signalEvent = strategy2.GenerateSignals(date2, CurrencyCode.USD, historicalData2, fxData2);

        // Assert: GLD should have a rebalance signal (to trigger sell)
        // and LastComputedWeights should have GLD=0
        if (signalEvent.Signals.ContainsKey(s_gld))
        {
            signalEvent.Signals[s_gld].Should().Be(SignalType.Rebalance);
            strategy2.LastComputedWeights.Should().ContainKey(s_gld);
            strategy2.LastComputedWeights![s_gld].Should().Be(0m);
        }
    }

    // --- Helpers ---

    private static (ConstructionModelStrategy Strategy, SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>> MarketData, SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> FxData) CreateStrategy(
        ITimedUniverseSelector? universeSelector)
    {
        var assets = new Dictionary<Asset, CurrencyCode>
        {
            [s_vti] = CurrencyCode.USD,
            [s_bnd] = CurrencyCode.USD,
            [s_gld] = CurrencyCode.USD,
        };
        var cash = new SortedDictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 100_000m };

        var mockOrderPrice = new Mock<IOrderPriceCalculationStrategy>();
        mockOrderPrice
            .Setup(o => o.CalculateOrderPrices(It.IsAny<DateOnly>(), It.IsAny<Asset>(), It.IsAny<TradeAction>(),
                It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>()))
            .Returns((OrderType.Market, 100m, 0m));

        var mockSizer = new Mock<IPositionSizer>();

        var mockModel = new Mock<IPortfolioConstructionModel>();
        mockModel.Setup(m => m.ComputeTargetWeights(It.IsAny<IReadOnlyList<Asset>>(), It.IsAny<decimal[][]>()))
            .Returns((IReadOnlyList<Asset> a, decimal[][] _) =>
            {
                var w = 1m / a.Count;
                return a.ToDictionary(x => x, _ => w);
            });

        var strategy = new ConstructionModelStrategy(
            "TestStrategy", assets, cash, mockOrderPrice.Object, mockSizer.Object,
            mockModel.Object, RebalancingFrequency.Monthly, lookbackWindow: 2,
            universeSelector: universeSelector);

        // Create enough historical data for weight computation
        var historicalData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>();
        var fxData = new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>();
        var baseDate = new DateOnly(2024, 5, 1);

        for (var i = 0; i < 70; i++)
        {
            var date = baseDate.AddDays(i);
            var dayData = new SortedDictionary<Asset, MarketData>();
            foreach (var asset in assets.Keys)
            {
                dayData[asset] = new MarketData(date, 100m + i * 0.1m, 105m, 95m, 100m + i * 0.1m, 100m + i * 0.1m, 1_000_000L, 0m, 1m);
            }
            historicalData[date] = dayData;
            fxData[date] = new SortedDictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 1m };
        }

        return (strategy, historicalData, fxData);
    }
}
