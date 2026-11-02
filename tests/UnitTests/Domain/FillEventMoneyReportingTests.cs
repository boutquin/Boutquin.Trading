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

using Money = Boutquin.Domain.ValueObjects.Money;

namespace Boutquin.Trading.Tests.UnitTests.Domain;

/// <summary>
/// M-9 (M9-BND) tests for the <see cref="Money"/>-typed reporting projection of a
/// <see cref="FillEvent"/>. The <see cref="FillEvent.FillPrice"/> and
/// <see cref="FillEvent.Commission"/> fields stay <see cref="decimal"/> (produced adjacent to
/// the hot loop); only the reporting projection is <see cref="Money"/>. These pin the AC-4
/// additive-boundary requirement that fill/commission can be reported as currency-tagged
/// <see cref="Money"/> equal to the backing <see cref="decimal"/> field amount-for-amount.
/// </summary>
public sealed class FillEventMoneyReportingTests
{
    private static readonly Symbol s_spy = new("SPY");

    private static FillEvent BuildFill() => new(
        Timestamp: new DateOnly(2024, 6, 2),
        Symbol: s_spy,
        StrategyName: "BuyAndHold",
        TradeAction: TradeAction.Buy,
        FillPrice: 472.37m,
        Quantity: 100,
        Commission: 4.7237m);

    /// <summary>
    /// <see cref="FillEvent.FillPriceAsMoney"/> returns the <see cref="FillEvent.FillPrice"/>
    /// decimal tagged with the supplied currency.
    /// </summary>
    [Fact]
    public void FillPriceAsMoney_TagsFillPriceWithSuppliedCurrency()
    {
        var fill = BuildFill();

        var money = fill.FillPriceAsMoney(Currency.USD);

        money.Amount.Should().Be(fill.FillPrice);
        money.Currency.Should().Be(Currency.USD);
    }

    /// <summary>
    /// <see cref="FillEvent.CommissionAsMoney"/> returns the <see cref="FillEvent.Commission"/>
    /// decimal tagged with the supplied currency.
    /// </summary>
    [Fact]
    public void CommissionAsMoney_TagsCommissionWithSuppliedCurrency()
    {
        var fill = BuildFill();

        var money = fill.CommissionAsMoney(Currency.CAD);

        money.Amount.Should().Be(fill.Commission);
        money.Currency.Should().Be(Currency.CAD);
    }
}
