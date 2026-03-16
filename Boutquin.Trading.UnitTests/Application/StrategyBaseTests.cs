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
}
