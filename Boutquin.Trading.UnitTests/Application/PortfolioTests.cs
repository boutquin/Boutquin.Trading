// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System.Collections.Immutable;
using Boutquin.Trading.Application;
using Boutquin.Trading.Domain.Data;
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Events;
using Boutquin.Trading.Domain.Interfaces;
using Moq;

namespace Boutquin.Trading.UnitTests.Application;

public sealed class PortfolioTests
{
    [Fact]
    public void Test_HandleDividendEventAsync_ShouldUpdateStrategyCashBalance()
    {
        // Arrange
        var mockStrategy = new Mock<IStrategy>();
        var mockCapitalAllocationStrategy = new Mock<ICapitalAllocationStrategy>();
        var mockBroker = new Mock<IBrokerage>();

        mockStrategy.Setup(s => s.Positions).Returns(new SortedDictionary<string, int> { { "asset1", 10 } });
        mockStrategy.Setup(s => s.Cash).Returns(new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 100.0m } });

        var strategies = new Dictionary<string, IStrategy> { { "strategy1", mockStrategy.Object } };
        var timestamp = DateOnly.FromDateTime(DateTime.Today);
        var marketData = new MarketData(
            Timestamp: timestamp,
            Open: 100,
            High: 200,
            Low: 50,
            Close: 150,
            AdjustedClose: 150,
            Volume: 1000000,
            DividendPerShare: 0,
            SplitCoefficient: 1);

        var historicalMarketData = new SortedDictionary<DateOnly, SortedDictionary<string, MarketData>>
        {
            { timestamp, new SortedDictionary<string, MarketData> { { "AAPL", marketData } } }
        };
        var historicalFxConversionRates = new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>
        {
            { timestamp, new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 1m } } }
        }; 
        var assetCurrencies = new Dictionary<string, CurrencyCode> { { "asset1", CurrencyCode.USD } };
        var baseCurrency = CurrencyCode.USD;

        var portfolio = new Portfolio(
            strategies,
            mockCapitalAllocationStrategy.Object,
            mockBroker.Object,
            baseCurrency,
            assetCurrencies,
            historicalMarketData,
            historicalFxConversionRates);

        
        var dividendEvent = new DividendEvent(timestamp, "asset1", 1.0m);

        // Act
        portfolio.HandleEventAsync(dividendEvent).Wait();

        // Assert
        Assert.Equal(110.0m, mockStrategy.Object.Cash[CurrencyCode.USD]);
    }
}
