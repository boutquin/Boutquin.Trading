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

public sealed class MaxSectorExposureRuleSellTests
{
    private static readonly Asset s_vti = new("VTI");
    private static Mock<IPortfolio> CreatePortfolioMock(
        SortedDictionary<DateOnly, decimal> equityCurve,
        SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>> historicalData,
        Dictionary<string, IStrategy> strategies)
    {
        var mock = new Mock<IPortfolio>();
        mock.Setup(p => p.EquityCurve).Returns(equityCurve);
        mock.Setup(p => p.HistoricalMarketData).Returns(historicalData);
        mock.Setup(p => p.Strategies)
            .Returns((IReadOnlyDictionary<string, IStrategy>)strategies);
        return mock;
    }

    private static Mock<IStrategy> CreateStrategyMock(Dictionary<Asset, int> positions)
    {
        var mock = new Mock<IStrategy>();
        mock.Setup(s => s.Positions)
            .Returns((IReadOnlyDictionary<Asset, int>)positions);
        return mock;
    }

    [Fact]
    public void Evaluate_SellReducesExposure_AllowsOrder()
    {
        // Portfolio = $100k. VTI: 450 shares at $100 = $45k = 45% equity exposure. Limit 40%.
        // Sell 100 VTI → post-trade: 350 * $100 = $35k = 35%. Should ALLOW.
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

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int> { [s_vti] = 450 });
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxSectorExposureRule(0.40m, assetClassMap);
        var order = new Order(
            Timestamp: new DateOnly(2026, 1, 1),
            StrategyName: "TestStrategy",
            Asset: s_vti,
            TradeAction: TradeAction.Sell,
            OrderType: OrderType.Market,
            Quantity: 100);

        rule.Evaluate(order, portfolio.Object).IsAllowed.Should().BeTrue(
            "selling VTI reduces equity exposure from 45% to 35%, below the 40% limit");
    }

    [Fact]
    public void Evaluate_SellDoesNotReduceEnough_RejectsOrder()
    {
        // Portfolio = $100k. VTI: 500 shares at $100 = $50k = 50% equity. Limit 40%.
        // Sell 50 VTI → post-trade: 450 * $100 = $45k = 45%. Still above 40%. Should REJECT.
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

        var strategyMock = CreateStrategyMock(new Dictionary<Asset, int> { [s_vti] = 500 });
        var strategies = new Dictionary<string, IStrategy> { ["TestStrategy"] = strategyMock.Object };
        var portfolio = CreatePortfolioMock(equityCurve, marketData, strategies);

        var rule = new MaxSectorExposureRule(0.40m, assetClassMap);
        var order = new Order(
            Timestamp: new DateOnly(2026, 1, 1),
            StrategyName: "TestStrategy",
            Asset: s_vti,
            TradeAction: TradeAction.Sell,
            OrderType: OrderType.Market,
            Quantity: 50);

        rule.Evaluate(order, portfolio.Object).IsAllowed.Should().BeFalse(
            "selling only 50 VTI still leaves equity exposure at 45%, above 40% limit");
    }
}
