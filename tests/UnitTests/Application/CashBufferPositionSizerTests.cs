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
/// Tests for the cash buffer feature in FixedWeightPositionSizer and DynamicWeightPositionSizer.
/// QSTrader-inspired: reserves a configurable percentage of portfolio value to prevent over-allocation.
/// </summary>
public sealed class CashBufferPositionSizerTests
{
    private static readonly DateOnly s_timestamp = new(2024, 1, 2);
    private static readonly Asset s_aapl = new("AAPL");
    private static readonly Asset s_msft = new("MSFT");

    /// <summary>
    /// Default buffer=0 produces same result as before (backward compatibility).
    /// $1000 portfolio, 1 asset at 100% weight, price=$200 → 5 shares.
    /// </summary>
    [Fact]
    public void FixedWeight_WithZeroBuffer_UsesFullValue()
    {
        // Arrange
        var sizer = new FixedWeightPositionSizer(
            new Dictionary<Asset, decimal> { { s_aapl, 1m } },
            CurrencyCode.USD,
            cashBufferPercent: 0m);

        var (strategyMock, marketData, fxRates, signals) = BuildSingleAssetScenario(
            s_aapl, totalValue: 100_000m, price: 100m);

        // Act
        var sizes = sizer.ComputePositionSizes(s_timestamp, signals, strategyMock.Object, marketData, fxRates);

        // Assert — 100,000 / 100 = 1000 shares
        sizes[s_aapl].Should().Be(1000);
    }

    /// <summary>
    /// $100k portfolio, 1 asset at 100% weight, price=$100.
    /// Without buffer: 1000 shares. With 5% buffer: $95,000 / $100 = 950 shares.
    /// </summary>
    [Fact]
    public void FixedWeight_WithFivePercentBuffer_ReducesPositionSize()
    {
        // Arrange
        var sizer = new FixedWeightPositionSizer(
            new Dictionary<Asset, decimal> { { s_aapl, 1m } },
            CurrencyCode.USD,
            cashBufferPercent: 0.05m);

        var (strategyMock, marketData, fxRates, signals) = BuildSingleAssetScenario(
            s_aapl, totalValue: 100_000m, price: 100m);

        // Act
        var sizes = sizer.ComputePositionSizes(s_timestamp, signals, strategyMock.Object, marketData, fxRates);

        // Assert — 100,000 * 0.95 / 100 = 950 shares
        sizes[s_aapl].Should().Be(950);
    }

