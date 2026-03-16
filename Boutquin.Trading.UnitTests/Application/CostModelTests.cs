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

using Boutquin.Trading.Application.CostModels;

namespace Boutquin.Trading.Tests.UnitTests.Application;

public sealed class CostModelTests
{
    private const decimal Precision = 1e-12m;

    [Theory]
    [MemberData(nameof(CostModelTestData.PercentageOfValueData), MemberType = typeof(CostModelTestData))]
    public void PercentageOfValueCostModel_CalculateCommission_ReturnsCorrectResult(
        decimal rate, decimal fillPrice, int quantity, TradeAction tradeAction, decimal expected)
    {
        var model = new PercentageOfValueCostModel(rate);
        var result = model.CalculateCommission(fillPrice, quantity, tradeAction);
        result.Should().BeApproximately(expected, Precision);
    }

    [Theory]
    [MemberData(nameof(CostModelTestData.FixedPerTradeData), MemberType = typeof(CostModelTestData))]
    public void FixedPerTradeCostModel_CalculateCommission_ReturnsFixedAmount(
        decimal fixedAmount, decimal fillPrice, int quantity, TradeAction tradeAction, decimal expected)
    {
        var model = new FixedPerTradeCostModel(fixedAmount);
        var result = model.CalculateCommission(fillPrice, quantity, tradeAction);
        result.Should().BeApproximately(expected, Precision);
    }

    [Theory]
    [MemberData(nameof(CostModelTestData.PerShareData), MemberType = typeof(CostModelTestData))]
    public void PerShareCostModel_CalculateCommission_ReturnsQuantityTimesRate(
        decimal perShareRate, decimal fillPrice, int quantity, TradeAction tradeAction, decimal expected)
    {
        var model = new PerShareCostModel(perShareRate);
        var result = model.CalculateCommission(fillPrice, quantity, tradeAction);
        result.Should().BeApproximately(expected, Precision);
    }

    [Theory]
    [MemberData(nameof(CostModelTestData.TieredData), MemberType = typeof(CostModelTestData))]
    public void TieredCostModel_CalculateCommission_AppliesCorrectTier(
        IReadOnlyList<(decimal MaxTradeValue, decimal Rate)> tiers,
        decimal fillPrice, int quantity, TradeAction tradeAction, decimal expected)
    {
        var model = new TieredCostModel(tiers);
        var result = model.CalculateCommission(fillPrice, quantity, tradeAction);
        result.Should().BeApproximately(expected, Precision);
    }

    [Fact]
    public void CompositeCostModel_CalculateCommission_SumsComponents()
    {
        var models = new List<ITransactionCostModel>
        {
            new PercentageOfValueCostModel(0.001m),
            new FixedPerTradeCostModel(5.00m)
        };
        var composite = new CompositeCostModel(models);
        // fillPrice=100, quantity=100, Buy → percentage=100*100*0.001=10 + fixed=5 = 15
        var result = composite.CalculateCommission(100m, 100, TradeAction.Buy);
        result.Should().BeApproximately(15.00m, Precision);
    }

    [Fact]
    public void PercentageOfValueCostModel_NegativeRate_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new PercentageOfValueCostModel(-0.001m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FixedPerTradeCostModel_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new FixedPerTradeCostModel(-5m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PerShareCostModel_ZeroRate_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new PerShareCostModel(0m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TieredCostModel_EmptyTiers_ThrowsArgumentException()
    {
        var act = () => new TieredCostModel(new List<(decimal, decimal)>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CompositeCostModel_EmptyModels_ThrowsArgumentException()
    {
        var act = () => new CompositeCostModel(new List<ITransactionCostModel>());
        act.Should().Throw<ArgumentException>();
    }
}
