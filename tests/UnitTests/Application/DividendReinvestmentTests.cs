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

using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Tests for dividend reinvestment (DRIP) functionality in Portfolio.
/// </summary>
public sealed class DividendReinvestmentTests
{
    private readonly Mock<IBrokerage> _mockBroker = new();
    private readonly Dictionary<Type, IEventHandler> _handlers = new()
    {
        { typeof(OrderEvent), new OrderEventHandler() },
        { typeof(MarketEvent), new MarketEventHandler() },
        { typeof(FillEvent), new FillEventHandler() },
        { typeof(SignalEvent), new SignalEventHandler() }
    };

    [Fact]
    public void UpdateCashForDividend_DripDisabled_ShouldOnlyCreditCash()
    {
        // Arrange
        var asset = new Asset("VTI");
        var testStrategy = new TestStrategy
        {
            Positions = new SortedDictionary<Asset, int> { { asset, 100 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 10_000m } }
        };
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", testStrategy } };
        var assetCurrencies = new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } };

        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            NullLogger<Portfolio>.Instance,
            isLive: true,
            enableDividendReinvestment: false
        );

        // Act — dividend of $0.50/share on 100 shares = $50
        portfolio.UpdateCashForDividend(asset, 0.50m, currentPrice: 200m);

        // Assert — cash credited, positions unchanged
        testStrategy.Cash[CurrencyCode.USD].Should().Be(10_050m);
        testStrategy.Positions[asset].Should().Be(100);
    }

    [Fact]
    public void UpdateCashForDividend_DripEnabled_ShouldReinvestWholeShares()
    {
        // Arrange
        var asset = new Asset("VTI");
        var testStrategy = new TestStrategy
        {
            Positions = new SortedDictionary<Asset, int> { { asset, 100 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 10_000m } }
        };
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", testStrategy } };
        var assetCurrencies = new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } };

        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            NullLogger<Portfolio>.Instance,
            isLive: true,
            enableDividendReinvestment: true
        );

        // Act — dividend of $2.00/share on 100 shares = $200 total
        // At $50/share, that buys 4 whole shares ($200), no fractional remainder
        portfolio.UpdateCashForDividend(asset, 2.00m, currentPrice: 50m);

        // Assert
        // Cash: 10000 + 200 (dividend) - 200 (reinvestment) = 10000
        testStrategy.Cash[CurrencyCode.USD].Should().Be(10_000m);
        // Position: 100 + 4 = 104
        testStrategy.Positions[asset].Should().Be(104);
    }

    [Fact]
    public void UpdateCashForDividend_DripEnabled_ShouldKeepFractionalCash()
    {
        // Arrange
        var asset = new Asset("VTI");
        var testStrategy = new TestStrategy
        {
            Positions = new SortedDictionary<Asset, int> { { asset, 100 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 10_000m } }
        };
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", testStrategy } };
        var assetCurrencies = new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } };

        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            NullLogger<Portfolio>.Instance,
            isLive: true,
            enableDividendReinvestment: true
        );

        // Act — dividend of $0.75/share on 100 shares = $75 total
        // At $50/share, that buys 1 whole share ($50), $25 remainder stays as cash
        portfolio.UpdateCashForDividend(asset, 0.75m, currentPrice: 50m);

        // Assert
        // Cash: 10000 + 75 (dividend) - 50 (1 share reinvestment) = 10025
        testStrategy.Cash[CurrencyCode.USD].Should().Be(10_025m);
        // Position: 100 + 1 = 101
        testStrategy.Positions[asset].Should().Be(101);
    }

    [Fact]
    public void UpdateCashForDividend_DripEnabled_DividendTooSmallForOneShare_ShouldOnlyCreditCash()
    {
        // Arrange
        var asset = new Asset("VTI");
        var testStrategy = new TestStrategy
        {
            Positions = new SortedDictionary<Asset, int> { { asset, 10 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 10_000m } }
        };
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", testStrategy } };
        var assetCurrencies = new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } };

        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            NullLogger<Portfolio>.Instance,
            isLive: true,
            enableDividendReinvestment: true
        );

        // Act — dividend of $0.10/share on 10 shares = $1 total
        // At $200/share, can't afford even 1 share
        portfolio.UpdateCashForDividend(asset, 0.10m, currentPrice: 200m);

        // Assert — cash credited but no reinvestment
        testStrategy.Cash[CurrencyCode.USD].Should().Be(10_001m);
        testStrategy.Positions[asset].Should().Be(10);
    }

    [Fact]
    public void UpdateCashForDividend_DripEnabled_MultipleStrategies_ShouldReinvestIndependently()
    {
        // Arrange
        var asset = new Asset("VTI");
        var strategy1 = new TestStrategy
        {
            Positions = new SortedDictionary<Asset, int> { { asset, 100 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 5_000m } }
        };
        var strategy2 = new TestStrategy
        {
            Positions = new SortedDictionary<Asset, int> { { asset, 50 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 3_000m } }
        };
        var strategies = new Dictionary<string, IStrategy>
        {
            { "Strategy1", strategy1 },
            { "Strategy2", strategy2 }
        };
        var assetCurrencies = new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } };

        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            NullLogger<Portfolio>.Instance,
            isLive: true,
            enableDividendReinvestment: true
        );

        // Act — dividend of $1.00/share at $25/share
        // Strategy1: 100 shares * $1 = $100 dividend → 4 shares reinvested ($100)
        // Strategy2: 50 shares * $1 = $50 dividend → 2 shares reinvested ($50)
        portfolio.UpdateCashForDividend(asset, 1.00m, currentPrice: 25m);

        // Assert Strategy1
        strategy1.Cash[CurrencyCode.USD].Should().Be(5_000m); // 5000 + 100 - 100
        strategy1.Positions[asset].Should().Be(104);

        // Assert Strategy2
        strategy2.Cash[CurrencyCode.USD].Should().Be(3_000m); // 3000 + 50 - 50
        strategy2.Positions[asset].Should().Be(52);
    }

    [Fact]
    public void UpdateCashForDividend_DripEnabled_ZeroCurrentPrice_ShouldOnlyCreditCash()
    {
        // Arrange
        var asset = new Asset("VTI");
        var testStrategy = new TestStrategy
        {
            Positions = new SortedDictionary<Asset, int> { { asset, 100 } },
            Cash = new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 10_000m } }
        };
        var strategies = new Dictionary<string, IStrategy> { { "TestStrategy", testStrategy } };
        var assetCurrencies = new Dictionary<Asset, CurrencyCode> { { asset, CurrencyCode.USD } };

        var portfolio = new Portfolio(
            CurrencyCode.USD,
            new ReadOnlyDictionary<string, IStrategy>(strategies),
            assetCurrencies,
            _handlers,
            _mockBroker.Object,
            NullLogger<Portfolio>.Instance,
            isLive: true,
            enableDividendReinvestment: true
        );

        // Act — DRIP enabled but no price provided (default 0)
        portfolio.UpdateCashForDividend(asset, 0.50m);

        // Assert — dividend credited, no reinvestment
        testStrategy.Cash[CurrencyCode.USD].Should().Be(10_050m);
        testStrategy.Positions[asset].Should().Be(100);
    }
}
