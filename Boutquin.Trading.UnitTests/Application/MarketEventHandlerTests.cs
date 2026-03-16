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
/// Tests for A4: MarketEventHandler feeds GenerateSignals results to event processor.
/// </summary>
public sealed class MarketEventHandlerTests
{
    /// <summary>
    /// A4: Signals returned by GenerateSignals should be processed as events.
    /// </summary>
    [Fact]
    public async Task MarketEventHandler_GenerateSignals_FeedsToEventProcessor()
    {
        // Arrange
        var asset = new Asset("AAPL");
        var date = new DateOnly(2024, 1, 15);
        var marketData = new SortedDictionary<Asset, MarketData>
        {
            { asset, new MarketData(date, 100m, 105m, 99m, 104m, 104m, 1000, 0m, 1m) }
        };
        var fxRates = new SortedDictionary<CurrencyCode, decimal>();

        var marketEvent = new MarketEvent(date, marketData, fxRates);

        var signal = new SignalEvent(date, "TestStrategy", new Dictionary<Asset, SignalType>
        {
            { asset, SignalType.Overweight }
        });

        var mockPortfolio = new Mock<IPortfolio>();
        mockPortfolio.Setup(p => p.IsLive).Returns(false);
        mockPortfolio.Setup(p => p.GenerateSignals(marketEvent)).Returns([signal]);

        // Track calls to HandleEventAsync to verify signals are processed
        var processedEvents = new List<IFinancialEvent>();
        mockPortfolio.Setup(p => p.HandleEventAsync(It.IsAny<IFinancialEvent>()))
            .Callback<IFinancialEvent>(e => processedEvents.Add(e))
            .Returns(Task.CompletedTask);

        var handler = new MarketEventHandler();

        // Act
        await handler.HandleEventAsync(mockPortfolio.Object, marketEvent).ConfigureAwait(true);

        // Assert — the signal should have been fed back into HandleEventAsync
        processedEvents.Should().Contain(signal);
    }
}
