// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

namespace Boutquin.Trading.UnitTests.Application;
using Boutquin.Trading.Application.PositionSizing;
using Boutquin.Trading.Domain.Data;
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Interfaces;

using Moq;

public sealed class FixedWeightPositionSizerTests
{
    private readonly DateOnly _initialTimestamp = new(year: 2023, month: 5, day: 1);

    [Fact]
    public void FixedWeightPositionSizer_ComputePositionSizes_ValidParameters_ShouldComputePositionSizes()
    {
        // Arrange
        var fixedAssetWeights = new Dictionary<string, decimal> { { "AAPL", 1m } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };
        var baseCurrency = CurrencyCode.USD;
        var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, baseCurrency);
        var signalType = new Dictionary<string, SignalType> { { "AAPL", SignalType.Rebalance } };
        var marketData = new MarketData(
            Timestamp: _initialTimestamp,
            Open: 100,
            High: 200,
            Low: 50,
            Close: 200,
            AdjustedClose: 200,
            Volume: 1000000,
            DividendPerShare: 0,
            SplitCoefficient: 1);

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<string, MarketData>?>
        {
            { _initialTimestamp, new SortedDictionary<string, MarketData> { { "AAPL", marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };
        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Assets).Returns(assetCurrencies);
        strategyMock.Setup(s => s.ComputeTotalValue(It.IsAny<DateOnly>(), It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<string, MarketData>>>(), It.IsAny<CurrencyCode>(), It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>())).Returns(1000m);

        // Act
        var positionSizes = positionSizer.ComputePositionSizes(_initialTimestamp, signalType, strategyMock.Object, historicalMarketData, historicalFxConversionRates);

        // Assert
        positionSizes.Should().NotBeNull();
        positionSizes.Should().ContainKey("AAPL");
        positionSizes["AAPL"].Should().Be(5);  // 1000 / 200 = 5
    }

    [Fact]
    public void FixedWeightPositionSizer_ComputePositionSizes_FixedAssetWeightNotFound_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var fixedAssetWeights = new Dictionary<string, decimal> { { "AAPL", 1m } };
        var baseCurrency = CurrencyCode.USD;
        var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, baseCurrency);
        var signalType = new Dictionary<string, SignalType> { { "MSFT", SignalType.Rebalance } };
        var marketData = new MarketData(
            Timestamp: _initialTimestamp,
            Open: 100,
            High: 200,
            Low: 50,
            Close: 200,
            AdjustedClose: 200,
            Volume: 1000000,
            DividendPerShare: 0,
            SplitCoefficient: 1);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<string, MarketData>?>
        {
            { _initialTimestamp, new SortedDictionary<string, MarketData> { { "MSFT", marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };
        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Assets).Returns(new Dictionary<string, CurrencyCode> { { "MSFT", CurrencyCode.USD } });

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => positionSizer.ComputePositionSizes(_initialTimestamp, signalType, strategyMock.Object, historicalMarketData, historicalFxConversionRates));
    }

    [Fact]
    public void FixedWeightPositionSizer_ComputePositionSizes_TwoAssets_ShouldComputePositionSizes()
    {
        // Arrange
        var fixedAssetWeights = new Dictionary<string, decimal> { { "AAPL", 0.6m }, { "MSFT", 0.4m } };
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD }, { "MSFT", CurrencyCode.USD } };
        var baseCurrency = CurrencyCode.USD;
        var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, baseCurrency);

        var signalType = new Dictionary<string, SignalType> { { "AAPL", SignalType.Rebalance }, { "MSFT", SignalType.Rebalance } };

        var marketDataAAPL = new MarketData(
            Timestamp: _initialTimestamp,
            Open: 100,
            High: 200,
            Low: 50,
            Close: 200,
            AdjustedClose: 200,
            Volume: 1000000,
            DividendPerShare: 0,
            SplitCoefficient: 1);

        var marketDataMSFT = new MarketData(
            Timestamp: _initialTimestamp,
            Open: 50,
            High: 100,
            Low: 25,
            Close: 100,
            AdjustedClose: 100,
            Volume: 1000000,
            DividendPerShare: 0,
            SplitCoefficient: 1);

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<string, MarketData>?>
        {
        { _initialTimestamp, new SortedDictionary<string, MarketData> { { "AAPL", marketDataAAPL }, { "MSFT", marketDataMSFT } } }
    };

        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
    {
        { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
    };

        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Assets).Returns(assetCurrencies);
        strategyMock.Setup(s => s.ComputeTotalValue(It.IsAny<DateOnly>(), It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<string, MarketData>>>(), It.IsAny<CurrencyCode>(), It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>())).Returns(1000m);

        // Act
        var positionSizes = positionSizer.ComputePositionSizes(_initialTimestamp, signalType, strategyMock.Object, historicalMarketData, historicalFxConversionRates);

        // Assert
        positionSizes.Should().NotBeNull();

        positionSizes.Should().ContainKey("AAPL");
        positionSizes["AAPL"].Should().Be(3);  // 1000 * 0.6 / 200 = 3

        positionSizes.Should().ContainKey("MSFT");
        positionSizes["MSFT"].Should().Be(4);  // 1000 * 0.4 / 100 = 4
    }
}
