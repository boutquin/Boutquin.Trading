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

/// <summary>
/// Pins the share-granularity tolerance of <see cref="MaxPositionSizeRule"/> and
/// <see cref="MaxSectorExposureRule"/>. Position sizing rounds share counts with
/// <see cref="MidpointRounding.AwayFromZero"/>, so a target exactly at a cap overshoots it by up to
/// half a share on roughly half of all rebalances. A cap on a discrete instrument cannot be honoured
/// more finely than one share, so each rule tolerates <c>max(fixed tolerance, one share's weight)</c>
/// above the cap — and still rejects anything more than one share over.
/// </summary>
public sealed class RiskRuleShareGranularityTests
{
    private static readonly Symbol s_vti = new("VTI");
    private static readonly Symbol s_hyg = new("HYG");
    private static readonly DateOnly s_date = new(2026, 1, 2);
    private const decimal Price = 100m;

    // ─── MaxPositionSizeRule ─────────────────────────────────────────────

    [Fact]
    public void MaxPositionSize_TargetAtCap_RoundsUpHalfAShare_IsAllowed()
    {
        // 100,050 portfolio, 100-unit share, 100 % cap: the sizer's target is 1,000.5 shares, which
        // AwayFromZero rounds to 1,001 = 100,100 = 100.04998 %. That is 5 bp over the cap — five times
        // the fixed 1 bp tolerance — but half a share, inside the one-share granularity (0.09995 %).
        const decimal total = 100_050m;
        var shares = SizerShares(total, weight: 1.0m, Price);
        shares.Should().Be(1_001, "AwayFromZero rounds the half share up — the overshoot under test");
        var portfolio = UsdPortfolio(total, existing: new());
        var rule = new MaxPositionSizeRule(1.0m);
        var order = Buy(s_vti, shares);

        rule.Evaluate(order, portfolio).IsAllowed.Should().BeTrue("half a share over the cap is rounding, not a breach");
        rule.EvaluateBatch([order], portfolio).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void MaxPositionSize_MoreThanOneShareOverCap_IsRejected()
    {
        // 1,002 shares = 100,200 / 100,050 = 100.15 % — one and a half shares over the cap; the bound is one.
        const decimal total = 100_050m;
        var portfolio = UsdPortfolio(total, existing: new());
        var rule = new MaxPositionSizeRule(1.0m);
        var order = Buy(s_vti, 1_002);

        var single = rule.Evaluate(order, portfolio);
        single.IsAllowed.Should().BeFalse();
        single.RejectionReason.Should().Contain("VTI");
        rule.EvaluateBatch([order], portfolio).IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void MaxPositionSize_ShareWorthLessThanOneBp_KeepsTheFixedTolerance()
    {
        // 10,000,000 portfolio: one 100-unit share is 0.001 % (0.1 bp), below the fixed 1 bp tolerance,
        // so the fixed tolerance stays binding — exactly the previous behaviour for large portfolios.
        const decimal total = 10_000_000m;
        var portfolio = UsdPortfolio(total, existing: new());
        var rule = new MaxPositionSizeRule(0.25m);

        rule.Evaluate(Buy(s_vti, 25_001), portfolio).IsAllowed.Should().BeTrue("25.001 % is 0.1 bp over, inside 1 bp");
        rule.Evaluate(Buy(s_vti, 25_011), portfolio).IsAllowed.Should().BeFalse("25.011 % is 1.1 bp over, outside 1 bp");
        rule.EvaluateBatch([Buy(s_vti, 25_001)], portfolio).IsAllowed.Should().BeTrue();
        rule.EvaluateBatch([Buy(s_vti, 25_011)], portfolio).IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void MaxPositionSize_ForeignAsset_MeasuresOneShareInBaseCurrency()
    {
        // The etfwealthiq CAD arm: a 130,065 CAD portfolio buying a 100 USD share at 1.30 CAD/USD (130 CAD).
        // The sizer converts the CAD target into USD (100,050 USD) and rounds 1,000.5 → 1,001 shares =
        // 130,130 CAD = 100.04998 %. One share is 130 CAD = 0.09995 % of the portfolio, measured in base.
        const decimal totalCad = 130_065m;
        const decimal usdCad = 1.30m;
        var shares = SizerShares(totalCad / usdCad, weight: 1.0m, Price);
        shares.Should().Be(1_001);
        var portfolio = CadPortfolioWithUsdAsset(totalCad, usdCad);
        var rule = new MaxPositionSizeRule(1.0m);

        rule.Evaluate(Buy(s_vti, shares), portfolio).IsAllowed.Should().BeTrue();
        rule.EvaluateBatch([Buy(s_vti, shares)], portfolio).IsAllowed.Should().BeTrue();
        rule.Evaluate(Buy(s_vti, shares + 1), portfolio).IsAllowed.Should().BeFalse("one and a half shares over the cap");
    }

    // ─── MaxSectorExposureRule ───────────────────────────────────────────

    [Fact]
    public void MaxSectorExposure_TwoHoldingsAtCap_EachRoundingUpHalfAShare_IsAllowed()
    {
        // 20,100 portfolio, two 100-unit equity ETFs at 50 % each under a 100 % equity cap: each target is
        // 100.5 shares → 101, so the class holds 20,200 = 100.4975 % — 50 bp over, five times the fixed
        // 10 bp tolerance, but half a share per holding; the bound is one share per holding (0.995 %).
        const decimal total = 20_100m;
        var shares = SizerShares(total, weight: 0.5m, Price);
        shares.Should().Be(101);
        var rule = new MaxSectorExposureRule(1.0m, EquityClasses());

        // Batch: a fresh portfolio buying both legs together.
        rule.EvaluateBatch([Buy(s_vti, shares), Buy(s_hyg, shares)], UsdPortfolio(total, existing: new()))
            .IsAllowed.Should().BeTrue();

        // Single order: the second leg arriving after the first has filled.
        rule.Evaluate(Buy(s_hyg, shares), UsdPortfolio(total, existing: new() { [s_vti] = shares }))
            .IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void MaxSectorExposure_MoreThanOneSharePerHoldingOverCap_IsRejected()
    {
        // 102 + 102 shares = 20,400 / 20,100 = 101.49 % — two shares per holding over the cap.
        const decimal total = 20_100m;
        var rule = new MaxSectorExposureRule(1.0m, EquityClasses());

        var batch = rule.EvaluateBatch([Buy(s_vti, 102), Buy(s_hyg, 102)], UsdPortfolio(total, existing: new()));
        batch.IsAllowed.Should().BeFalse();
        batch.RejectionReason.Should().Contain("Equities");

        rule.Evaluate(Buy(s_hyg, 102), UsdPortfolio(total, existing: new() { [s_vti] = 102 }))
            .IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void MaxSectorExposure_SharesWorthLessThanTenBp_KeepsTheFixedTolerance()
    {
        // 1,000,000 portfolio, 50 % equity cap = 5,000 shares across two 100-unit ETFs. Two shares are
        // 0.02 % (2 bp), below the fixed 10 bp tolerance, so the fixed tolerance stays binding:
        // 5 bp over (2,503 + 2,502) passes, 15 bp over (2,508 + 2,507) fails.
        const decimal total = 1_000_000m;
        var rule = new MaxSectorExposureRule(0.50m, EquityClasses());
        var fresh = UsdPortfolio(total, existing: new());

        rule.EvaluateBatch([Buy(s_vti, 2_503), Buy(s_hyg, 2_502)], fresh).IsAllowed.Should().BeTrue("50.05 % is inside 10 bp");
        rule.EvaluateBatch([Buy(s_vti, 2_508), Buy(s_hyg, 2_507)], fresh).IsAllowed.Should().BeFalse("50.15 % is outside 10 bp");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    /// <summary>The share count the position sizers produce: <c>Round(value × weight / price, AwayFromZero)</c>.</summary>
    private static int SizerShares(decimal allocatableValue, decimal weight, decimal price) =>
        (int)Math.Round(allocatableValue * weight / price, MidpointRounding.AwayFromZero);

    private static Order Buy(Symbol asset, int quantity) =>
        new(s_date, "S", asset, TradeAction.Buy, OrderType.Market, quantity);

    private static Bar BarAt(decimal price) => new(s_date, price, price, price, price, price, 1_000_000);

    private static IReadOnlyDictionary<Symbol, AssetClassCode> EquityClasses() =>
        new Dictionary<Symbol, AssetClassCode> { [s_vti] = AssetClassCode.Equities, [s_hyg] = AssetClassCode.Equities };

    private static IPortfolio UsdPortfolio(decimal totalValue, Dictionary<Symbol, int> existing)
    {
        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Positions).Returns(existing);

        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.BaseCurrency).Returns(Currency.USD);
        portfolio.Setup(p => p.GetAssetCurrency(It.IsAny<Symbol>())).Returns(Currency.USD);
        portfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy> { ["S"] = strategy.Object });
        portfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal> { [s_date] = totalValue });
        portfolio.Setup(p => p.CalculateTotalPortfolioValue(s_date)).Returns(totalValue);
        portfolio.Setup(p => p.HistoricalMarketData).Returns(new SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>>
        {
            [s_date] = new SortedDictionary<Symbol, Bar> { [s_vti] = BarAt(Price), [s_hyg] = BarAt(Price) },
        });
        portfolio.Setup(p => p.HistoricalFxConversionRates).Returns(new SortedDictionary<DateOnly, SortedDictionary<Currency, decimal>>
        {
            [s_date] = new SortedDictionary<Currency, decimal>(),
        });
        return portfolio.Object;
    }

    private static IPortfolio CadPortfolioWithUsdAsset(decimal totalEquityCad, decimal usdCad)
    {
        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Positions).Returns(new Dictionary<Symbol, int>());

        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.BaseCurrency).Returns(Currency.CAD);
        portfolio.Setup(p => p.GetAssetCurrency(s_vti)).Returns(Currency.USD);
        portfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy> { ["S"] = strategy.Object });
        portfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal> { [s_date] = totalEquityCad });
        portfolio.Setup(p => p.CalculateTotalPortfolioValue(s_date)).Returns(totalEquityCad);
        portfolio.Setup(p => p.HistoricalMarketData).Returns(new SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>>
        {
            [s_date] = new SortedDictionary<Symbol, Bar> { [s_vti] = BarAt(Price) },
        });
        portfolio.Setup(p => p.HistoricalFxConversionRates).Returns(new SortedDictionary<DateOnly, SortedDictionary<Currency, decimal>>
        {
            [s_date] = new SortedDictionary<Currency, decimal> { [Currency.USD] = usdCad },
        });
        return portfolio.Object;
    }
}
