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

namespace Boutquin.Trading.Tests.UnitTests.Application.Caching;

/// <summary>
/// Tests for SimulatedBrokerage buffered market data path.
/// </summary>
public sealed class SimulatedBrokerageBufferedTests
{
    private static readonly Asset s_aapl = new("AAPL");
    private static readonly DateOnly s_day1 = new(2024, 1, 15);

    private static MarketData CreateMarketData(DateOnly date, decimal close = 150m) =>
        new(date, 100m, 200m, 50m, close, close, 1_000_000, 0m, 1m);

    /// <summary>
    /// When buffered data is set, SubmitOrderAsync uses the buffer instead of calling FetchMarketDataAsync.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_WithBufferedData_DoesNotCallFetcher()
    {
        // Arrange
        var fetcher = new Mock<IMarketDataFetcher>();
        var brokerage = new SimulatedBrokerage(fetcher.Object, new PercentageOfValueCostModel(0.001m));

        var bufferedData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [s_day1] = new() { [s_aapl] = CreateMarketData(s_day1) }
        };

        brokerage.SetBufferedMarketData(bufferedData);

        var order = new Order(
            Timestamp: s_day1,
            StrategyName: "Strategy1",
            Asset: s_aapl,
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 10);

        // Act
        var result = await brokerage.SubmitOrderAsync(order, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        fetcher.Verify(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// When buffered data is set but order date is not in buffer, returns false.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_WithBufferedData_MissingDate_ReturnsFalse()
    {
        // Arrange
        var fetcher = new Mock<IMarketDataFetcher>();
        var brokerage = new SimulatedBrokerage(fetcher.Object, new PercentageOfValueCostModel(0.001m));

        var bufferedData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [s_day1] = new() { [s_aapl] = CreateMarketData(s_day1) }
        };

        brokerage.SetBufferedMarketData(bufferedData);

        var order = new Order(
            Timestamp: new DateOnly(2024, 1, 16), // Not in buffer
            StrategyName: "Strategy1",
            Asset: s_aapl,
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 10);

        // Act
        var result = await brokerage.SubmitOrderAsync(order, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        fetcher.Verify(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Without buffered data, the original fetch path is used (backward compatible).
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_WithoutBufferedData_UsesFetcher()
    {
        // Arrange
        var fetcher = new Mock<IMarketDataFetcher>();
        var marketData = new SortedDictionary<Asset, MarketData> { [s_aapl] = CreateMarketData(s_day1) };
        fetcher.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(s_day1, marketData) }.ToAsyncEnumerable());

        var brokerage = new SimulatedBrokerage(fetcher.Object, new PercentageOfValueCostModel(0.001m));

        var order = new Order(
            Timestamp: s_day1,
            StrategyName: "Strategy1",
            Asset: s_aapl,
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 10);

        // Act
        var result = await brokerage.SubmitOrderAsync(order, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        fetcher.Verify(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// SetBufferedMarketData on IBrokerage default does nothing (no-op for non-simulated brokerages).
    /// </summary>
    [Fact]
    public void IBrokerage_SetBufferedMarketData_DefaultIsNoOp()
    {
        // Arrange
        var mockBrokerage = new Mock<IBrokerage>();
        // The default interface method should be callable without error
        var data = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>();

        // Act — should not throw
        mockBrokerage.Object.SetBufferedMarketData(data);
    }

    /// <summary>
    /// FillOccurred event fires correctly through the buffered path.
    /// </summary>
    [Fact]
    public async Task SubmitOrderAsync_WithBufferedData_FiresFillEvent()
    {
        // Arrange
        var fetcher = new Mock<IMarketDataFetcher>();
        var brokerage = new SimulatedBrokerage(fetcher.Object, new PercentageOfValueCostModel(0.001m));

        var bufferedData = new Dictionary<DateOnly, SortedDictionary<Asset, MarketData>>
        {
            [s_day1] = new() { [s_aapl] = CreateMarketData(s_day1) }
        };

        brokerage.SetBufferedMarketData(bufferedData);

        FillEvent? capturedFill = null;
        brokerage.FillOccurred += (_, fill) => { capturedFill = fill; return Task.CompletedTask; };

        var order = new Order(
            Timestamp: s_day1,
            StrategyName: "Strategy1",
            Asset: s_aapl,
            TradeAction: TradeAction.Buy,
            OrderType: OrderType.Market,
            Quantity: 10);

        // Act
        await brokerage.SubmitOrderAsync(order, CancellationToken.None);

        // Assert
        capturedFill.Should().NotBeNull();
        capturedFill!.Asset.Should().Be(s_aapl);
    }
}
