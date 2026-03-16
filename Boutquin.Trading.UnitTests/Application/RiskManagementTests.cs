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
        SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>?>? historicalData = null,
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

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>?>
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

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>?>
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

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>?>
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

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>?>
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

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>?>
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

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>?>
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

    [Fact]
    public void MaxSectorExposureRule_UnknownAssetClass_ShouldAllow()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m,
        };

        var portfolio = CreatePortfolioMock(equityCurve: equityCurve);
        var assetClassMap = new Dictionary<Asset, AssetClassCode>(); // Empty — HYG not mapped

        var rule = new MaxSectorExposureRule(0.40m, assetClassMap);
        var order = CreateBuyOrder(s_hyg);

        rule.Evaluate(order, portfolio.Object).IsAllowed.Should().BeTrue();
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

        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>?>
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
}
