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

/// <summary>
/// Tests for <see cref="SpreadSlippage"/> (R2I-15).
/// </summary>
public sealed class SpreadSlippageTests
{
    private const decimal Precision = 1e-12m;
    private static readonly Asset s_testAsset = new("AAPL");

    [Fact]
    public void CalculateFillPrice_Buy_IncreasesPrice()
    {
        var halfSpreads = new Dictionary<Asset, decimal>();
        var slippage = new SpreadSlippage(halfSpreads, defaultHalfSpread: 0.001m);

        var fillPrice = slippage.CalculateFillPrice(100m, 100, TradeAction.Buy);

        fillPrice.Should().BeApproximately(100.1m, Precision); // 100 * (1 + 0.001)
    }

    [Fact]
    public void CalculateFillPrice_Sell_DecreasesPrice()
    {
        var halfSpreads = new Dictionary<Asset, decimal>();
        var slippage = new SpreadSlippage(halfSpreads, defaultHalfSpread: 0.001m);

        var fillPrice = slippage.CalculateFillPrice(100m, 100, TradeAction.Sell);

        fillPrice.Should().BeApproximately(99.9m, Precision); // 100 * (1 - 0.001)
    }

    [Fact]
    public void CalculateFillPriceForAsset_UsesPerAssetSpread()
    {
        var halfSpreads = new Dictionary<Asset, decimal>
        {
            [s_testAsset] = 0.005m // 50 bps for AAPL
        };
        var slippage = new SpreadSlippage(halfSpreads, defaultHalfSpread: 0.001m);

        var fillPrice = slippage.CalculateFillPriceForAsset(100m, 100, TradeAction.Buy, s_testAsset);

        fillPrice.Should().BeApproximately(100.5m, Precision); // 100 * (1 + 0.005)
    }

    [Fact]
    public void CalculateFillPriceForAsset_UnknownAsset_UsesDefault()
    {
        var halfSpreads = new Dictionary<Asset, decimal>();
        var slippage = new SpreadSlippage(halfSpreads, defaultHalfSpread: 0.002m);

        var fillPrice = slippage.CalculateFillPriceForAsset(100m, 100, TradeAction.Buy, new Asset("UNKNOWN"));

        fillPrice.Should().BeApproximately(100.2m, Precision); // 100 * (1 + 0.002)
    }

    [Fact]
    public void CalculateFillPrice_BuySellAsymmetry()
    {
        var halfSpreads = new Dictionary<Asset, decimal>();
        var slippage = new SpreadSlippage(halfSpreads, defaultHalfSpread: 0.01m);

        var buyPrice = slippage.CalculateFillPrice(100m, 100, TradeAction.Buy);
        var sellPrice = slippage.CalculateFillPrice(100m, 100, TradeAction.Sell);

        buyPrice.Should().BeGreaterThan(100m);
        sellPrice.Should().BeLessThan(100m);
        (buyPrice - sellPrice).Should().BeApproximately(2m, Precision); // Full spread = 2 * half-spread * price
    }

    [Fact]
    public void Constructor_NullHalfSpreads_ThrowsArgumentNullException()
    {
        var act = () => new SpreadSlippage(null!, 0.001m);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ZeroDefaultHalfSpread_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new SpreadSlippage(new Dictionary<Asset, decimal>(), 0m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativeDefaultHalfSpread_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new SpreadSlippage(new Dictionary<Asset, decimal>(), -0.001m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculateFillPrice_LargeSpread_CalculatesCorrectly()
    {
        var halfSpreads = new Dictionary<Asset, decimal>();
        var slippage = new SpreadSlippage(halfSpreads, defaultHalfSpread: 0.10m); // 10% half-spread

        var buyPrice = slippage.CalculateFillPrice(100m, 100, TradeAction.Buy);
        buyPrice.Should().Be(110m); // 100 * 1.10
    }
}
