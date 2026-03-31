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

public sealed class MaxDrawdownRuleCurrentDDTests
{
    private static readonly Asset s_vti = new("VTI");

    private static Order CreateBuyOrder() =>
        new(
            Timestamp: new DateOnly(2026, 3, 16),
            StrategyName: "TestStrategy",
            Asset: s_vti,
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 100);

    private static Mock<IPortfolio> CreatePortfolioMock(SortedDictionary<DateOnly, decimal> equityCurve)
    {
        var mock = new Mock<IPortfolio>();
        mock.Setup(p => p.EquityCurve).Returns(equityCurve);
        return mock;
    }

    [Theory]
    [MemberData(nameof(MaxDrawdownRuleCurrentDDTestData.AfterRecoveryCases),
        MemberType = typeof(MaxDrawdownRuleCurrentDDTestData))]
    public void Evaluate_AfterRecovery_AllowsOrders(
        SortedDictionary<DateOnly, decimal> equityCurve, decimal maxDD, bool expectedAllowed)
    {
        var rule = new MaxDrawdownRule(maxDD);
        var portfolio = CreatePortfolioMock(equityCurve);

        var result = rule.Evaluate(CreateBuyOrder(), portfolio.Object);

        result.IsAllowed.Should().Be(expectedAllowed,
            "after full recovery the current drawdown is 0%, below the 15% limit");
    }

    [Theory]
    [MemberData(nameof(MaxDrawdownRuleCurrentDDTestData.InCurrentDrawdownCases),
        MemberType = typeof(MaxDrawdownRuleCurrentDDTestData))]
    public void Evaluate_InCurrentDrawdown_RejectsOrders(
        SortedDictionary<DateOnly, decimal> equityCurve, decimal maxDD, bool expectedAllowed)
    {
        var rule = new MaxDrawdownRule(maxDD);
        var portfolio = CreatePortfolioMock(equityCurve);

        var result = rule.Evaluate(CreateBuyOrder(), portfolio.Object);

        result.IsAllowed.Should().Be(expectedAllowed,
            "current drawdown of 20% exceeds the 15% limit");
    }

    [Theory]
    [MemberData(nameof(MaxDrawdownRuleCurrentDDTestData.WithinThresholdCases),
        MemberType = typeof(MaxDrawdownRuleCurrentDDTestData))]
    public void Evaluate_WithinThreshold_AllowsOrders(
        SortedDictionary<DateOnly, decimal> equityCurve, decimal maxDD, bool expectedAllowed)
    {
        var rule = new MaxDrawdownRule(maxDD);
        var portfolio = CreatePortfolioMock(equityCurve);

        var result = rule.Evaluate(CreateBuyOrder(), portfolio.Object);

        result.IsAllowed.Should().Be(expectedAllowed,
            "current drawdown of 10% is within the 15% limit");
    }
}
