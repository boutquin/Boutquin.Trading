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

public sealed class StrategyBaseTests
{
    private static IStrategy CreateStrategy(
        SortedDictionary<CurrencyCode, decimal>? cash = null,
        IReadOnlyDictionary<Asset, CurrencyCode>? assets = null)
    {
        var defaultAssets = assets ?? new Dictionary<Asset, CurrencyCode>
        {
            { new Asset("AAPL"), CurrencyCode.USD }
        };
        var defaultCash = cash ?? new SortedDictionary<CurrencyCode, decimal>
        {
            { CurrencyCode.USD, 10000m }
        };

        return new BuyAndHoldStrategy(
            "TestStrategy",
            defaultAssets,
            defaultCash,
            new DateOnly(2024, 1, 15),
            new Mock<IOrderPriceCalculationStrategy>().Object,
            new Mock<IPositionSizer>().Object);
    }

    [Fact]
    public void UpdateCash_AddsToExistingBalance()
    {
        var strategy = CreateStrategy();
        strategy.UpdateCash(CurrencyCode.USD, 500m);
        strategy.Cash[CurrencyCode.USD].Should().Be(10500m);
    }

    [Fact]
    public void UpdateCash_CreatesNewCurrencyEntry()
    {
        var strategy = CreateStrategy();
        strategy.UpdateCash(CurrencyCode.EUR, 1000m);
        strategy.Cash[CurrencyCode.EUR].Should().Be(1000m);
    }

    [Fact]
    public void UpdatePositions_IncrementsQuantity()
    {
        var asset = new Asset("AAPL");
        var strategy = CreateStrategy();
        strategy.UpdatePositions(asset, 50);
        strategy.UpdatePositions(asset, 25);
        strategy.GetPositionQuantity(asset).Should().Be(75);
    }

    [Fact]
    public void UpdatePositions_CreatesNewAssetEntry()
    {
        var asset = new Asset("MSFT");
        var strategy = CreateStrategy();
        strategy.UpdatePositions(asset, 100);
        strategy.GetPositionQuantity(asset).Should().Be(100);
    }

    [Fact]
    public void GetPositionQuantity_ReturnsZeroForUnknownAsset()
    {
        var strategy = CreateStrategy();
        strategy.GetPositionQuantity(new Asset("UNKNOWN")).Should().Be(0);
    }

    [Fact]
    public void SetPosition_SetsAbsoluteQuantity()
    {
        var asset = new Asset("AAPL");
        var strategy = CreateStrategy();
        strategy.UpdatePositions(asset, 50);
        strategy.SetPosition(asset, 200);
        strategy.GetPositionQuantity(asset).Should().Be(200);
    }

    [Fact]
    public void Positions_ExposedAsIReadOnlyDictionary()
    {
        var strategy = CreateStrategy();
        // The compile-time type of the Positions property is IReadOnlyDictionary
        IReadOnlyDictionary<Asset, int> positions = strategy.Positions;
        positions.Should().NotBeNull();
    }

    [Fact]
    public void Cash_ExposedAsIReadOnlyDictionary()
    {
        var strategy = CreateStrategy();
        // The compile-time type of the Cash property is IReadOnlyDictionary
        IReadOnlyDictionary<CurrencyCode, decimal> cash = strategy.Cash;
        cash.Should().NotBeNull();
        cash.Should().ContainKey(CurrencyCode.USD);
    }

    /// <summary>
    /// Dynamic universe: assets with position=0 and no market data should not throw.
    /// They contribute $0 to total value (not yet incepted).
    /// </summary>
    [Fact]
    public void ComputeTotalValue_ShouldNotThrow_WhenPositionZeroAndNoMarketData()
    {
        // Arrange — 2 assets, only AAPL has market data; MSFT has position 0 and no data
        var aapl = new Asset("AAPL");
        var msft = new Asset("MSFT");
        var assets = new Dictionary<Asset, CurrencyCode>
        {
            { aapl, CurrencyCode.USD },
            { msft, CurrencyCode.USD }
        };
        var strategy = CreateStrategy(assets: assets);
        strategy.UpdatePositions(aapl, 10);
        // MSFT is not in positions → defaults to 0

        var timestamp = new DateOnly(2024, 1, 15);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            {
                timestamp, new SortedDictionary<Asset, MarketData>
                {
                    { aapl, new MarketData(timestamp, 150, 155, 149, 152, 152, 5_000_000, 0, 1) }
                    // No MSFT market data
                }
            }
        };
        var fxRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { timestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act — should not throw; MSFT with position 0 and no data contributes $0
        var totalValue = strategy.ComputeTotalValue(
            timestamp, CurrencyCode.USD, historicalMarketData, fxRates);

        // Assert — only AAPL contributes: 10 shares × $152 = $1,520 + $10,000 cash
        totalValue.Should().Be(10 * 152m + 10_000m);
    }

    /// <summary>
    /// Assets with position > 0 but no market data must throw InvalidOperationException.
    /// We cannot value a held position without price data.
    /// </summary>
    [Fact]
    public void ComputeTotalValue_ShouldThrow_WhenPositionNonZeroAndNoMarketData()
    {
        // Arrange — 2 assets, only AAPL has market data; MSFT has position > 0 but no data
        var aapl = new Asset("AAPL");
        var msft = new Asset("MSFT");
        var assets = new Dictionary<Asset, CurrencyCode>
        {
            { aapl, CurrencyCode.USD },
            { msft, CurrencyCode.USD }
        };
        var strategy = CreateStrategy(assets: assets);
        strategy.UpdatePositions(aapl, 10);
        strategy.UpdatePositions(msft, 5); // Non-zero position, no market data

        var timestamp = new DateOnly(2024, 1, 15);
        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            {
                timestamp, new SortedDictionary<Asset, MarketData>
                {
                    { aapl, new MarketData(timestamp, 150, 155, 149, 152, 152, 5_000_000, 0, 1) }
                    // No MSFT market data
                }
            }
        };
        var fxRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { timestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        };

        // Act & Assert — should throw because MSFT has a held position but no price
        var act = () => strategy.ComputeTotalValue(
            timestamp, CurrencyCode.USD, historicalMarketData, fxRates);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Market data not found*MSFT*");
    }

    /// <summary>
    /// H6: Verifies that StrategyBase makes a defensive copy of the cash dictionary,
    /// so external mutation does not affect strategy state.
    /// </summary>
    [Fact]
    public void Constructor_ShouldDefensivelyCopyCash()
    {
        // Arrange
        var originalCash = new SortedDictionary<CurrencyCode, decimal>
        {
            { CurrencyCode.USD, 10000m }
        };
        var strategy = CreateStrategy(cash: originalCash);

        // Act — mutate the original dictionary after construction
        originalCash[CurrencyCode.USD] = 99999m;
        originalCash[CurrencyCode.EUR] = 5000m;

        // Assert — strategy's cash should be unaffected
        strategy.Cash[CurrencyCode.USD].Should().Be(10000m);
        strategy.Cash.Should().NotContainKey(CurrencyCode.EUR);
    }
}
