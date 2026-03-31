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

using Boutquin.Trading.Application.Helpers;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Tests for the minimumTradeValue parameter on TargetPortfolioDiffer.
/// </summary>
public sealed class MinimumTradeValueTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_bnd = new("BND");
    private static readonly Asset s_gld = new("GLD");

    /// <summary>
    /// Zero threshold = current behavior, no orders suppressed.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_ZeroThreshold_NoSuppression()
    {
        var targetWeights = new Dictionary<Asset, decimal> { [s_vti] = 0.61m, [s_bnd] = 0.39m };
        var currentPositions = new Dictionary<Asset, int> { [s_vti] = 600, [s_bnd] = 400 };
        var prices = new Dictionary<Asset, decimal> { [s_vti] = 100m, [s_bnd] = 100m };

        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(
            targetWeights, currentPositions, prices, 100_000m, minimumTradeValue: 0m);

        orders.Should().HaveCount(2); // Small buy for VTI, small sell for BND
    }

    /// <summary>
    /// Small trades below threshold are suppressed.
    /// VTI: target 610 shares, current 600 → delta=10, notional=$1000. With threshold $1500 → suppressed.
    /// BND: target 390, current 400 → delta=-10, notional=$1000. With threshold $1500 → suppressed.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_SmallTradesBelowThreshold_Suppressed()
    {
        var targetWeights = new Dictionary<Asset, decimal> { [s_vti] = 0.61m, [s_bnd] = 0.39m };
        var currentPositions = new Dictionary<Asset, int> { [s_vti] = 600, [s_bnd] = 400 };
        var prices = new Dictionary<Asset, decimal> { [s_vti] = 100m, [s_bnd] = 100m };

        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(
            targetWeights, currentPositions, prices, 100_000m, minimumTradeValue: 1500m);

        orders.Should().BeEmpty("both trades have notional $1000 which is below $1500 threshold");
    }

    /// <summary>
    /// Large trades above threshold are NOT suppressed.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_LargeTradesAboveThreshold_NotSuppressed()
    {
        // VTI: target 50% of 100k = 500 shares, current 0 → buy 500 shares @ $100 = $50,000
        var targetWeights = new Dictionary<Asset, decimal> { [s_vti] = 0.5m };
        var currentPositions = new Dictionary<Asset, int>();
        var prices = new Dictionary<Asset, decimal> { [s_vti] = 100m };

        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(
            targetWeights, currentPositions, prices, 100_000m, minimumTradeValue: 1000m);

        orders.Should().ContainSingle();
        orders[0].TradeAction.Should().Be(TradeAction.Buy);
        orders[0].Quantity.Should().Be(500);
    }

    /// <summary>
    /// Mixed: one trade above threshold, one below. Only the large one survives.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_MixedSizes_OnlyLargeSurvives()
    {
        // VTI: target 70% = 700, current 600 → buy 100 @ $100 = $10,000 (above)
        // BND: target 29% = 290, current 300 → sell 10 @ $100 = $1,000 (below $5000)
        // GLD: target 1% = 10, current 100 → sell 90 @ $100 = $9,000 (above)
        var targetWeights = new Dictionary<Asset, decimal> { [s_vti] = 0.70m, [s_bnd] = 0.29m, [s_gld] = 0.01m };
        var currentPositions = new Dictionary<Asset, int> { [s_vti] = 600, [s_bnd] = 300, [s_gld] = 100 };
        var prices = new Dictionary<Asset, decimal> { [s_vti] = 100m, [s_bnd] = 100m, [s_gld] = 100m };

        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(
            targetWeights, currentPositions, prices, 100_000m, minimumTradeValue: 5000m);

        orders.Should().HaveCount(2);
        orders.Should().Contain(o => o.Asset == s_vti && o.TradeAction == TradeAction.Buy);
        orders.Should().Contain(o => o.Asset == s_gld && o.TradeAction == TradeAction.Sell);
        orders.Should().NotContain(o => o.Asset == s_bnd);
    }

    /// <summary>
    /// Negative threshold throws ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_NegativeThreshold_Throws()
    {
        var targetWeights = new Dictionary<Asset, decimal> { [s_vti] = 1m };
        var currentPositions = new Dictionary<Asset, int>();
        var prices = new Dictionary<Asset, decimal> { [s_vti] = 100m };

        var act = () => TargetPortfolioDiffer.ComputeRebalanceOrders(
            targetWeights, currentPositions, prices, 100_000m, minimumTradeValue: -1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
