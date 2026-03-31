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
using Boutquin.Trading.Domain.Events;
using Boutquin.Trading.Domain.ValueObjects;

using Moq;

public sealed class DrawdownCircuitBreakerTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_tlt = new("TLT");

    private static Mock<IPortfolio> CreatePortfolioMock(
        SortedDictionary<DateOnly, decimal> equityCurve,
        Dictionary<string, IStrategy>? strategies = null)
    {
        var mock = new Mock<IPortfolio>();
        mock.Setup(p => p.EquityCurve).Returns(equityCurve);
        mock.Setup(p => p.Strategies)
            .Returns((IReadOnlyDictionary<string, IStrategy>)(strategies ?? new Dictionary<string, IStrategy>()));
        mock.Setup(p => p.SubmitOrderAsync(It.IsAny<OrderEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
    }

    private static Mock<IStrategy> CreateStrategyMock(Dictionary<Asset, int>? positions = null)
    {
        var mock = new Mock<IStrategy>();
        mock.Setup(s => s.Positions)
            .Returns((IReadOnlyDictionary<Asset, int>)(positions ?? new Dictionary<Asset, int>()));
        return mock;
    }

    // ── Constructor validation ──

    [Fact]
    public void Constructor_InvalidThreshold_ShouldThrow()
    {
        var act1 = () => new DrawdownCircuitBreaker(0m);
        act1.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => new DrawdownCircuitBreaker(1.5m);
        act2.Should().Throw<ArgumentOutOfRangeException>();

        var act3 = () => new DrawdownCircuitBreaker(-0.1m);
        act3.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativeCooldown_ShouldThrow()
    {
        var act = () => new DrawdownCircuitBreaker(0.25m, cooldownDays: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ValidParams_ShouldNotThrow()
    {
        var act = () => new DrawdownCircuitBreaker(0.25m, cooldownDays: 21);
        act.Should().NotThrow();
    }

    // ── No breach ──

    [Fact]
    public async Task CheckAsync_BelowThreshold_NoLiquidation()
    {
        // 5% drawdown, 25% threshold → no action
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100m,
            [new DateOnly(2026, 1, 2)] = 95m,
        };
        var portfolio = CreatePortfolioMock(equityCurve);
        var breaker = new DrawdownCircuitBreaker(0.25m);

        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 1));
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 2));

        breaker.LiquidationCount.Should().Be(0);
        breaker.InCashMode.Should().BeFalse();
        portfolio.Verify(p => p.SubmitOrderAsync(It.IsAny<OrderEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckAsync_PeakTracksCorrectly()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100m,
            [new DateOnly(2026, 1, 2)] = 110m, // new peak
            [new DateOnly(2026, 1, 3)] = 105m, // 4.5% drawdown from 110
        };
        var portfolio = CreatePortfolioMock(equityCurve);
        var breaker = new DrawdownCircuitBreaker(0.05m); // 5% threshold

        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 1));
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 2));
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 3));

        // 4.5% < 5% → no liquidation
        breaker.LiquidationCount.Should().Be(0);
    }

    // ── Breach triggers liquidation ──

    [Fact]
    public async Task CheckAsync_BreachThreshold_LiquidatesAllPositions()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100m,
            [new DateOnly(2026, 1, 2)] = 70m, // 30% drawdown
        };

        var strategy = CreateStrategyMock(new Dictionary<Asset, int>
        {
            [s_vti] = 50,
            [s_tlt] = 30,
        });

        var strategies = new Dictionary<string, IStrategy> { ["Main"] = strategy.Object };
        var portfolio = CreatePortfolioMock(equityCurve, strategies);

        var breaker = new DrawdownCircuitBreaker(0.25m); // 25% threshold

        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 1));
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 2));

        breaker.LiquidationCount.Should().Be(1);
        breaker.InCashMode.Should().BeTrue();

        // Should have submitted 2 sell orders (one per position)
        portfolio.Verify(
            p => p.SubmitOrderAsync(It.IsAny<OrderEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ── Cooldown behavior ──

    [Fact]
    public async Task CheckAsync_DuringCooldown_NoDuplicateLiquidation()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100m,
            [new DateOnly(2026, 1, 2)] = 70m, // breach
            [new DateOnly(2026, 1, 3)] = 65m, // still in drawdown
        };

        var strategy = CreateStrategyMock(new Dictionary<Asset, int> { [s_vti] = 50 });
        var strategies = new Dictionary<string, IStrategy> { ["Main"] = strategy.Object };
        var portfolio = CreatePortfolioMock(equityCurve, strategies);

        var breaker = new DrawdownCircuitBreaker(0.25m, cooldownDays: 5);

        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 1));
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 2)); // triggers
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 3)); // cooldown day 1

        breaker.LiquidationCount.Should().Be(1); // no duplicate
        breaker.InCashMode.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_CooldownExpiry_ExitsCashMode()
    {
        // Build equity curve: peak at 100, breach at 70, then hold
        var equityCurve = new SortedDictionary<DateOnly, decimal>();
        var baseDate = new DateOnly(2026, 1, 1);
        equityCurve[baseDate] = 100m;
        equityCurve[baseDate.AddDays(1)] = 70m; // breach day

        // Add 5 cooldown days
        for (var i = 2; i <= 6; i++)
        {
            equityCurve[baseDate.AddDays(i)] = 72m;
        }

        // Day after cooldown expires
        equityCurve[baseDate.AddDays(7)] = 75m;

        var portfolio = CreatePortfolioMock(equityCurve);
        var breaker = new DrawdownCircuitBreaker(0.25m, cooldownDays: 5);

        // Process each day
        foreach (var date in equityCurve.Keys)
        {
            await breaker.CheckAsync(portfolio.Object, date);
        }

        breaker.LiquidationCount.Should().Be(1);
        breaker.InCashMode.Should().BeFalse(); // cooldown expired
    }

    // ── Edge cases ──

    [Fact]
    public async Task CheckAsync_EmptyPositions_NoSellOrders()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100m,
            [new DateOnly(2026, 1, 2)] = 70m, // breach
        };

        // Strategy with no positions (already liquidated or all-cash)
        var strategy = CreateStrategyMock(new Dictionary<Asset, int>());
        var strategies = new Dictionary<string, IStrategy> { ["Main"] = strategy.Object };
        var portfolio = CreatePortfolioMock(equityCurve, strategies);

        var breaker = new DrawdownCircuitBreaker(0.25m);

        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 1));
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 2));

        breaker.LiquidationCount.Should().Be(1);
        breaker.InCashMode.Should().BeTrue();
        portfolio.Verify(p => p.SubmitOrderAsync(It.IsAny<OrderEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckAsync_FirstValueZeroThenRecovers_NoDivByZero()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 0m,   // total wipeout
            [new DateOnly(2026, 1, 2)] = 50m,  // recovery
            [new DateOnly(2026, 1, 3)] = 45m,  // 10% drawdown from 50
        };
        var portfolio = CreatePortfolioMock(equityCurve);
        var breaker = new DrawdownCircuitBreaker(0.25m);

        // Should not throw — peak should be set to 50 on day 2
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 1));
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 2));
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 3));

        breaker.LiquidationCount.Should().Be(0); // 10% < 25%
    }

    [Fact]
    public async Task CheckAsync_ZeroCooldown_ExitsImmediately()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 100m,
            [new DateOnly(2026, 1, 2)] = 70m, // breach
            [new DateOnly(2026, 1, 3)] = 72m, // next day
        };

        var portfolio = CreatePortfolioMock(equityCurve);
        var breaker = new DrawdownCircuitBreaker(0.25m, cooldownDays: 0);

        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 1));
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 2)); // triggers
        await breaker.CheckAsync(portfolio.Object, new DateOnly(2026, 1, 3)); // exits cash mode immediately

        breaker.LiquidationCount.Should().Be(1);
        breaker.InCashMode.Should().BeFalse();
    }
}
