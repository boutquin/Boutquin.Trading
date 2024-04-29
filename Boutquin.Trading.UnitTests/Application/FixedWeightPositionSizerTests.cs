// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Represents a set of tests for the FixedWeightPositionSizer class.
/// </summary>
public sealed class FixedWeightPositionSizerTests
{
    private readonly DateOnly _initialTimestamp = new(year: 2023, month: 5, day: 1);

    /// <summary>
    /// Tests that the ComputePositionSizes method of the FixedWeightPositionSizer class computes position sizes correctly when given valid parameters.
    /// </summary>
    [Fact]
    public void FixedWeightPositionSizer_ComputePositionSizes_ValidParameters_ShouldComputePositionSizes()
    {
        // Arrange
        var fixedAssetWeights = new Dictionary<Asset, decimal> { { new Asset("AAPL"), 1m } };
        var assetCurrencies = new Dictionary<Asset, CurrencyCode> { { new Asset("AAPL"), CurrencyCode.USD } };
        var baseCurrency = CurrencyCode.USD;
        var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, baseCurrency);
        var signalType = new Dictionary<Asset, SignalType> { { new Asset("AAPL"), SignalType.Rebalance } };
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

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>?>
        {
            { _initialTimestamp, new SortedDictionary<Asset, MarketData> { { new Asset("AAPL"), marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };
        var strategyMock = new Mock<IStrategy>();
        _ = strategyMock.Setup(s => s.Assets).Returns(assetCurrencies);
        _ = strategyMock.Setup(s => s.ComputeTotalValue(
            It.IsAny<DateOnly>(),
            It.IsAny<CurrencyCode>(),
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>(),
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>())).Returns(1000m);

        // Act
        var positionSizes = positionSizer.ComputePositionSizes(_initialTimestamp, signalType, strategyMock.Object, historicalMarketData, historicalFxConversionRates);

        // Assert
        positionSizes.Should().NotBeNull();
        positionSizes.Should().ContainKey(new Asset("AAPL"));
        positionSizes[new Asset("AAPL")].Should().Be(5);  // 1000 / 200 = 5
    }

    /// <summary>
    /// Tests that the ComputePositionSizes method of the FixedWeightPositionSizer class throws an InvalidOperationException when the fixed asset weight is not found.
    /// </summary>
    [Fact]
    public void FixedWeightPositionSizer_ComputePositionSizes_FixedAssetWeightNotFound_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var fixedAssetWeights = new Dictionary<Asset, decimal> { { new Asset("AAPL"), 1m } };
        var baseCurrency = CurrencyCode.USD;
        var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, baseCurrency);
        var signalType = new Dictionary<Asset, SignalType> { { new Asset("MSFT"), SignalType.Rebalance } };
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
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>?>
        {
            { _initialTimestamp, new SortedDictionary<Asset, MarketData> { { new Asset("MSFT"), marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };
        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Assets).Returns(new Dictionary<Asset, CurrencyCode> { { new Asset("MSFT"), CurrencyCode.USD } });

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => positionSizer.ComputePositionSizes(_initialTimestamp, signalType, strategyMock.Object, historicalMarketData, historicalFxConversionRates));
    }

    /// <summary>
    /// Tests that the ComputePositionSizes method of the FixedWeightPositionSizer class computes position sizes correctly when there are two assets.
    /// </summary>
    [Fact]
    public void FixedWeightPositionSizer_ComputePositionSizes_TwoAssets_ShouldComputePositionSizes()
    {
        // Arrange
        var fixedAssetWeights = new Dictionary<Asset, decimal> { { new Asset("AAPL"), 0.6m }, { new Asset("MSFT"), 0.4m } };
        var assetCurrencies = new Dictionary<Asset, CurrencyCode> { { new Asset("AAPL"), CurrencyCode.USD }, { new Asset("MSFT"), CurrencyCode.USD } };
        var baseCurrency = CurrencyCode.USD;
        var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, baseCurrency);

        var signalType = new Dictionary<Asset, SignalType> { { new Asset("AAPL"), SignalType.Rebalance }, { new Asset("MSFT"), SignalType.Rebalance } };

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

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>?>
        {
            { _initialTimestamp, new SortedDictionary<Asset, MarketData> { { new Asset("AAPL"), marketDataAAPL }, { new Asset("MSFT"), marketDataMSFT } } }
        };

        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
            {
                { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
            };

        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Assets).Returns(assetCurrencies);
        strategyMock.Setup(s => s.ComputeTotalValue(
            It.IsAny<DateOnly>(),
            It.IsAny<CurrencyCode>(),
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>(),  
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>())).Returns(1000m);

        // Act
        var positionSizes = positionSizer.ComputePositionSizes(_initialTimestamp, signalType, strategyMock.Object, historicalMarketData, historicalFxConversionRates);

        // Assert
        positionSizes.Should().NotBeNull();

        positionSizes.Should().ContainKey(new Asset("AAPL"));
        positionSizes[new Asset("AAPL")].Should().Be(3);  // 1000 * 0.6 / 200 = 3

        positionSizes.Should().ContainKey(new Asset("MSFT"));
        positionSizes[new Asset("MSFT")].Should().Be(4);  // 1000 * 0.4 / 100 = 4
    }
}
