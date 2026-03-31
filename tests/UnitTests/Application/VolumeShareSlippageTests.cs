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

using Boutquin.Trading.Application.SlippageModels;

public sealed class VolumeShareSlippageTests
{
    private const decimal Precision = 1e-10m;

    // ============================================================
    // Constructor validation
    // ============================================================

    [Fact]
    public void Constructor_DefaultParameters_ShouldSetDefaults()
    {
        var sut = new VolumeShareSlippage();
        sut.VolumeLimit.Should().Be(0.025m);
        sut.PriceImpact.Should().Be(0.1m);
    }

    [Fact]
    public void Constructor_CustomParameters_ShouldSetValues()
    {
        var sut = new VolumeShareSlippage(volumeLimit: 0.05m, priceImpact: 0.2m);
        sut.VolumeLimit.Should().Be(0.05m);
        sut.PriceImpact.Should().Be(0.2m);
    }

    [Fact]
    public void Constructor_ZeroVolumeLimit_ShouldThrow()
    {
        var act = () => new VolumeShareSlippage(volumeLimit: 0m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativePriceImpact_ShouldThrow()
    {
        var act = () => new VolumeShareSlippage(priceImpact: -0.1m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ============================================================
    // Volume-aware overload
    // ============================================================

    [Fact]
    public void CalculateFillPrice_Buy_ShouldIncreasePrice()
    {
        var sut = new VolumeShareSlippage(volumeLimit: 0.025m, priceImpact: 0.1m);
        var fillPrice = sut.CalculateFillPrice(100m, 1000, TradeAction.Buy, 100_000);

        // Volume fraction = 1000/100000 = 0.01; impact = 0.1 * 0.01 = 0.001
        // Fill = 100 * (1 + 0.001) = 100.1
        fillPrice.Should().BeGreaterThan(100m);
        fillPrice.Should().BeApproximately(100.1m, 0.01m);
    }

    [Fact]
    public void CalculateFillPrice_Sell_ShouldDecreasePrice()
    {
        var sut = new VolumeShareSlippage(volumeLimit: 0.025m, priceImpact: 0.1m);
        var fillPrice = sut.CalculateFillPrice(100m, 1000, TradeAction.Sell, 100_000);

        fillPrice.Should().BeLessThan(100m);
        fillPrice.Should().BeApproximately(99.9m, 0.01m);
    }

    [Fact]
    public void CalculateFillPrice_LargeOrder_ShouldHaveMoreImpact()
    {
        var sut = new VolumeShareSlippage(volumeLimit: 0.025m, priceImpact: 0.1m);

        var smallFill = sut.CalculateFillPrice(100m, 100, TradeAction.Buy, 100_000);
        var largeFill = sut.CalculateFillPrice(100m, 2000, TradeAction.Buy, 100_000);

        // Larger order should have more price impact
        largeFill.Should().BeGreaterThan(smallFill);
    }

    [Fact]
    public void CalculateFillPrice_ZeroVolume_ShouldReturnTheoreticalPrice()
    {
        var sut = new VolumeShareSlippage(volumeLimit: 0.025m, priceImpact: 0.1m);
        var fillPrice = sut.CalculateFillPrice(100m, 0, TradeAction.Buy, 100_000);

        // Zero quantity = no impact
        fillPrice.Should().Be(100m);
    }

    // ============================================================
    // Non-volume overload (no volume info)
    // ============================================================

    [Fact]
    public void CalculateFillPrice_WithoutVolume_ShouldReturnTheoreticalPrice()
    {
        var sut = new VolumeShareSlippage();

        // Without volume info, assumes full price impact
        var fillPrice = sut.CalculateFillPrice(100m, 1000, TradeAction.Buy);
        // Full impact = priceImpact (0.1), so 100 * (1 + 0.1) = 110
        fillPrice.Should().Be(110m);
    }

    // ============================================================
    // Volume limit capping
    // ============================================================

    [Fact]
    public void CalculateFillPrice_ExceedingVolumeLimit_ShouldCapImpact()
    {
        var sut = new VolumeShareSlippage(volumeLimit: 0.01m, priceImpact: 0.5m);

        // Order = 5000 shares, volume = 100_000, limit = 0.01
        // Volume fraction = 5000/100000 = 0.05, but capped at 0.01
        // Impact = 0.5 * 0.01 = 0.005
        var fillPrice = sut.CalculateFillPrice(100m, 5000, TradeAction.Buy, 100_000);

        // Fill should reflect capped fraction, not actual fraction
        fillPrice.Should().BeApproximately(100.5m, 0.1m);

        // Compare with an order within the limit
        _ = sut.CalculateFillPrice(100m, 500, TradeAction.Buy, 100_000);

        // Both should be capped by volume limit — large order capped
        fillPrice.Should().BeLessThanOrEqualTo(100m * (1m + sut.PriceImpact * sut.VolumeLimit) + Precision);
    }
}
