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
using FluentAssertions;

/// <summary>
/// Tests for <see cref="MaxSectorExposureRule"/> edge cases around missing data.
/// </summary>
public sealed class MaxSectorExposureRuleTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_bnd = new("BND");
    private static readonly Asset s_xyz = new("XYZ"); // Not in asset class map

    /// <summary>
    /// Regression: Orders for assets not in the asset class map must be rejected,
    /// not silently allowed. Previously, missing assets silently bypassed sector limits.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldReject_WhenOrderAssetNotInAssetClassMap()
    {
        // Arrange — asset class map only has VTI, order is for XYZ (unmapped)
        var assetClassMap = new Dictionary<Asset, AssetClassCode>
        {
            [s_vti] = AssetClassCode.Equities
        };
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m
        };
        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m)
            }
        };
        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Positions)
            .Returns((IReadOnlyDictionary<Asset, int>)new Dictionary<Asset, int>());
        var strategies = new Dictionary<string, IStrategy> { ["Test"] = strategyMock.Object };
        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.EquityCurve).Returns(equityCurve);
        portfolio.Setup(p => p.HistoricalMarketData).Returns(marketData);
        portfolio.Setup(p => p.Strategies)
            .Returns((IReadOnlyDictionary<string, IStrategy>)strategies);

        var rule = new MaxSectorExposureRule(0.40m, assetClassMap);
        var order = new Order(
            Timestamp: new DateOnly(2026, 1, 1),
            StrategyName: "Test",
            Asset: s_xyz, // Not in map
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 100);

        // Act
        var result = rule.Evaluate(order, portfolio.Object);

        // Assert — should reject, not silently allow
        result.IsAllowed.Should().BeFalse("unmapped assets must not silently bypass sector limits");
    }

    /// <summary>
    /// Empty equity curve at startup is allowed (no data to evaluate).
    /// </summary>
    [Fact]
    public void Evaluate_ShouldAllow_WhenNoEquityCurve()
    {
        var assetClassMap = new Dictionary<Asset, AssetClassCode> { [s_vti] = AssetClassCode.Equities };
        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());

        var rule = new MaxSectorExposureRule(0.40m, assetClassMap);
        var order = new Order(
            Timestamp: new DateOnly(2026, 1, 1),
            StrategyName: "Test",
            Asset: s_vti,
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 100);

        rule.Evaluate(order, portfolio.Object).IsAllowed.Should().BeTrue(
            "empty equity curve at startup means rule cannot evaluate — allow");
    }

    /// <summary>
    /// Regression: When equity curve exists but no market data is available,
    /// the rule must reject rather than silently allowing.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldReject_WhenNoLatestMarketData()
    {
        var assetClassMap = new Dictionary<Asset, AssetClassCode> { [s_vti] = AssetClassCode.Equities };
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m
        };
        // Historical market data exists but is empty (no entries)
        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>();

        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.EquityCurve).Returns(equityCurve);
        portfolio.Setup(p => p.HistoricalMarketData).Returns(marketData);

        var rule = new MaxSectorExposureRule(0.40m, assetClassMap);
        var order = new Order(
            Timestamp: new DateOnly(2026, 1, 1),
            StrategyName: "Test",
            Asset: s_vti,
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 100);

        var result = rule.Evaluate(order, portfolio.Object);
        result.IsAllowed.Should().BeFalse(
            "active portfolio with no market data should not silently allow orders");
    }

    /// <summary>
    /// Exposure exactly at the limit must be allowed — strict equality is not a breach.
    /// Regression: discrete share quantities can land exactly on the limit; rejecting
    /// at equality causes spurious rebalance failures.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldAllow_WhenExposureExactlyAtLimit()
    {
        // Arrange: 60% limit, portfolio $100k, VTI (Equities) at exactly 60%
        var assetClassMap = new Dictionary<Asset, AssetClassCode>
        {
            [s_vti] = AssetClassCode.Equities,
            [s_bnd] = AssetClassCode.FixedIncome
        };
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m
        };
        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                // 600 shares × $100 = $60,000 = exactly 60%
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 100m, 1_000_000, 0m),
                [s_bnd] = new MarketData(new DateOnly(2026, 1, 1), 80m, 81m, 79m, 80m, 80m, 500_000, 0m)
            }
        };
        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Positions)
            .Returns((IReadOnlyDictionary<Asset, int>)new Dictionary<Asset, int>
            {
                [s_vti] = 600,
                [s_bnd] = 500
            });
        var strategies = new Dictionary<string, IStrategy> { ["Test"] = strategyMock.Object };
        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.EquityCurve).Returns(equityCurve);
        portfolio.Setup(p => p.HistoricalMarketData).Returns(marketData);
        portfolio.Setup(p => p.Strategies)
            .Returns((IReadOnlyDictionary<string, IStrategy>)strategies);

        var rule = new MaxSectorExposureRule(0.60m, assetClassMap);
        // Order for VTI with 0 quantity change — evaluates Equities sector at exactly 60%
        var order = new Order(
            Timestamp: new DateOnly(2026, 1, 1),
            StrategyName: "Test",
            Asset: s_vti,
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 0);

        // Act
        var result = rule.Evaluate(order, portfolio.Object);

        // Assert — 60% == 60% limit should be allowed
        result.IsAllowed.Should().BeTrue(
            "exposure exactly at the limit is not a breach");
    }

    /// <summary>
    /// Exposure within tolerance (1bp) above the limit must be allowed.
    /// Discrete share quantities make exact targets impossible; rejecting at
    /// 60.005% against a 60% limit is a false positive.
    /// </summary>
    [Fact]
    public void EvaluateBatch_ShouldAllow_WhenExposureWithinTolerance()
    {
        // Arrange: 60% limit, portfolio $100k
        // Position: 600 shares VTI × $100 = $60,000 + tiny overshoot from BND rebalance
        // We simulate exposure at 60.009% (within 1bp tolerance)
        var assetClassMap = new Dictionary<Asset, AssetClassCode>
        {
            [s_vti] = AssetClassCode.Equities,
            [s_bnd] = AssetClassCode.FixedIncome
        };
        // Portfolio value set so that VTI exposure = 60.009%
        // 600 × $100.015 = $60,009 / $100,000 = 60.009%
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100_000m
        };
        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [new DateOnly(2026, 1, 1)] = new()
            {
                [s_vti] = new MarketData(new DateOnly(2026, 1, 1), 100.015m, 101m, 99m, 100.015m, 100.015m, 1_000_000, 0m),
                [s_bnd] = new MarketData(new DateOnly(2026, 1, 1), 80m, 81m, 79m, 80m, 80m, 500_000, 0m)
            }
        };
        var strategyMock = new Mock<IStrategy>();
        strategyMock.Setup(s => s.Positions)
            .Returns((IReadOnlyDictionary<Asset, int>)new Dictionary<Asset, int>
            {
                [s_vti] = 600,
                [s_bnd] = 500
            });
        var strategies = new Dictionary<string, IStrategy> { ["Test"] = strategyMock.Object };
        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.EquityCurve).Returns(equityCurve);
        portfolio.Setup(p => p.HistoricalMarketData).Returns(marketData);
        portfolio.Setup(p => p.Strategies)
            .Returns((IReadOnlyDictionary<string, IStrategy>)strategies);

        var rule = new MaxSectorExposureRule(0.60m, assetClassMap);

        // Batch: rebalance BND (no change to VTI exposure)
        var orders = new List<Order>
        {
            new(Timestamp: new DateOnly(2026, 1, 1), StrategyName: "Test",
                Asset: s_bnd, TradeAction: TradeAction.Buy, OrderType: OrderType.Market, Quantity: 0)
        };

        // Act
        var result = rule.EvaluateBatch(orders, portfolio.Object);

        // Assert — 60.009% is within 1bp tolerance of 60% limit
        result.IsAllowed.Should().BeTrue(
            "exposure within 1bp tolerance of the limit should not be rejected");
    }
}
