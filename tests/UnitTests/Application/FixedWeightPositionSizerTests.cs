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
        var fixedAssetWeights = new Dictionary<Symbol, decimal> { { new Symbol("AAPL"), 1m } };
        var assetCurrencies = new Dictionary<Symbol, CurrencyCode> { { new Symbol("AAPL"), CurrencyCode.USD } };
        var baseCurrency = CurrencyCode.USD;
        var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, baseCurrency);
        var signalType = new Dictionary<Symbol, SignalType> { { new Symbol("AAPL"), SignalType.Rebalance } };
        var marketData = new Bar(
            Date: _initialTimestamp,
            Open: 100,
            High: 200,
            Low: 50,
            Close: 200,
            AdjustedClose: 200,
            Volume: 1000000);

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Symbol, Bar>>
        {
            { _initialTimestamp, new SortedDictionary<Symbol, Bar> { { new Symbol("AAPL"), marketData } } }
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
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>>>(),
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>())).Returns(1000m);

        // Act
        var positionSizes = positionSizer.ComputePositionSizes(_initialTimestamp, signalType, strategyMock.Object, historicalMarketData, historicalFxConversionRates);

        // Assert
        positionSizes.Should().NotBeNull();
        positionSizes.Should().ContainKey(new Symbol("AAPL"));
        positionSizes[new Symbol("AAPL")].Should().Be(5);  // 1000 / 200 = 5
    }

    /// <summary>
    /// Tests that the ComputePositionSizes method of the FixedWeightPositionSizer class throws an InvalidOperationException when the fixed asset weight is not found.
    /// </summary>
    [Fact]
    public void FixedWeightPositionSizer_ComputePositionSizes_FixedAssetWeightNotFound_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var fixedAssetWeights = new Dictionary<Symbol, decimal> { { new Symbol("AAPL"), 1m } };
        var baseCurrency = CurrencyCode.USD;
        var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, baseCurrency);
        var signalType = new Dictionary<Symbol, SignalType> { { new Symbol("MSFT"), SignalType.Rebalance } };
        var marketData = new Bar(
            Date: _initialTimestamp,
            Open: 100,
            High: 200,
            Low: 50,
            Close: 200,
            AdjustedClose: 200,
            Volume: 1000000);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Symbol, Bar>>
        {
            { _initialTimestamp, new SortedDictionary<Symbol, Bar> { { new Symbol("MSFT"), marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _initialTimestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };
        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Assets).Returns(new Dictionary<Symbol, CurrencyCode> { { new Symbol("MSFT"), CurrencyCode.USD } });

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
        var fixedAssetWeights = new Dictionary<Symbol, decimal> { { new Symbol("AAPL"), 0.6m }, { new Symbol("MSFT"), 0.4m } };
        var assetCurrencies = new Dictionary<Symbol, CurrencyCode> { { new Symbol("AAPL"), CurrencyCode.USD }, { new Symbol("MSFT"), CurrencyCode.USD } };
        var baseCurrency = CurrencyCode.USD;
        var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, baseCurrency);

        var signalType = new Dictionary<Symbol, SignalType> { { new Symbol("AAPL"), SignalType.Rebalance }, { new Symbol("MSFT"), SignalType.Rebalance } };

        var marketDataAAPL = new Bar(
            Date: _initialTimestamp,
            Open: 100,
            High: 200,
            Low: 50,
            Close: 200,
            AdjustedClose: 200,
            Volume: 1000000);

        var marketDataMSFT = new Bar(
            Date: _initialTimestamp,
            Open: 50,
            High: 100,
            Low: 25,
            Close: 100,
            AdjustedClose: 100,
            Volume: 1000000);

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Symbol, Bar>>
        {
            { _initialTimestamp, new SortedDictionary<Symbol, Bar> { { new Symbol("AAPL"), marketDataAAPL }, { new Symbol("MSFT"), marketDataMSFT } } }
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
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>>>(),
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>())).Returns(1000m);

        // Act
        var positionSizes = positionSizer.ComputePositionSizes(_initialTimestamp, signalType, strategyMock.Object, historicalMarketData, historicalFxConversionRates);

        // Assert
        positionSizes.Should().NotBeNull();

        positionSizes.Should().ContainKey(new Symbol("AAPL"));
        positionSizes[new Symbol("AAPL")].Should().Be(3);  // 1000 * 0.6 / 200 = 3

        positionSizes.Should().ContainKey(new Symbol("MSFT"));
        positionSizes[new Symbol("MSFT")].Should().Be(4);  // 1000 * 0.4 / 100 = 4
    }

    /// <summary>
    /// M10: Verifies that position sizing uses Math.Round(MidpointRounding.AwayFromZero) instead of truncation.
    /// With truncation, 1000 * 0.5 / 333 = 1.5015... would yield 1. With rounding, it yields 2.
    /// </summary>
    [Fact]
    public void FixedWeightPositionSizer_ComputePositionSizes_ShouldRoundAwayFromZero()
    {
        // Arrange — values chosen so desiredAssetValue / price = 1.5015... (rounds to 2, truncates to 1)
        var fixedAssetWeights = new Dictionary<Symbol, decimal> { { new Symbol("XYZ"), 0.5m } };
        var assetCurrencies = new Dictionary<Symbol, CurrencyCode> { { new Symbol("XYZ"), CurrencyCode.USD } };
        var baseCurrency = CurrencyCode.USD;
        var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, baseCurrency);
        var signalType = new Dictionary<Symbol, SignalType> { { new Symbol("XYZ"), SignalType.Rebalance } };
        var marketData = new Bar(
            Date: _initialTimestamp,
            Open: 333,
            High: 340,
            Low: 330,
            Close: 333,
            AdjustedClose: 333,
            Volume: 1000000);

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Symbol, Bar>>
        {
            { _initialTimestamp, new SortedDictionary<Symbol, Bar> { { new Symbol("XYZ"), marketData } } }
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
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>>>(),
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>())).Returns(1000m);

        // Act
        var positionSizes = positionSizer.ComputePositionSizes(_initialTimestamp, signalType, strategyMock.Object, historicalMarketData, historicalFxConversionRates);

        // Assert — 1000 * 0.5 / 333 = 1.5015... → Math.Round = 2 (not truncation = 1)
        positionSizes[new Symbol("XYZ")].Should().Be(2);
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
        var vti = new Symbol("VTI");
        var vea = new Symbol("VEA");
        var iemg = new Symbol("IEMG");
        var weights = new Dictionary<Symbol, decimal>
        {
            { vti, 0.55m },
            { vea, 0.35m },
            { iemg, 0.10m }
        };
        var assetCurrencies = new Dictionary<Symbol, CurrencyCode>
        {
            { vti, CurrencyCode.USD },
            { vea, CurrencyCode.USD },
            { iemg, CurrencyCode.USD }
        };
        // Only VTI has a signal (VEA and IEMG not yet incepted)
        var signalType = new Dictionary<Symbol, SignalType> { { vti, SignalType.Rebalance } };
        var positionSizer = new FixedWeightPositionSizer(weights, CurrencyCode.USD, renormalizeForSignaledAssets: true);

        var md = new Bar(_initialTimestamp, 100, 110, 90, 100, 100, 1_000_000);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Symbol, Bar>>
        {
            { _initialTimestamp, new SortedDictionary<Symbol, Bar> { { vti, md } } }
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
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>>>(),
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
        var vti = new Symbol("VTI");
        var vea = new Symbol("VEA");
        var weights = new Dictionary<Symbol, decimal>
        {
            { vti, 0.55m },
            { vea, 0.35m }
        };
        var assetCurrencies = new Dictionary<Symbol, CurrencyCode>
        {
            { vti, CurrencyCode.USD },
            { vea, CurrencyCode.USD }
        };
        // Only VTI has a signal
        var signalType = new Dictionary<Symbol, SignalType> { { vti, SignalType.Rebalance } };
        var positionSizer = new FixedWeightPositionSizer(weights, CurrencyCode.USD); // renormalize defaults to false

        var md = new Bar(_initialTimestamp, 100, 110, 90, 100, 100, 1_000_000);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Symbol, Bar>>
        {
            { _initialTimestamp, new SortedDictionary<Symbol, Bar> { { vti, md } } }
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
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>>>(),
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
