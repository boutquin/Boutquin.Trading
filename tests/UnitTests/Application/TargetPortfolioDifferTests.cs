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
/// Tests for TargetPortfolioDiffer — computes rebalance orders by diffing target weights
/// against current holdings. QSTrader-inspired target portfolio diffing.
/// </summary>
public sealed class TargetPortfolioDifferTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_bnd = new("BND");
    private static readonly Asset s_gld = new("GLD");

    /// <summary>
    /// Target matches current exactly → no orders needed.
    /// $100k portfolio, VTI at 60% weight, price=$100 → target=600, current=600 → empty.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_NoChangesNeeded_ReturnsEmptyList()
    {
        // Arrange
        var targetWeights = new Dictionary<Asset, decimal> { { s_vti, 0.6m }, { s_bnd, 0.4m } };
        var currentPositions = new Dictionary<Asset, int> { { s_vti, 600 }, { s_bnd, 400 } };
        var prices = new Dictionary<Asset, decimal> { { s_vti, 100m }, { s_bnd, 100m } };

        // Act
        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(targetWeights, currentPositions, prices, 100_000m);

        // Assert
        orders.Should().BeEmpty();
    }

    /// <summary>
    /// Target has asset not in current → Buy order for the full target quantity.
    /// $100k portfolio, VTI at 50% weight, price=$200 → target=250 shares, current=0 → Buy 250.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_NewPosition_ReturnsBuyOrder()
    {
        // Arrange
        var targetWeights = new Dictionary<Asset, decimal> { { s_vti, 0.5m } };
        var currentPositions = new Dictionary<Asset, int>();
        var prices = new Dictionary<Asset, decimal> { { s_vti, 200m } };

        // Act
        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(targetWeights, currentPositions, prices, 100_000m);

        // Assert — 100,000 * 0.5 / 200 = 250 shares
        orders.Should().HaveCount(1);
        orders[0].Asset.Should().Be(s_vti);
        orders[0].TradeAction.Should().Be(TradeAction.Buy);
        orders[0].Quantity.Should().Be(250);
    }

    /// <summary>
    /// Current has asset not in target → Sell order for the full current quantity.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_ExitPosition_ReturnsSellOrder()
    {
        // Arrange
        var targetWeights = new Dictionary<Asset, decimal>();
        var currentPositions = new Dictionary<Asset, int> { { s_vti, 500 } };
        var prices = new Dictionary<Asset, decimal> { { s_vti, 100m } };

        // Act
        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(targetWeights, currentPositions, prices, 100_000m);

        // Assert
        orders.Should().HaveCount(1);
        orders[0].Asset.Should().Be(s_vti);
        orders[0].TradeAction.Should().Be(TradeAction.Sell);
        orders[0].Quantity.Should().Be(500);
    }

    /// <summary>
    /// Target weight higher than current → Buy for the delta only.
    /// $100k, VTI at 60%, price=$100 → target=600, current=400 → Buy 200.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_IncreasePosition_ReturnsBuyDelta()
    {
        // Arrange
        var targetWeights = new Dictionary<Asset, decimal> { { s_vti, 0.6m } };
        var currentPositions = new Dictionary<Asset, int> { { s_vti, 400 } };
        var prices = new Dictionary<Asset, decimal> { { s_vti, 100m } };

        // Act
        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(targetWeights, currentPositions, prices, 100_000m);

        // Assert — target 600 - current 400 = Buy 200
        orders.Should().HaveCount(1);
        orders[0].TradeAction.Should().Be(TradeAction.Buy);
        orders[0].Quantity.Should().Be(200);
    }

    /// <summary>
    /// Target weight lower than current → Sell delta.
    /// $100k, VTI at 30%, price=$100 → target=300, current=500 → Sell 200.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_DecreasePosition_ReturnsSellDelta()
    {
        // Arrange
        var targetWeights = new Dictionary<Asset, decimal> { { s_vti, 0.3m } };
        var currentPositions = new Dictionary<Asset, int> { { s_vti, 500 } };
        var prices = new Dictionary<Asset, decimal> { { s_vti, 100m } };

        // Act
        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(targetWeights, currentPositions, prices, 100_000m);

        // Assert — target 300 - current 500 = Sell 200
        orders.Should().HaveCount(1);
        orders[0].TradeAction.Should().Be(TradeAction.Sell);
        orders[0].Quantity.Should().Be(200);
    }

    /// <summary>
    /// Mixed rebalance: sells appear before buys in returned list (frees cash first).
    /// VTI: increase (Buy), BND: decrease (Sell) → Sell BND first, then Buy VTI.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_MultiAsset_SellsBeforeBuys()
    {
        // Arrange — $100k, VTI target 70% (700 shares), BND target 30% (300 shares)
        var targetWeights = new Dictionary<Asset, decimal> { { s_vti, 0.7m }, { s_bnd, 0.3m } };
        var currentPositions = new Dictionary<Asset, int> { { s_vti, 500 }, { s_bnd, 500 } };
        var prices = new Dictionary<Asset, decimal> { { s_vti, 100m }, { s_bnd, 100m } };

        // Act
        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(targetWeights, currentPositions, prices, 100_000m);

        // Assert — BND Sell 200 first, then VTI Buy 200
        orders.Should().HaveCount(2);
        orders[0].TradeAction.Should().Be(TradeAction.Sell);
        orders[0].Asset.Should().Be(s_bnd);
        orders[0].Quantity.Should().Be(200);
        orders[1].TradeAction.Should().Be(TradeAction.Buy);
        orders[1].Asset.Should().Be(s_vti);
        orders[1].Quantity.Should().Be(200);
    }

    /// <summary>
    /// Empty target weights with 3 assets held → 3 sell orders (full liquidation).
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_FullLiquidation_EmptyTargetSellsAll()
    {
        // Arrange
        var targetWeights = new Dictionary<Asset, decimal>();
        var currentPositions = new Dictionary<Asset, int> { { s_vti, 100 }, { s_bnd, 200 }, { s_gld, 300 } };
        var prices = new Dictionary<Asset, decimal> { { s_vti, 100m }, { s_bnd, 100m }, { s_gld, 100m } };

        // Act
        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(targetWeights, currentPositions, prices, 100_000m);

        // Assert — 3 sell orders
        orders.Should().HaveCount(3);
        orders.Should().OnlyContain(o => o.TradeAction == TradeAction.Sell);
        orders.Select(o => o.Asset).Should().BeEquivalentTo(new[] { s_vti, s_bnd, s_gld });
    }

    /// <summary>
    /// totalPortfolioValue &lt;= 0 → ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_ZeroPortfolioValue_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var targetWeights = new Dictionary<Asset, decimal> { { s_vti, 1m } };
        var currentPositions = new Dictionary<Asset, int>();
        var prices = new Dictionary<Asset, decimal> { { s_vti, 100m } };

        // Act & Assert
        var act = () => TargetPortfolioDiffer.ComputeRebalanceOrders(targetWeights, currentPositions, prices, 0m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Target has asset with no price entry → InvalidOperationException.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_MissingPrice_ThrowsInvalidOperationException()
    {
        // Arrange
        var targetWeights = new Dictionary<Asset, decimal> { { s_vti, 0.5m }, { s_bnd, 0.5m } };
        var currentPositions = new Dictionary<Asset, int>();
        var prices = new Dictionary<Asset, decimal> { { s_vti, 100m } }; // BND price missing

        // Act & Assert
        var act = () => TargetPortfolioDiffer.ComputeRebalanceOrders(targetWeights, currentPositions, prices, 100_000m);
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Verify M10 rounding: $100k portfolio, 1 asset at 50% weight, price=$333
    /// → $50,000 / 333 = 150.15... → 150 shares (rounds to nearest, AwayFromZero).
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_RoundsAwayFromZero()
    {
        // Arrange
        var targetWeights = new Dictionary<Asset, decimal> { { s_vti, 0.5m } };
        var currentPositions = new Dictionary<Asset, int>();
        var prices = new Dictionary<Asset, decimal> { { s_vti, 333m } };

        // Act
        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(targetWeights, currentPositions, prices, 100_000m);

        // Assert — 100,000 * 0.5 / 333 = 150.15... → 150 shares
        orders.Should().HaveCount(1);
        orders[0].Quantity.Should().Be(150);
    }

    /// <summary>
    /// Verify TargetWeight and CurrentWeight are populated correctly on each order.
    /// $100k, VTI target 60%, current 400 shares at $100 = $40k = 40% of portfolio.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_SetsCorrectWeights()
    {
        // Arrange
        var targetWeights = new Dictionary<Asset, decimal> { { s_vti, 0.6m } };
        var currentPositions = new Dictionary<Asset, int> { { s_vti, 400 } };
        var prices = new Dictionary<Asset, decimal> { { s_vti, 100m } };

        // Act
        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(targetWeights, currentPositions, prices, 100_000m);

        // Assert
        orders.Should().HaveCount(1);
        orders[0].TargetWeight.Should().Be(0.6m);
        orders[0].CurrentWeight.Should().Be(0.4m); // 400 * 100 / 100,000
    }

    /// <summary>
    /// 3 ETFs pure rebalance: sum of (buy value - sell value) is within rounding tolerance of zero.
    /// $100k, target: VTI 50%, BND 30%, GLD 20%. Current: VTI 300, BND 400, GLD 200 at $100 each.
    /// Target: VTI 500, BND 300, GLD 200. Delta: VTI +200, BND -100, GLD 0.
    /// Buy value = 200*100 = 20,000. Sell value = 100*100 = 10,000.
    /// Net != 0 because current portfolio was $90k and target is based on $100k total.
    /// Use scenario where current == total: VTI 400, BND 300, GLD 300 = $100k.
    /// Target: 500, 300, 200. Delta: VTI +100 ($10k), GLD -100 ($10k). Net = 0.
    /// </summary>
    [Fact]
    public void ComputeRebalanceOrders_ThreeAssetRebalance_NetDeltaIsCorrect()
    {
        // Arrange — current positions sum to $100k at $100/share
        var targetWeights = new Dictionary<Asset, decimal> { { s_vti, 0.5m }, { s_bnd, 0.3m }, { s_gld, 0.2m } };
        var currentPositions = new Dictionary<Asset, int> { { s_vti, 400 }, { s_bnd, 300 }, { s_gld, 300 } };
        var prices = new Dictionary<Asset, decimal> { { s_vti, 100m }, { s_bnd, 100m }, { s_gld, 100m } };

        // Act
        var orders = TargetPortfolioDiffer.ComputeRebalanceOrders(targetWeights, currentPositions, prices, 100_000m);

        // Assert — net delta should be zero for a pure rebalance (no new cash)
        var netDelta = 0m;
        foreach (var order in orders)
        {
            var value = order.Quantity * prices[order.Asset];
            netDelta += order.TradeAction == TradeAction.Buy ? value : -value;
        }

        netDelta.Should().BeApproximately(0m, 1m); // within $1 rounding tolerance
    }
}
