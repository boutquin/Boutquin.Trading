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
/// Tests for <see cref="DynamicWeightPositionSizer"/>.
/// </summary>
public sealed class DynamicWeightPositionSizerTests
{
    private readonly DateOnly _timestamp = new(2024, 1, 15);

    /// <summary>
    /// M10: Verifies that DynamicWeightPositionSizer uses Math.Round(MidpointRounding.AwayFromZero)
    /// instead of truncation. With truncation, 500 / 333 = 1.5015... yields 1. With rounding, it yields 2.
    /// </summary>
    [Fact]
    public void DynamicWeightPositionSizer_ShouldRoundAwayFromZero()
    {
        // Arrange
        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var asset = new Asset("XYZ");
        var signalType = new Dictionary<Asset, SignalType> { { asset, SignalType.Rebalance } };

        var marketData = new MarketData(
            Timestamp: _timestamp,
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
            { _timestamp, new SortedDictionary<Asset, MarketData> { { asset, marketData } } }
        };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _timestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Strategy with equal weight fallback — 1 asset gets 100% weight
        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Assets).Returns(new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } });
        strategyMock.Setup(s => s.ComputeTotalValue(
            It.IsAny<DateOnly>(),
            It.IsAny<CurrencyCode>(),
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>(),
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>())).Returns(500m);

        // Act — desiredValue = 500 * 1.0 = 500; 500 / 333 = 1.5015... → rounds to 2
        var positionSizes = positionSizer.ComputePositionSizes(
            _timestamp, signalType, strategyMock.Object, historicalMarketData, historicalFxConversionRates);

        // Assert — Math.Round(1.5015..., AwayFromZero) = 2, not truncation = 1
        positionSizes[asset].Should().Be(2);
    }
}
