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

using Boutquin.Trading.Application.SlippageModels;

namespace Boutquin.Trading.Tests.UnitTests.Application;

public sealed class SlippageModelTests
{
    private const decimal Precision = 1e-12m;

    [Fact]
    public void NoSlippage_CalculateFillPrice_ReturnsTheoreticalPrice()
    {
        var model = new NoSlippage();
        var result = model.CalculateFillPrice(100m, 100, TradeAction.Buy);
        result.Should().Be(100m);
    }

    [Fact]
    public void FixedSlippage_Buy_IncreasesPrice()
    {
        var model = new FixedSlippage(0.05m);
        var result = model.CalculateFillPrice(100m, 100, TradeAction.Buy);
        result.Should().BeApproximately(100.05m, Precision);
    }

    [Fact]
    public void FixedSlippage_Sell_DecreasesPrice()
    {
        var model = new FixedSlippage(0.05m);
        var result = model.CalculateFillPrice(100m, 100, TradeAction.Sell);
        result.Should().BeApproximately(99.95m, Precision);
    }

    [Fact]
    public void FixedSlippage_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new FixedSlippage(-0.01m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PercentageSlippage_Buy_IncreasesPrice()
    {
        var model = new PercentageSlippage(0.001m); // 0.1%
        var result = model.CalculateFillPrice(100m, 100, TradeAction.Buy);
        result.Should().BeApproximately(100.10m, Precision);
    }

    [Fact]
    public void PercentageSlippage_Sell_DecreasesPrice()
    {
        var model = new PercentageSlippage(0.001m); // 0.1%
        var result = model.CalculateFillPrice(100m, 100, TradeAction.Sell);
        result.Should().BeApproximately(99.90m, Precision);
    }

    [Fact]
    public void PercentageSlippage_ZeroPercentage_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new PercentageSlippage(0m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SpreadSlippage_UsesAssetSpecificSpread()
    {
        var asset = new Asset("AAPL");
        var halfSpreads = new Dictionary<Asset, decimal> { { asset, 0.0005m } };
        var model = new SpreadSlippage(halfSpreads, 0.001m);

        // Buy: 100 * (1 + 0.0005) = 100.05
        var result = model.CalculateFillPriceForAsset(100m, 100, TradeAction.Buy, asset);
        result.Should().BeApproximately(100.05m, Precision);
    }

    [Fact]
    public void SpreadSlippage_FallsBackToDefaultSpread()
    {
        var asset = new Asset("UNKNOWN");
        var halfSpreads = new Dictionary<Asset, decimal>();
        var model = new SpreadSlippage(halfSpreads, 0.001m);

        // Buy: 100 * (1 + 0.001) = 100.10
        var result = model.CalculateFillPriceForAsset(100m, 100, TradeAction.Buy, asset);
        result.Should().BeApproximately(100.10m, Precision);
    }

    [Fact]
    public void SpreadSlippage_BuyPriceHigherThanSellPrice()
    {
        var asset = new Asset("SPY");
        var halfSpreads = new Dictionary<Asset, decimal> { { asset, 0.0002m } };
        var model = new SpreadSlippage(halfSpreads, 0.001m);

        var buyPrice = model.CalculateFillPriceForAsset(100m, 100, TradeAction.Buy, asset);
        var sellPrice = model.CalculateFillPriceForAsset(100m, 100, TradeAction.Sell, asset);

        buyPrice.Should().BeGreaterThan(sellPrice);
    }

    [Fact]
    public void SpreadSlippage_NegativeDefaultHalfSpread_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new SpreadSlippage(new Dictionary<Asset, decimal>(), -0.001m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
