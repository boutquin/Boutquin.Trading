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
/// Represents a set of tests for the RebalancingBuyAndHoldStrategy class.
/// </summary>
public sealed class RebalancingBuyAndHoldStrategyTests
{
    private readonly string _name = "TestStrategy";
    private readonly IReadOnlyDictionary<Asset, CurrencyCode> _assets = new Dictionary<Asset, CurrencyCode> { { new Asset("AAPL"), CurrencyCode.USD } };
    private readonly SortedDictionary<CurrencyCode, decimal> _cash = new() { { CurrencyCode.USD, 10000m } };
    private readonly Mock<IOrderPriceCalculationStrategy> _orderPriceCalculationStrategyMock = new();
    private readonly Mock<IPositionSizer> _positionSizerMock = new();
    private readonly DateOnly _initialTimestamp = new(year: 2023, month: 5, day: 1);
    private readonly MarketData _marketData = new(
        Timestamp: new DateOnly(year: 2023, month: 5, day: 1),
        Open: 100,
        High: 200,
        Low: 50,
        Close: 150,
        AdjustedClose: 150,
        Volume: 1000000,
        DividendPerShare: 0,
        SplitCoefficient: 1);

    /// <summary>
    /// Tests that the RebalancingBuyAndHoldStrategy constructor creates an instance when given valid parameters.
    /// </summary>
    [Fact]
    public void RebalancingBuyAndHoldStrategy_Constructor_ShouldCreateInstance()
    {
        // Act
        var strategy = new RebalancingBuyAndHoldStrategy(_name, _assets, _cash, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object, RebalancingFrequency.Daily);

        // Assert
        strategy.Should().NotBeNull();
        strategy.Name.Should().Be(_name);
        strategy.Assets.Should().BeEquivalentTo(_assets);
        strategy.Cash.Should().BeEquivalentTo(_cash);
        strategy.OrderPriceCalculationStrategy.Should().Be(_orderPriceCalculationStrategyMock.Object);
        strategy.PositionSizer.Should().Be(_positionSizerMock.Object);
    }