    /// <summary>
    /// 2 assets at 60%/40%, $100k, 5% buffer.
    /// AAPL: 100,000 * 0.95 * 0.60 / 200 = 285 shares
    /// MSFT: 100,000 * 0.95 * 0.40 / 100 = 380 shares
    /// Both are 5% smaller than without buffer (300 and 400 respectively).
    /// </summary>
    [Fact]
    public void FixedWeight_WithBuffer_MultiAsset_ScalesProportionally()
    {
        // Arrange
        var weights = new Dictionary<Asset, decimal> { { s_aapl, 0.6m }, { s_msft, 0.4m } };
        var sizer = new FixedWeightPositionSizer(weights, CurrencyCode.USD, cashBufferPercent: 0.05m);

        var assetCurrencies = new Dictionary<Asset, CurrencyCode>
        {
            { s_aapl, CurrencyCode.USD },
            { s_msft, CurrencyCode.USD }
        };

        var mdAapl = MakeMarketData(200m);
        var mdMsft = MakeMarketData(100m);

        var marketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { s_timestamp, new SortedDictionary<Asset, MarketData> { { s_aapl, mdAapl }, { s_msft, mdMsft } } }
        };
        var fxRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { s_timestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };
        var signals = new Dictionary<Asset, SignalType>
        {
            { s_aapl, SignalType.Rebalance },
            { s_msft, SignalType.Rebalance }
        };

        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Assets).Returns(assetCurrencies);
        strategyMock.Setup(s => s.ComputeTotalValue(
            It.IsAny<DateOnly>(),
            It.IsAny<CurrencyCode>(),
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>(),
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>()))
            .Returns(100_000m);

        // Act
        var sizes = sizer.ComputePositionSizes(s_timestamp, signals, strategyMock.Object, marketData, fxRates);

        // Assert — allocatable = 95,000
        // AAPL: 95,000 * 0.6 / 200 = 285
        sizes[s_aapl].Should().Be(285);
        // MSFT: 95,000 * 0.4 / 100 = 380
        sizes[s_msft].Should().Be(380);
    }

    /// <summary>
    /// Same as the FixedWeight 5% buffer test but using DynamicWeightPositionSizer
    /// with a non-CMS strategy (equal weight fallback → 100% for single asset).
    /// </summary>
    [Fact]
    public void DynamicWeight_WithFivePercentBuffer_ReducesPositionSize()
    {
        // Arrange
        var sizer = new DynamicWeightPositionSizer(CurrencyCode.USD, cashBufferPercent: 0.05m);

        var (strategyMock, marketData, fxRates, signals) = BuildSingleAssetScenario(
            s_aapl, totalValue: 100_000m, price: 100m);

        // Act
        var sizes = sizer.ComputePositionSizes(s_timestamp, signals, strategyMock.Object, marketData, fxRates);

        // Assert — equal weight fallback = 1.0 for single asset; 100,000 * 0.95 / 100 = 950
        sizes[s_aapl].Should().Be(950);
    }

    /// <summary>
    /// Negative buffer is invalid.
    /// </summary>
    [Fact]
    public void FixedWeight_NegativeBuffer_ThrowsArgumentOutOfRange()
    {
        // Act & Assert
        var act = () => new FixedWeightPositionSizer(
            new Dictionary<Asset, decimal> { { s_aapl, 1m } },
            CurrencyCode.USD,
            cashBufferPercent: -0.01m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Buffer of exactly 1.0 would allocate nothing — invalid.
    /// </summary>
    [Fact]
    public void FixedWeight_BufferAtOne_ThrowsArgumentOutOfRange()
    {
        // Act & Assert
        var act = () => new FixedWeightPositionSizer(
            new Dictionary<Asset, decimal> { { s_aapl, 1m } },
            CurrencyCode.USD,
            cashBufferPercent: 1.0m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Buffer of 99% is valid — allocates only 1% of portfolio.
    /// $100k * 0.01 / 100 = 10 shares.
    /// </summary>
    [Fact]
    public void FixedWeight_BufferAtNinetyNinePercent_AllocatesMinimal()
    {
        // Arrange
        var sizer = new FixedWeightPositionSizer(
            new Dictionary<Asset, decimal> { { s_aapl, 1m } },
            CurrencyCode.USD,
            cashBufferPercent: 0.99m);

        var (strategyMock, marketData, fxRates, signals) = BuildSingleAssetScenario(
            s_aapl, totalValue: 100_000m, price: 100m);

        // Act
        var sizes = sizer.ComputePositionSizes(s_timestamp, signals, strategyMock.Object, marketData, fxRates);

        // Assert — 100,000 * 0.01 / 100 = 10 shares
        sizes[s_aapl].Should().Be(10);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MarketData MakeMarketData(decimal price) =>
        new(
            Timestamp: s_timestamp,
            Open: price,
            High: price,
            Low: price,
            Close: price,
            AdjustedClose: price,
            Volume: 1_000_000,
            DividendPerShare: 0,
            SplitCoefficient: 1);

    private static (Mock<IStrategy> Strategy, Dictionary<DateOnly, SortedDictionary<Asset, MarketData>> MarketData, Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> FxRates, Dictionary<Asset, SignalType> Signals) BuildSingleAssetScenario(
        Asset asset, decimal totalValue, decimal price)
    {
        var assetCurrencies = new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } };
        var md = MakeMarketData(price);
        var marketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            { s_timestamp, new SortedDictionary<Asset, MarketData> { { asset, md } } }
        };
        var fxRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { s_timestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };
        var signals = new Dictionary<Asset, SignalType> { { asset, SignalType.Rebalance } };

        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Assets).Returns(assetCurrencies);
        strategyMock.Setup(s => s.ComputeTotalValue(
            It.IsAny<DateOnly>(),
            It.IsAny<CurrencyCode>(),
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>>(),
            It.IsAny<IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>>()))
            .Returns(totalValue);

        return (strategyMock, marketData, fxRates, signals);
    }
}
