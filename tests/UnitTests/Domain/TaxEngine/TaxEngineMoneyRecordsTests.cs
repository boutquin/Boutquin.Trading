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

using Boutquin.Domain.Extensions;
using Boutquin.Trading.Domain.TaxEngine;
using Money = Boutquin.Domain.ValueObjects.Money;

namespace Boutquin.Trading.Tests.UnitTests.Domain.TaxEngine;

/// <summary>
/// Tests for the M-9 (M9-TAX) adoption of <see cref="Money"/> on the seven
/// TaxEngine domain records in <c>src/Domain/TaxEngine/</c>. These pin
/// umbrella AC-5 (records side): amount fields carry <see cref="Money"/>
/// (currency-tagged), rate fields stay <see cref="decimal"/>, and summing
/// amounts across currencies throws
/// <see cref="Boutquin.Domain.Exceptions.CurrencyMismatchException"/> where the
/// pre-Money <see cref="decimal"/> code silently computed.
/// </summary>
public sealed class TaxEngineMoneyRecordsTests
{
    private static readonly Symbol s_aapl = new("AAPL");

    private static LotDisposal UsdDisposal(decimal gainLoss) => new(
        PurchaseDate: new DateOnly(2024, 1, 2),
        SaleDate: new DateOnly(2024, 6, 2),
        Quantity: 100m,
        CostPerShare: Currency.USD.Amount(10m),
        SalePrice: Currency.USD.Amount(12m),
        Commission: Currency.USD.Amount(1m),
        RealizedGainLoss: Currency.USD.Amount(gainLoss),
        HoldingPeriod: HoldingPeriod.ShortTerm);

    private static DividendRecord Dividend(Currency currency, decimal amountPerShare) => new(
        ExDate: new DateOnly(2024, 3, 1),
        PayDate: new DateOnly(2024, 3, 15),
        Symbol: s_aapl,
        AmountPerShare: currency.Amount(amountPerShare),
        DividendType: DividendType.UsOrdinary,
        WithholdingTaxRate: 0.15m);

    // ---- AC-5 RED #1: cross-currency summation throws -------------------------

    /// <summary>
    /// Summing the realized gain/loss of a USD <see cref="LotDisposal"/> with a
    /// CAD one must throw
    /// <see cref="Boutquin.Domain.Exceptions.CurrencyMismatchException"/>. With the
    /// pre-Money <see cref="decimal"/> fields this addition silently produced a
    /// nonsense cross-currency total; <see cref="Money"/>'s <c>+</c> operator
    /// throws on mismatch.
    /// </summary>
    [Fact]
    public void SummingRealizedGainLoss_AcrossCurrencies_Throws()
    {
        var usd = UsdDisposal(200m);
        var cad = new LotDisposal(
            PurchaseDate: new DateOnly(2024, 1, 2),
            SaleDate: new DateOnly(2024, 6, 2),
            Quantity: 100m,
            CostPerShare: Currency.CAD.Amount(10m),
            SalePrice: Currency.CAD.Amount(12m),
            Commission: Currency.CAD.Amount(1m),
            RealizedGainLoss: Currency.CAD.Amount(200m),
            HoldingPeriod: HoldingPeriod.ShortTerm);

        var act = () => usd.RealizedGainLoss + cad.RealizedGainLoss;

        act.Should().Throw<Boutquin.Domain.Exceptions.CurrencyMismatchException>();
    }

    /// <summary>
    /// Summing two <see cref="DividendRecord.AmountPerShare"/> values whose
    /// currencies differ (USD vs EUR) must throw
    /// <see cref="Boutquin.Domain.Exceptions.CurrencyMismatchException"/>.
    /// </summary>
    [Fact]
    public void SummingDividendAmountPerShare_AcrossCurrencies_Throws()
    {
        var usd = Dividend(Currency.USD, 1.50m);
        var eur = Dividend(Currency.EUR, 1.50m);

        var act = () => usd.AmountPerShare + eur.AmountPerShare;

        act.Should().Throw<Boutquin.Domain.Exceptions.CurrencyMismatchException>();
    }

    /// <summary>
    /// Same-currency summation is the well-defined complement: it must NOT throw
    /// and must preserve the shared currency tag. This proves the throw above is
    /// a genuine cross-currency guard, not a blanket rejection of addition.
    /// </summary>
    [Fact]
    public void SummingRealizedGainLoss_SameCurrency_Sums()
    {
        var a = UsdDisposal(200m);
        var b = UsdDisposal(50m);

        var total = a.RealizedGainLoss + b.RealizedGainLoss;

        total.Amount.Should().Be(250m);
        total.Currency.Should().Be(Currency.USD);
    }

    // ---- AC-5 RED #2: DividendRecord exposes exactly one currency source ------

