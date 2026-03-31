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

using Boutquin.Trading.Application.PortfolioConstruction;

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

    /// <summary>
    /// Dynamic universe scenario: assets without signals (not yet incepted)
    /// should be skipped entirely — not included in position sizes.
    /// </summary>
    [Fact]
    public void DynamicWeightPositionSizer_ShouldSkipUnsignaledAssets()
    {
        // Arrange — 3 assets, but only VTI has a signal and market data
        var vti = new Asset("VTI");
        var vea = new Asset("VEA");
        var iemg = new Asset("IEMG");

        var assets = new Dictionary<Asset, CurrencyCode>
        {
            { vti, CurrencyCode.USD },
            { vea, CurrencyCode.USD },
            { iemg, CurrencyCode.USD }
        };

        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);

        var strategy = new ConstructionModelStrategy(
            "Test",
            assets,
            new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 100_000m } },
            new ClosePriceOrderPriceCalculationStrategy(),
            positionSizer,
            new EqualWeightConstruction(),
            RebalancingFrequency.Quarterly,
            lookbackWindow: 60);

        // Simulate that the construction model computed weights for only VTI
        // by using reflection to set LastComputedWeights
        var weightsField = typeof(ConstructionModelStrategy)
            .GetProperty(nameof(ConstructionModelStrategy.LastComputedWeights));
        weightsField!.SetValue(strategy, new Dictionary<Asset, decimal>
        {
            { vti, 1.0m }
        });

        // Only VTI is signaled (VEA and IEMG not yet incepted in dynamic universe)
        var signals = new Dictionary<Asset, SignalType>
        {
            { vti, SignalType.Rebalance }
        };

        var vtiMd = new MarketData(_timestamp, 200, 201, 199, 200, 200, 1_000_000, 0, 1);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { _timestamp, new SortedDictionary<Asset, MarketData> { { vti, vtiMd } } }
        };
        var fxRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _timestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var positionSizes = positionSizer.ComputePositionSizes(
            _timestamp, signals, strategy, historicalMarketData, fxRates);

        // Assert — only VTI should be present; VEA and IEMG should NOT appear
        positionSizes.Should().ContainKey(vti);
        positionSizes.Should().NotContainKey(vea);
        positionSizes.Should().NotContainKey(iemg);
        positionSizes[vti].Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Signaled assets with target weight 0 should still be included in position sizes
    /// (producing position = 0), not silently omitted.
    /// </summary>
    [Fact]
    public void DynamicWeightPositionSizer_ShouldSizeSignaledAssetWithZeroWeight()
    {
        // Arrange
        var vti = new Asset("VTI");
        var assets = new Dictionary<Asset, CurrencyCode>
        {
            { vti, CurrencyCode.USD }
        };

        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);

        var strategy = new ConstructionModelStrategy(
            "Test",
            assets,
            new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 100_000m } },
            new ClosePriceOrderPriceCalculationStrategy(),
            positionSizer,
            new EqualWeightConstruction(),
            RebalancingFrequency.Quarterly,
            lookbackWindow: 60);

        // Set LastComputedWeights with weight = 0
        var weightsField = typeof(ConstructionModelStrategy)
            .GetProperty(nameof(ConstructionModelStrategy.LastComputedWeights));
        weightsField!.SetValue(strategy, new Dictionary<Asset, decimal>
        {
            { vti, 0m }
        });

        var signals = new Dictionary<Asset, SignalType>
        {
            { vti, SignalType.Rebalance }
        };

        var vtiMd = new MarketData(_timestamp, 200, 201, 199, 200, 200, 1_000_000, 0, 1);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { _timestamp, new SortedDictionary<Asset, MarketData> { { vti, vtiMd } } }
        };
        var fxRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _timestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act
        var positionSizes = positionSizer.ComputePositionSizes(
            _timestamp, signals, strategy, historicalMarketData, fxRates);

        // Assert — VTI should be present with position size 0
        positionSizes.Should().ContainKey(vti);
        positionSizes[vti].Should().Be(0);
    }

    /// <summary>
    /// Regression: When a ConstructionModelStrategy has null LastComputedWeights
    /// (e.g. during warm-up before the construction model has enough data),
    /// DynamicWeightPositionSizer must throw InvalidOperationException instead of
    /// silently falling back to 1/N equal weight. The silent fallback caused entire
    /// backtests to run on equal weights without any warning, producing portfolios
    /// that couldn't beat their benchmarks.
    /// </summary>
    [Fact]
    public void DynamicWeightPositionSizer_ShouldThrow_WhenLastComputedWeightsIsNull()
    {
        // Arrange — ConstructionModelStrategy with no data means LastComputedWeights is null
        var asset1 = new Asset("VTI");
        var asset2 = new Asset("TLT");
        var assets = new Dictionary<Asset, CurrencyCode>
        {
            { asset1, CurrencyCode.USD },
            { asset2, CurrencyCode.USD }
        };

        var positionSizer = new DynamicWeightPositionSizer(CurrencyCode.USD);
        var strategy = new ConstructionModelStrategy(
            "Test",
            assets,
            new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 100_000m } },
            new ClosePriceOrderPriceCalculationStrategy(),
            positionSizer,
            new EqualWeightConstruction(),
            RebalancingFrequency.Quarterly,
            lookbackWindow: 60);

        // Verify precondition: no signals have been generated yet → weights are null
        strategy.LastComputedWeights.Should().BeNull("No signals generated yet");

        var signals = new Dictionary<Asset, SignalType>
        {
            { asset1, SignalType.Rebalance },
            { asset2, SignalType.Rebalance }
        };

        var md1 = new MarketData(_timestamp, 200, 201, 199, 200, 200, 1_000_000, 0, 1);
        var md2 = new MarketData(_timestamp, 100, 101, 99, 100, 100, 500_000, 0, 1);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { _timestamp, new SortedDictionary<Asset, MarketData> { { asset1, md1 }, { asset2, md2 } } }
        };
        var fxRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { _timestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act & Assert — should throw, not silently use 1/N
        var act = () => positionSizer.ComputePositionSizes(
            _timestamp, signals, strategy, historicalMarketData, fxRates);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*construction model*weight*");
    }
}
