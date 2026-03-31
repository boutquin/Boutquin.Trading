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

using Boutquin.Trading.Application.RiskManagement;
using Boutquin.Trading.Domain.ValueObjects;

using Moq;

public sealed class RiskManagementTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_tlt = new("TLT");
    private static readonly Asset s_hyg = new("HYG");

    private static Order CreateBuyOrder(Asset asset, int quantity = 100, decimal? price = null) =>
        new(
            Timestamp: new DateOnly(2026, 3, 16),
            StrategyName: "TestStrategy",
            Asset: asset,
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: quantity,
            PrimaryPrice: price);

    private static Order CreateSellOrder(Asset asset, int quantity = 100) =>
        new(
            Timestamp: new DateOnly(2026, 3, 16),
            StrategyName: "TestStrategy",
            Asset: asset,
            TradeAction: TradeAction.Sell,
            OrderType: OrderType.Market,
            Quantity: quantity);

    private static Mock<IPortfolio> CreatePortfolioMock(
        SortedDictionary<DateOnly, decimal>? equityCurve = null,
        SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>? historicalData = null,
        Dictionary<string, IStrategy>? strategies = null)
    {
        var mock = new Mock<IPortfolio>();
        mock.Setup(p => p.EquityCurve)
            .Returns(equityCurve ?? []);
        mock.Setup(p => p.HistoricalMarketData)
            .Returns(historicalData ?? []);
        mock.Setup(p => p.Strategies)
            .Returns((IReadOnlyDictionary<string, IStrategy>)(strategies ?? new Dictionary<string, IStrategy>()));
        return mock;
    }

    private static Mock<IStrategy> CreateStrategyMock(Dictionary<Asset, int>? positions = null)
    {
        var mock = new Mock<IStrategy>();
        mock.Setup(s => s.Positions)
            .Returns((IReadOnlyDictionary<Asset, int>)(positions ?? new Dictionary<Asset, int>()));
        return mock;
    }

    // ============================================================
    // MaxDrawdownRule Tests
    // ============================================================

    [Fact]
    public void MaxDrawdownRule_InvalidThreshold_ShouldThrow()
    {
        var act1 = () => new MaxDrawdownRule(0m);
        act1.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => new MaxDrawdownRule(1.5m);
        act2.Should().Throw<ArgumentOutOfRangeException>();

        var act3 = () => new MaxDrawdownRule(-0.1m);
        act3.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MaxDrawdownRule_ValidThreshold_ShouldNotThrow()
    {
        var act = () => new MaxDrawdownRule(0.20m);
        act.Should().NotThrow();

        var act2 = () => new MaxDrawdownRule(1.0m);
        act2.Should().NotThrow();
    }

    [Fact]
    public void MaxDrawdownRule_BelowLimit_ShouldAllow()
    {
        // Equity curve: 100 → 95 → 98 (5% drawdown, limit is 20%)
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100m,
            [new DateOnly(2026, 1, 2)] = 95m,
            [new DateOnly(2026, 1, 3)] = 98m,
        };

        var portfolio = CreatePortfolioMock(equityCurve: equityCurve);
        var rule = new MaxDrawdownRule(0.20m);
        var order = CreateBuyOrder(s_vti);

        var result = rule.Evaluate(order, portfolio.Object);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void MaxDrawdownRule_AboveLimit_ShouldReject()
    {
        // Equity curve: 100 → 70 (30% drawdown, limit is 20%)
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100m,
            [new DateOnly(2026, 1, 2)] = 70m,
        };

        var portfolio = CreatePortfolioMock(equityCurve: equityCurve);
        var rule = new MaxDrawdownRule(0.20m);
        var order = CreateBuyOrder(s_vti);

        var result = rule.Evaluate(order, portfolio.Object);

        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("drawdown");
    }

    [Fact]
    public void MaxDrawdownRule_InsufficientData_ShouldAllow()
    {
        // Only 1 data point — cannot compute drawdown
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100m,
        };

        var portfolio = CreatePortfolioMock(equityCurve: equityCurve);
        var rule = new MaxDrawdownRule(0.10m);
        var order = CreateBuyOrder(s_vti);

        rule.Evaluate(order, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void MaxDrawdownRule_ExactlyAtLimit_ShouldAllow()
    {
        // Equity curve: 100 → 80 (exactly 20% drawdown, limit is 20%)
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100m,
            [new DateOnly(2026, 1, 2)] = 80m,
        };

        var portfolio = CreatePortfolioMock(equityCurve: equityCurve);
        var rule = new MaxDrawdownRule(0.20m);
        var order = CreateBuyOrder(s_vti);

        rule.Evaluate(order, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    // ============================================================
    // MaxPositionSizeRule Tests
    // ============================================================

    [Fact]
    public void MaxPositionSizeRule_InvalidThreshold_ShouldThrow()
    {
        var act = () => new MaxPositionSizeRule(0m);
        act.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => new MaxPositionSizeRule(1.5m);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MaxPositionSizeRule_PositionWithinLimit_ShouldAllow()
    {
        // Portfolio value = 100,000. Buying 100 shares at $100 = $10,000 = 10%. Limit 25%.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
            },
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int>());
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxPositionSizeRule(0.25m);
        var order = CreateBuyOrder(s_vti, quantity: 100);

        rule.Evaluate(order, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void MaxPositionSizeRule_PositionExceedsLimit_ShouldReject()
    {
        // Portfolio value = 100,000. Buying 300 shares at $100 = $30,000 = 30%. Limit 25%.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
            },
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int>());
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxPositionSizeRule(0.25m);
        var order = CreateBuyOrder(s_vti, quantity: 300);

        var result = rule.Evaluate(order, portfolio.Object);
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("VTI");
    }

    [Fact]
    public void MaxPositionSizeRule_EmptyEquityCurve_ShouldAllow()
    {
        var portfolio = CreatePortfolioMock();
        var rule = new MaxPositionSizeRule(0.25m);
        var order = CreateBuyOrder(s_vti);

        rule.Evaluate(order, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void MaxPositionSizeRule_PositionSizeCapped_ExistingPosition()
    {
        // Existing 200 shares + buying 100 more = 300 shares at $100 = $30,000 = 30%. Limit 25%.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
            },
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int> { [s_vti] = 200 });
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxPositionSizeRule(0.25m);
        var order = CreateBuyOrder(s_vti, quantity: 100);

        rule.Evaluate(order, portfolio.Object).IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void MaxPositionSizeRule_SellReducesPosition_ShouldAllow()
    {
        // Existing 300 shares, selling 200 → 100 shares at $100 = $10,000 = 10%. Limit 25%.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
            },
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int> { [s_vti] = 300 });
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxPositionSizeRule(0.25m);
        var order = CreateSellOrder(s_vti, quantity: 200);

        rule.Evaluate(order, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    // ============================================================
    // MaxSectorExposureRule Tests
    // ============================================================

    [Fact]
    public void MaxSectorExposureRule_InvalidThreshold_ShouldThrow()
    {
        var map = new Dictionary<Asset, AssetClassCode>();
        var act = () => new MaxSectorExposureRule(0m, map);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MaxSectorExposureRule_NullMap_ShouldThrow()
    {
        var act = () => new MaxSectorExposureRule(0.40m, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MaxSectorExposureRule_WithinLimit_ShouldAllow()
    {
        // Portfolio = $100k. Equity exposure = $10k (VTI). Buying $5k more TLT (fixed income). Limit 40%.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
                [s_tlt] = new MarketData(new DateOnly(2026, 1, 1), 50m, 51m, 49m, 50m, 50m, 500_000, 0m),
            },
        };

        var assetClassMap = new Dictionary<Asset, AssetClassCode>
        {
            [s_vti] = AssetClassCode.Equities,
            [s_tlt] = AssetClassCode.FixedIncome,
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int> { [s_vti] = 100 });
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxSectorExposureRule(0.40m, assetClassMap);
        var order = CreateBuyOrder(s_tlt, quantity: 100);

        rule.Evaluate(order, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void MaxSectorExposureRule_ExceedsLimit_ShouldReject()
    {
        // Portfolio = $100k. Already 300 shares VTI at $100 = $30k equity. Buying 200 more = $50k = 50%. Limit 40%.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
            },
        };

        var assetClassMap = new Dictionary<Asset, AssetClassCode>
        {
            [s_vti] = AssetClassCode.Equities,
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int> { [s_vti] = 300 });
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxSectorExposureRule(0.40m, assetClassMap);
        var order = CreateBuyOrder(s_vti, quantity: 200);

        var result = rule.Evaluate(order, portfolio.Object);
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Equities");
    }

    /// <summary>
    /// Regression: Unknown asset class must now reject, not silently allow.
    /// Previously, unmapped assets silently bypassed sector limits.
    /// </summary>
    [Fact]
    public void MaxSectorExposureRule_UnknownAssetClass_ShouldReject()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var portfolio = CreatePortfolioMock(equityCurve: equityCurve);
        var assetClassMap = new Dictionary<Asset, AssetClassCode>(); // Empty — HYG not mapped

        var rule = new MaxSectorExposureRule(0.40m, assetClassMap);
        var order = CreateBuyOrder(s_hyg);

        rule.Evaluate(order, portfolio.Object).IsAllowed.Should().BeFalse(
            "unmapped assets must not silently bypass sector limits");
    }

    // ============================================================
    // RiskManager (Composite) Tests
    // ============================================================

    [Fact]
    public void RiskManager_AllRulesPass_ShouldAllow()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
            [new DateOnly(2026, 1, 2)] = 99_000m, // 1% drawdown
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 2)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 2), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
            },
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int>());
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var manager = new RiskManager(new IRiskRule[]
        {
            new MaxDrawdownRule(0.20m),
            new MaxPositionSizeRule(0.25m),
        });

        var order = CreateBuyOrder(s_vti, quantity: 100);
        manager.Evaluate(order, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void RiskManager_OneRuleFails_ShouldReject()
    {
        // Drawdown is 30%, exceeds 20% limit
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
            [new DateOnly(2026, 1, 2)] = 70_000m,
        };

        var portfolio = CreatePortfolioMock(equityCurve: equityCurve);

        var manager = new RiskManager(new IRiskRule[]
        {
            new MaxDrawdownRule(0.20m),
            new MaxPositionSizeRule(0.25m),
        });

        var order = CreateBuyOrder(s_vti, quantity: 10);
        var result = manager.Evaluate(order, portfolio.Object);
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("drawdown");
    }

    [Fact]
    public void RiskManager_NoRules_ShouldAllow()
    {
        var portfolio = CreatePortfolioMock();
        var manager = new RiskManager(Array.Empty<IRiskRule>());
        var order = CreateBuyOrder(s_vti);

        manager.Evaluate(order, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void RiskManager_NullRules_ShouldThrow()
    {
        var act = () => new RiskManager(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ============================================================
    // RiskEvaluation Tests
    // ============================================================

    [Fact]
    public void RiskEvaluation_Allowed_ShouldHaveNullReason()
    {
        RiskEvaluation.Allowed.IsAllowed.Should().BeTrue();
        RiskEvaluation.Allowed.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void RiskEvaluation_Rejected_ShouldHaveReason()
    {
        var result = RiskEvaluation.Rejected("too risky");
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Be("too risky");
    }

    // ============================================================
    // BatchRiskEvaluation Tests
    // ============================================================

    [Fact]
    public void BatchRiskEvaluation_Allowed_ShouldBeTrue()
    {
        BatchRiskEvaluation.Allowed.IsAllowed.Should().BeTrue();
        BatchRiskEvaluation.Allowed.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void BatchRiskEvaluation_Rejected_ShouldHaveReason()
    {
        var result = BatchRiskEvaluation.Rejected("batch too risky");
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Be("batch too risky");
    }

    // ============================================================
    // MaxPositionSizeRule.EvaluateBatch Tests
    // ============================================================

    [Fact]
    public void MaxPositionSizeBatch_ProjectedWithinLimit_ShouldAllow()
    {
        // Portfolio = $100k. Batch: sell 100 VTI, buy 100 TLT.
        // VTI: existing 200, projected 100 → $10k = 10%. TLT: existing 0, projected 100 → $5k = 5%.
        // Limit 25%. Both under.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
                [s_tlt] = new MarketData(new DateOnly(2026, 1, 1), 50m, 51m, 49m, 50m, 50m, 500_000, 0m),
            },
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int> { [s_vti] = 200 });
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxPositionSizeRule(0.25m);
        var orders = new List<Order>
        {
            CreateSellOrder(s_vti, quantity: 100),
            CreateBuyOrder(s_tlt, quantity: 100),
        };

        rule.EvaluateBatch(orders, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void MaxPositionSizeBatch_IndividuallyViolates_ButBatchOk_ShouldAllow()
    {
        // Portfolio = $100k. VTI existing = 300 shares at $100 = $30k = 30%.
        // Single order: buy 50 TLT at $50 → TLT = $2,500 = 2.5% → individually OK.
        // But selling 200 VTI reduces to 100 shares = $10k = 10%.
        // Per-order evaluation of the sell would see 300-200=100 shares → 10% → OK.
        // Test that batch handles net deltas for multiple orders on same asset.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
                [s_tlt] = new MarketData(new DateOnly(2026, 1, 1), 50m, 51m, 49m, 50m, 50m, 500_000, 0m),
            },
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int> { [s_vti] = 300 });
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxPositionSizeRule(0.25m);
        var orders = new List<Order>
        {
            CreateSellOrder(s_vti, quantity: 200),
            CreateBuyOrder(s_tlt, quantity: 50),
        };

        rule.EvaluateBatch(orders, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void MaxPositionSizeBatch_ProjectedExceedsLimit_ShouldReject()
    {
        // Portfolio = $100k. Batch: buy 300 VTI at $100 = $30k. Existing 0. Projected = 30%. Limit 25%.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
            },
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int>());
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxPositionSizeRule(0.25m);
        var orders = new List<Order>
        {
            CreateBuyOrder(s_vti, quantity: 300),
        };

        var result = rule.EvaluateBatch(orders, portfolio.Object);
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("VTI");
    }

    [Fact]
    public void MaxPositionSizeBatch_EmptyBatch_ShouldAllow()
    {
        var portfolio = CreatePortfolioMock();
        var rule = new MaxPositionSizeRule(0.25m);

        rule.EvaluateBatch(new List<Order>(), portfolio.Object).IsAllowed.Should().BeTrue();
    }

    // ============================================================
    // MaxSectorExposureRule.EvaluateBatch Tests
    // ============================================================

    [Fact]
    public void MaxSectorExposureBatch_AllEquityRebalance_ShouldAllow()
    {
        // The core bug scenario: all-equity portfolio with 50% sector limit.
        // Portfolio = $100k. 3 equity ETFs: VTI (300 shares), TLT mapped as equity too.
        // Batch: sell 100 VTI, buy 100 HYG (also equity).
        // Net equity exposure: (200 * $100) + (100 * $40) = $24,000 = 24%. Under 50%.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
                [s_hyg] = new MarketData(new DateOnly(2026, 1, 1), 40m, 41m, 39m, 40m, 40m, 500_000, 0m),
            },
        };

        var assetClassMap = new Dictionary<Asset, AssetClassCode>
        {
            [s_vti] = AssetClassCode.Equities,
            [s_hyg] = AssetClassCode.Equities,
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int> { [s_vti] = 300 });
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxSectorExposureRule(0.50m, assetClassMap);
        var orders = new List<Order>
        {
            CreateSellOrder(s_vti, quantity: 100),
            CreateBuyOrder(s_hyg, quantity: 100),
        };

        rule.EvaluateBatch(orders, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void MaxSectorExposureBatch_ProjectedExceedsLimit_ShouldReject()
    {
        // Portfolio = $100k. Buy 400 VTI at $100 = $40k equity = 40%. Limit 35%.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
            },
        };

        var assetClassMap = new Dictionary<Asset, AssetClassCode>
        {
            [s_vti] = AssetClassCode.Equities,
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int>());
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxSectorExposureRule(0.35m, assetClassMap);
        var orders = new List<Order>
        {
            CreateBuyOrder(s_vti, quantity: 400),
        };

        var result = rule.EvaluateBatch(orders, portfolio.Object);
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Equities");
    }

    [Fact]
    public void MaxSectorExposureBatch_UnmappedAsset_ShouldReject()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };
        var portfolio = CreatePortfolioMock(equityCurve: equityCurve);
        var assetClassMap = new Dictionary<Asset, AssetClassCode>
        {
            [s_vti] = AssetClassCode.Equities,
            // HYG deliberately unmapped
        };

        var rule = new MaxSectorExposureRule(0.50m, assetClassMap);
        var orders = new List<Order>
        {
            CreateBuyOrder(s_vti, quantity: 100),
            CreateBuyOrder(s_hyg, quantity: 50),
        };

        rule.EvaluateBatch(orders, portfolio.Object).IsAllowed.Should().BeFalse(
            "unmapped assets must not silently bypass sector limits in batch mode");
    }

    // ============================================================
    // RiskManager.EvaluateBatch Tests
    // ============================================================

    [Fact]
    public void RiskManagerBatch_AllRulesPass_ShouldAllow()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
            [new DateOnly(2026, 1, 2)] = 99_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 2)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 2), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
                [s_tlt] = new MarketData(new DateOnly(2026, 1, 2), 50m, 51m, 49m, 50m, 50m, 500_000, 0m),
            },
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int> { [s_vti] = 200 });
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var manager = new RiskManager(new IRiskRule[]
        {
            new MaxDrawdownRule(0.20m),
            new MaxPositionSizeRule(0.25m),
        });

        var orders = new List<Order>
        {
            CreateSellOrder(s_vti, quantity: 100),
            CreateBuyOrder(s_tlt, quantity: 100),
        };

        manager.EvaluateBatch(orders, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void RiskManagerBatch_DrawdownRuleRejects_ShouldReject()
    {
        // 30% drawdown exceeds 20% limit — batch still rejected because
        // MaxDrawdownRule's default EvaluateBatch falls back to per-order.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
            [new DateOnly(2026, 1, 2)] = 70_000m,
        };

        var portfolio = CreatePortfolioMock(equityCurve: equityCurve);

        var manager = new RiskManager(new IRiskRule[]
        {
            new MaxDrawdownRule(0.20m),
            new MaxPositionSizeRule(0.25m),
        });

        var orders = new List<Order>
        {
            CreateBuyOrder(s_vti, quantity: 10),
        };

        var result = manager.EvaluateBatch(orders, portfolio.Object);
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("drawdown");
    }

    [Fact]
    public void RiskManagerBatch_EmptyOrders_ShouldAllow()
    {
        var portfolio = CreatePortfolioMock();
        var manager = new RiskManager(new IRiskRule[] { new MaxDrawdownRule(0.20m) });

        manager.EvaluateBatch(new List<Order>(), portfolio.Object).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void RiskManagerBatch_NoRules_ShouldAllow()
    {
        var portfolio = CreatePortfolioMock();
        var manager = new RiskManager(Array.Empty<IRiskRule>());
        var orders = new List<Order> { CreateBuyOrder(s_vti) };

        manager.EvaluateBatch(orders, portfolio.Object).IsAllowed.Should().BeTrue();
    }

    // ============================================================
    // Close vs AdjustedClose consistency tests
    // The engine values positions using AdjustedClose throughout
    // (equity curve, position sizer, strategy). The risk manager
    // must do the same — using raw Close causes spurious rejections
    // when dividend-adjusted data diverges from raw prices.
    // ============================================================

    [Fact]
    public void MaxPositionSizeRule_Evaluate_UsesAdjustedClose_NotClose()
    {
        // Simulate dividend-paying ETF where Close > AdjustedClose.
        // Close = 60, AdjustedClose = 50 (20% gap from cumulative dividends).
        // Portfolio = $100k. Buying 500 shares.
        // With AdjustedClose: 500 * 50 = $25,000 = 25% → within 30% limit.
        // With raw Close:     500 * 60 = $30,000 = 30% → ALSO within limit, but closer.
        // Use tighter scenario: 600 shares at limit 30%.
        // AdjustedClose: 600 * 50 = $30,000 = 30% → at limit → allowed.
        // raw Close:     600 * 60 = $36,000 = 36% → exceeds 35% → rejected.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                //                                                Close  AdjClose
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 60m, 61m, 59m, 60m, 50m, 1_000_000, 0m),
            },
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int>());
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxPositionSizeRule(0.35m);
        var order = CreateBuyOrder(s_vti, quantity: 600);

        // Position = 600 * AdjustedClose(50) / 100k = 30% → within 35% limit → ALLOW
        var result = rule.Evaluate(order, portfolio.Object);
        result.IsAllowed.Should().BeTrue(
            "risk manager should use AdjustedClose (50) not raw Close (60); " +
            "600 shares * $50 = $30,000 = 30% which is within the 35% limit");
    }

    [Fact]
    public void MaxPositionSizeBatch_UsesAdjustedClose_NotClose()
    {
        // Same scenario as above but via EvaluateBatch.
        // Close = 60, AdjustedClose = 50. Portfolio = $100k. Buying 600 shares.
        // AdjustedClose: 600 * 50 = $30k = 30% → within 35% limit → allowed.
        // raw Close:     600 * 60 = $36k = 36% → exceeds 35% → rejected.
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 60m, 61m, 59m, 60m, 50m, 1_000_000, 0m),
            },
        };

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int>());
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxPositionSizeRule(0.35m);
        var orders = new List<Order> { CreateBuyOrder(s_vti, quantity: 600) };

        var result = rule.EvaluateBatch(orders, portfolio.Object);
        result.IsAllowed.Should().BeTrue(
            "risk manager should use AdjustedClose (50) not raw Close (60); " +
            "600 shares * $50 = $30,000 = 30% which is within the 35% limit");
    }
}