    /// <summary>
    /// Tests that the GenerateSignals method of the RebalancingBuyAndHoldStrategy class generates rebalance signals on the rebalancing date.
    /// </summary>
    [Fact]
    public void RebalancingBuyAndHoldStrategy_GenerateSignals_RebalancingDate_ShouldGenerateRebalanceSignals()
    {
        // Arrange
        var strategy = new RebalancingBuyAndHoldStrategy(_name, _assets, _cash, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object, RebalancingFrequency.Daily);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { _initialTimestamp, new SortedDictionary<Asset, MarketData> { { new Asset("AAPL"), _marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var signalEvent = strategy.GenerateSignals(
            _initialTimestamp,
            CurrencyCode.USD,
            historicalMarketData,
            historicalFxConversionRates);

        // Assert
        signalEvent.Should().NotBeNull();
        signalEvent.Timestamp.Should().Be(_initialTimestamp);
        signalEvent.StrategyName.Should().Be(_name);
        signalEvent.Signals.Should().ContainKey(new Asset("AAPL"));
        signalEvent.Signals[new Asset("AAPL")].Should().Be(SignalType.Rebalance);
    }

    /// <summary>
    /// Tests that the GenerateSignals method of the RebalancingBuyAndHoldStrategy class does not generate rebalance signals on dates that are not the rebalancing date.
    /// </summary>
    [Fact]
    public void RebalancingBuyAndHoldStrategy_GenerateSignals_NotRebalancingDate_ShouldNotGenerateRebalanceSignals()
    {
        // Arrange
        var strategy = new RebalancingBuyAndHoldStrategy(_name, _assets, _cash, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object, RebalancingFrequency.Monthly);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { _initialTimestamp, new SortedDictionary<Asset, MarketData> { { new Asset("AAPL"), _marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        _ = strategy.GenerateSignals(
            _initialTimestamp,
            CurrencyCode.USD,
            historicalMarketData,
            historicalFxConversionRates);
        var signalEvent = strategy.GenerateSignals(
            _initialTimestamp.AddDays(15),
            CurrencyCode.USD,
            historicalMarketData,
            historicalFxConversionRates);

        // Assert
        signalEvent.Should().NotBeNull();
        signalEvent.Timestamp.Should().Be(_initialTimestamp.AddDays(15));
        signalEvent.StrategyName.Should().Be(_name);
        signalEvent.Signals.Should().BeEmpty();  // No rebalancing signals should be generated
    }

    /// <summary>
    /// Tests that the GenerateSignals method of the RebalancingBuyAndHoldStrategy class generates rebalance signals on the second rebalancing date.
    /// </summary>
    [Fact]
    public void RebalancingBuyAndHoldStrategy_GenerateSignals_SecondRebalancingDate_ShouldGenerateRebalanceSignals()
    {
        // Arrange
        var strategy = new RebalancingBuyAndHoldStrategy(_name, _assets, _cash, _orderPriceCalculationStrategyMock.Object, _positionSizerMock.Object, RebalancingFrequency.Monthly);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { _initialTimestamp, new SortedDictionary<Asset, MarketData> { { new Asset("AAPL"), _marketData } } },
            { _initialTimestamp.AddMonths(1), new SortedDictionary<Asset, MarketData> { { new Asset("AAPL"), _marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } },
            { _initialTimestamp.AddMonths(1), new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var firstSignalEvent = strategy.GenerateSignals(
            _initialTimestamp,
            CurrencyCode.USD,
            historicalMarketData,
            historicalFxConversionRates);
        var secondSignalEvent = strategy.GenerateSignals(
            _initialTimestamp.AddMonths(1),
            CurrencyCode.USD,
            historicalMarketData,
            historicalFxConversionRates);

        // Assert
        firstSignalEvent.Should().NotBeNull();
        firstSignalEvent.Timestamp.Should().Be(_initialTimestamp);
        firstSignalEvent.StrategyName.Should().Be(_name);
        firstSignalEvent.Signals.Should().ContainKey(new Asset("AAPL"));
        firstSignalEvent.Signals[new Asset("AAPL")].Should().Be(SignalType.Rebalance);

        secondSignalEvent.Should().NotBeNull();
        secondSignalEvent.Timestamp.Should().Be(_initialTimestamp.AddMonths(1));
        secondSignalEvent.StrategyName.Should().Be(_name);
        secondSignalEvent.Signals.Should().ContainKey(new Asset("AAPL"));
        secondSignalEvent.Signals[new Asset("AAPL")].Should().Be(SignalType.Rebalance);
    }

    /// <summary>
    /// With a universe selector, only eligible assets should receive rebalance signals.
    /// Reproduces the benchmark crash: VTI available from 2001, VEA from 2007 — before 2007,
    /// only VTI should get a signal.
    /// </summary>
    [Fact]
    public void RebalancingBuyAndHoldStrategy_DynamicUniverse_ShouldOnlySignalEligibleAssets()
    {
        // Arrange — two assets, VEA not yet available on the test date
        var vti = new Asset("VTI");
        var vea = new Asset("VEA");
        var assets = new Dictionary<Asset, CurrencyCode>
        {
            { vti, CurrencyCode.USD },
            { vea, CurrencyCode.USD }
        };
        var universeSelector = new Boutquin.Trading.Application.Universe.DynamicUniverse(
            new Dictionary<Asset, DateOnly>
            {
                { vti, new DateOnly(2001, 5, 22) },
                { vea, new DateOnly(2007, 4, 9) }
            });

        var strategy = new RebalancingBuyAndHoldStrategy(
            _name, assets, _cash,
            _orderPriceCalculationStrategyMock.Object,
            _positionSizerMock.Object,
            RebalancingFrequency.Quarterly,
            tradingCalendar: null,
            universeSelector: universeSelector);

        var testDate = new DateOnly(2003, 1, 2); // Before VEA inception
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { testDate, new SortedDictionary<Asset, MarketData> { { vti, _marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { testDate, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var signalEvent = strategy.GenerateSignals(testDate, CurrencyCode.USD, historicalMarketData, historicalFxConversionRates);

        // Assert — only VTI should have a signal, VEA should be excluded
        signalEvent.Signals.Should().ContainKey(vti);
        signalEvent.Signals.Should().NotContainKey(vea);
    }

    /// <summary>
    /// After an asset becomes eligible, it should appear in signals.
    /// </summary>
    [Fact]
    public void RebalancingBuyAndHoldStrategy_DynamicUniverse_AssetAppearsAfterInception()
    {
        // Arrange
        var vti = new Asset("VTI");
        var vea = new Asset("VEA");
        var assets = new Dictionary<Asset, CurrencyCode>
        {
            { vti, CurrencyCode.USD },
            { vea, CurrencyCode.USD }
        };
        var universeSelector = new Boutquin.Trading.Application.Universe.DynamicUniverse(
            new Dictionary<Asset, DateOnly>
            {
                { vti, new DateOnly(2001, 5, 22) },
                { vea, new DateOnly(2007, 4, 9) }
            });

        var strategy = new RebalancingBuyAndHoldStrategy(
            _name, assets, _cash,
            _orderPriceCalculationStrategyMock.Object,
            _positionSizerMock.Object,
            RebalancingFrequency.Quarterly,
            tradingCalendar: null,
            universeSelector: universeSelector);

        var testDate = new DateOnly(2008, 1, 2); // After both inceptions
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { testDate, new SortedDictionary<Asset, MarketData> { { vti, _marketData }, { vea, _marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { testDate, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var signalEvent = strategy.GenerateSignals(testDate, CurrencyCode.USD, historicalMarketData, historicalFxConversionRates);

        // Assert — both assets should have signals
        signalEvent.Signals.Should().ContainKey(vti);
        signalEvent.Signals.Should().ContainKey(vea);
    }

    /// <summary>
    /// L5: With RebalancingFrequency.Never, the strategy should only generate signals on the first call.
    /// All subsequent calls should return empty signals because next rebalancing date is DateOnly.MaxValue.
    /// </summary>
    [Fact]
    public void RebalancingBuyAndHoldStrategy_NeverFrequency_ShouldOnlyRebalanceOnce()
    {
        // Arrange
        var strategy = new RebalancingBuyAndHoldStrategy(
            _name, _assets, _cash,
            _orderPriceCalculationStrategyMock.Object,
            _positionSizerMock.Object,
            RebalancingFrequency.Never);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { _initialTimestamp, new SortedDictionary<Asset, MarketData> { { new Asset("AAPL"), _marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act — first call should rebalance
        var firstSignals = strategy.GenerateSignals(
            _initialTimestamp, CurrencyCode.USD, historicalMarketData, historicalFxConversionRates);

        // Second call — even far in the future — should not rebalance
        var futureDate = _initialTimestamp.AddYears(10);
        var secondSignals = strategy.GenerateSignals(
            futureDate, CurrencyCode.USD, historicalMarketData, historicalFxConversionRates);

        // Assert
        firstSignals.Signals.Should().NotBeEmpty("First call should generate rebalance signals");
        secondSignals.Signals.Should().BeEmpty("RebalancingFrequency.Never should prevent all subsequent rebalances");
    }
}