    /// <summary>
    /// <see cref="DividendRecord"/> must carry exactly ONE stored currency — the
    /// one inside <see cref="DividendRecord.AmountPerShare"/>. The pre-change
    /// record had a standalone stored <c>Currency</c> field; after M9-TAX that
    /// field is removed as a stored member (SPEC-SCHEMA-08 single-source pin).
    /// This test asserts the record's stored-member surface contains no
    /// independently-settable <c>Currency</c> property.
    /// </summary>
    [Fact]
    public void DividendRecord_ExposesExactlyOneStoredCurrencySource()
    {
        var props = typeof(DividendRecord).GetProperties();

        // Currency-typed members of the record.
        var currencyMembers = props
            .Where(p => p.PropertyType == typeof(Currency))
            .Select(p => p.Name)
            .ToArray();

        // A derived (computed) accessor is allowed; a SECOND stored currency is not.
        // The single stored currency lives inside AmountPerShare (a Money), which
        // is NOT a Currency-typed property itself. So any Currency-typed property
        // present must be the derived accessor with NO init/set (read-only),
        // and there must be at most one of them.
        currencyMembers.Length.Should().BeLessThanOrEqualTo(1,
            "the dividend's currency has a single source (AmountPerShare.Currency); " +
            "a second stored Currency field is a state-space hazard");

        // The constructor must NOT accept a standalone Currency parameter — the
        // currency is supplied solely through the AmountPerShare Money argument.
        var ctor = typeof(DividendRecord).GetConstructors().Single();
        ctor.GetParameters()
            .Any(p => p.ParameterType == typeof(Currency))
            .Should().BeFalse(
                "the record must not take a standalone Currency parameter; " +
                "AmountPerShare (Money) is the sole currency source");
    }

    /// <summary>
    /// If a derived <c>Currency</c> accessor is exposed for consumer convenience,
    /// it must return <see cref="DividendRecord.AmountPerShare"/>'s currency — i.e.
    /// it is a projection of the single source, not an independent value.
    /// </summary>
    [Fact]
    public void DividendRecord_DerivedCurrency_EqualsAmountPerShareCurrency()
    {
        var dividend = Dividend(Currency.EUR, 2.00m);

        dividend.Currency.Should().Be(dividend.AmountPerShare.Currency);
        dividend.Currency.Should().Be(Currency.EUR);
    }

    // ---- AC-5 RED #3: each record tags amounts as Money with correct Currency -

    /// <summary>
    /// Every reshaped record carries its amount fields as <see cref="Money"/>
    /// tagged with the constructed <see cref="Currency"/>; rate fields stay
    /// <see cref="decimal"/>.
    /// </summary>
    [Fact]
    public void AllRecords_TagAmountsAsMoney_WithCorrectCurrency()
    {
        // LotDisposal
        var disposal = UsdDisposal(200m);
        disposal.CostPerShare.Should().Be(Currency.USD.Amount(10m));
        disposal.SalePrice.Currency.Should().Be(Currency.USD);
        disposal.Commission.Currency.Should().Be(Currency.USD);
        disposal.RealizedGainLoss.Should().Be(Currency.USD.Amount(200m));
        ((object)disposal.Quantity).Should().BeOfType<decimal>();

        // TaxLot
        var lot = new TaxLot(
            PurchaseDate: new DateOnly(2024, 1, 2),
            Quantity: 100m,
            CostPerShare: Currency.CAD.Amount(9m),
            Commission: Currency.CAD.Amount(0.5m));
        lot.CostPerShare.Currency.Should().Be(Currency.CAD);
        lot.Commission.Should().Be(Currency.CAD.Amount(0.5m));

        // DividendRecord (AmountPerShare Money; WithholdingTaxRate stays decimal)
        var dividend = Dividend(Currency.USD, 1.25m);
        dividend.AmountPerShare.Should().Be(Currency.USD.Amount(1.25m));
        dividend.WithholdingTaxRate.Should().Be(0.15m);

        // TaxImpact
        var impact = new TaxImpact(
            GrossGainLoss: Currency.USD.Amount(1000m),
            TaxableAmount: Currency.USD.Amount(800m),
            EstimatedTax: Currency.USD.Amount(160m),
            ShortTermGainLoss: Currency.USD.Amount(400m),
            LongTermGainLoss: Currency.USD.Amount(600m));
        impact.GrossGainLoss.Should().Be(Currency.USD.Amount(1000m));
        impact.EstimatedTax.Currency.Should().Be(Currency.USD);

        // DividendTaxImpact
        var divTax = new DividendTaxImpact(
            GrossIncome: Currency.USD.Amount(500m),
            TaxableIncome: Currency.USD.Amount(500m),
            TaxCredits: Currency.USD.Amount(75m),
            EstimatedTax: Currency.USD.Amount(100m));
        divTax.GrossIncome.Should().Be(Currency.USD.Amount(500m));
        divTax.TaxCredits.Currency.Should().Be(Currency.USD);

        // LossHarvestingResult (amounts Money; IsLossDisallowed/Rule unchanged)
        var loss = new LossHarvestingResult(
            IsLossDisallowed: true,
            DisallowedAmount: Currency.USD.Amount(300m),
            AllowedAmount: Currency.USD.Amount(0m),
            Rule: "WashSale");
        loss.DisallowedAmount.Should().Be(Currency.USD.Amount(300m));
        loss.IsLossDisallowed.Should().BeTrue();
        loss.Rule.Should().Be("WashSale");

        // RocAdjustmentResult
        var roc = new RocAdjustmentResult(
            NewCostBasis: Currency.USD.Amount(50m),
            DeemedCapitalGain: Currency.USD.Amount(0m));
        roc.NewCostBasis.Currency.Should().Be(Currency.USD);
        roc.DeemedCapitalGain.Should().Be(Currency.USD.Amount(0m));
    }
}
