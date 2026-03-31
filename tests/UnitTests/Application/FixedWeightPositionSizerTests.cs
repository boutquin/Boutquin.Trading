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

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
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
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
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

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
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

    /// <summary>
    /// M10: Verifies that position sizing uses Math.Round(MidpointRounding.AwayFromZero) instead of truncation.
    /// With truncation, 1000 * 0.5 / 333 = 1.5015... would yield 1. With rounding, it yields 2.
    /// </summary>
    [Fact]
    public void FixedWeightPositionSizer_ComputePositionSizes_ShouldRoundAwayFromZero()
    {
        // Arrange — values chosen so desiredAssetValue / price = 1.5015... (rounds to 2, truncates to 1)
        var fixedAssetWeights = new Dictionary<Asset, decimal> { { new Asset("XYZ"), 0.5m } };
        var assetCurrencies = new Dictionary<Asset, CurrencyCode> { { new Asset("XYZ"), CurrencyCode.USD } };
        var baseCurrency = CurrencyCode.USD;
        var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, baseCurrency);
        var signalType = new Dictionary<Asset, SignalType> { { new Asset("XYZ"), SignalType.Rebalance } };
        var marketData = new MarketData(
            Timestamp: _initialTimestamp,
            Open: 333,
            High: 340,
            Low: 330,
            Close: 333,
            AdjustedClose: 333,
            Volume: 1000000,
            DividendPerShare: 0,
            SplitCoefficient: 1);

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { _initialTimestamp, new SortedDictionary<Asset, MarketData> { { new Asset("XYZ"), marketData } } }
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

        // Assert — 1000 * 0.5 / 333 = 1.5015... → Math.Round = 2 (not truncation = 1)
        positionSizes[new Asset("XYZ")].Should().Be(2);
    }

    /// <summary>
    /// With renormalization enabled, when only a subset of assets have signals,
    /// weights should be renormalized to sum to 1.0 among signaled assets.
    /// Reproduces the benchmark dynamic universe scenario: benchmark has VTI (55%), VEA (35%), IEMG (10%),
    /// but only VTI is available — should get 100% allocation, not 55%.
    /// </summary>
    [Fact]
    public void FixedWeightPositionSizer_Renormalize_PartialSignals_ShouldRenormalizeWeights()
    {
        // Arrange — 3 assets but only VTI has a signal (the others aren't incepted yet)
        var vti = new Asset("VTI");
        var vea = new Asset("VEA");
        var iemg = new Asset("IEMG");
        var weights = new Dictionary<Asset, decimal>
        {
            { vti, 0.55m },
            { vea, 0.35m },
            { iemg, 0.10m }
        };
        var assetCurrencies = new Dictionary<Asset, CurrencyCode>
        {
            { vti, CurrencyCode.USD },
            { vea, CurrencyCode.USD },
            { iemg, CurrencyCode.USD }
        };
        // Only VTI has a signal (VEA and IEMG not yet incepted)
        var signalType = new Dictionary<Asset, SignalType> { { vti, SignalType.Rebalance } };
        var positionSizer = new FixedWeightPositionSizer(weights, CurrencyCode.USD, renormalizeForSignaledAssets: true);

        var md = new MarketData(_initialTimestamp, 100, 110, 90, 100, 100, 1_000_000, 0, 1);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { _initialTimestamp, new SortedDictionary<Asset, MarketData> { { vti, md } } }
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
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>())).Returns(100_000m);

        // Act
        var positionSizes = positionSizer.ComputePositionSizes(
            _initialTimestamp, signalType, strategyMock.Object, historicalMarketData, historicalFxConversionRates);

        // Assert — VTI weight renormalized from 55% to 100%: 100_000 * 1.0 / 100 = 1000 shares
        positionSizes.Should().ContainKey(vti);
        positionSizes[vti].Should().Be(1000);
        positionSizes.Should().NotContainKey(vea, "VEA has no signal");
        positionSizes.Should().NotContainKey(iemg, "IEMG has no signal");
    }

    /// <summary>
    /// Without renormalization, assets without signals should be skipped but weights NOT renormalized.
    /// </summary>
    [Fact]
    public void FixedWeightPositionSizer_NoRenormalize_PartialSignals_ShouldUseOriginalWeights()
    {
        // Arrange
        var vti = new Asset("VTI");
        var vea = new Asset("VEA");
        var weights = new Dictionary<Asset, decimal>
        {
            { vti, 0.55m },
            { vea, 0.35m }
        };
        var assetCurrencies = new Dictionary<Asset, CurrencyCode>
        {
            { vti, CurrencyCode.USD },
            { vea, CurrencyCode.USD }
        };
        // Only VTI has a signal
        var signalType = new Dictionary<Asset, SignalType> { { vti, SignalType.Rebalance } };
        var positionSizer = new FixedWeightPositionSizer(weights, CurrencyCode.USD); // renormalize defaults to false

        var md = new MarketData(_initialTimestamp, 100, 110, 90, 100, 100, 1_000_000, 0, 1);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { _initialTimestamp, new SortedDictionary<Asset, MarketData> { { vti, md } } }
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
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>())).Returns(100_000m);

        // Act
        var positionSizes = positionSizer.ComputePositionSizes(
            _initialTimestamp, signalType, strategyMock.Object, historicalMarketData, historicalFxConversionRates);

        // Assert — VTI keeps original 55% weight: 100_000 * 0.55 / 100 = 550 shares
        positionSizes.Should().ContainKey(vti);
        positionSizes[vti].Should().Be(550);
        positionSizes.Should().NotContainKey(vea, "VEA has no signal");
    }
}
