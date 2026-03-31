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

/// <summary>
/// Tests for A2/A3: FillEventHandler correctly handles buy vs sell fills.
/// </summary>
public sealed class FillEventHandlerTests
{
    /// <summary>
    /// A3: After a sell fill, strategy cash should increase (credit), not decrease (deduct).
    /// </summary>
    [Fact]
    public async Task FillEventHandler_SellOrder_CreditsCash()
    {
        // Arrange
        var asset = new Asset("AAPL");
        var strategyName = "TestStrategy";
        var mockStrategy = new Mock<IStrategy>();
        var mockPortfolio = new Mock<IPortfolio>();

        mockPortfolio.Setup(p => p.GetStrategy(strategyName)).Returns(mockStrategy.Object);
        mockPortfolio.Setup(p => p.GetAssetCurrency(asset)).Returns(CurrencyCode.USD);

        // A2: FillEvent now includes TradeAction
        var sellFillEvent = new FillEvent(
            new DateOnly(2024, 1, 15),
            asset,
            strategyName,
            TradeAction.Sell,
            100m,   // FillPrice
            10,     // Quantity
            5m);    // Commission

        var handler = new FillEventHandler();

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, sellFillEvent, CancellationToken.None).ConfigureAwait(true);

        // Assert — sell should credit cash: tradeValue - commission
        // tradeValue = 100 * 10 = 1000, credit = 1000 - 5 = 995
        mockStrategy.Verify(
            s => s.UpdateCash(CurrencyCode.USD, 995m),
            Times.Once);
    }

    /// <summary>
    /// A3: After a buy fill, strategy cash should decrease (deduct).
    /// </summary>
    [Fact]
    public async Task FillEventHandler_BuyOrder_DeductsCash()
    {
        // Arrange
        var asset = new Asset("AAPL");
        var strategyName = "TestStrategy";
        var mockStrategy = new Mock<IStrategy>();
        var mockPortfolio = new Mock<IPortfolio>();

        mockPortfolio.Setup(p => p.GetStrategy(strategyName)).Returns(mockStrategy.Object);
        mockPortfolio.Setup(p => p.GetAssetCurrency(asset)).Returns(CurrencyCode.USD);
        mockStrategy.Setup(s => s.Cash).Returns(new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 2000m } });

        var buyFillEvent = new FillEvent(
            new DateOnly(2024, 1, 15),
            asset,
            strategyName,
            TradeAction.Buy,
            100m,
            10,
            5m);

        var handler = new FillEventHandler();

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, buyFillEvent, CancellationToken.None).ConfigureAwait(true);

        // Assert — buy should deduct cash: -(tradeValue + commission)
        // tradeValue = 100 * 10 = 1000, deduct = -(1000 + 5) = -1005
        mockStrategy.Verify(
            s => s.UpdateCash(CurrencyCode.USD, -1005m),
            Times.Once);
    }

    /// <summary>
    /// Regression: When the asset's currency is not in strategy.Cash (initialization bug),
    /// FillEventHandler must throw InvalidOperationException instead of silently using 0 cash
    /// which would reject the buy as "insufficient funds" — masking the real problem.
    /// </summary>
    [Fact]
    public async Task FillEventHandler_ShouldThrow_WhenAssetCurrencyNotInCashDict()
    {
        // Arrange — fill is for USD asset, but strategy only has EUR cash
        var asset = new Asset("AAPL");
        var strategyName = "TestStrategy";
        var mockStrategy = new Mock<IStrategy>();
        var mockPortfolio = new Mock<IPortfolio>();

        mockPortfolio.Setup(p => p.GetStrategy(strategyName)).Returns(mockStrategy.Object);
        mockPortfolio.Setup(p => p.GetAssetCurrency(asset)).Returns(CurrencyCode.USD);
        mockStrategy.Setup(s => s.Cash).Returns(
            new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.EUR, 5000m } }); // No USD!

        var buyFillEvent = new FillEvent(
            new DateOnly(2024, 1, 15),
            asset,
            strategyName,
            TradeAction.Buy,
            100m,
            10,
            5m);

        var handler = new FillEventHandler();

        // Act & Assert — should throw, not silently use 0 cash
        var act = () => handler.HandleEventAsync(
            mockPortfolio.Object, buyFillEvent, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*USD*cash*");
    }
}
