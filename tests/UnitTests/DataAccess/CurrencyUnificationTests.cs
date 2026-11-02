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

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using CurrencyEntity = Boutquin.Trading.DataAccess.Entities.Currency;
using FxRateEntity = Boutquin.Trading.DataAccess.Entities.FxRate;

namespace Boutquin.Trading.Tests.UnitTests.DataAccess;

/// <summary>
/// Serialization-safety tests for the currency-enum unification onto
/// <see cref="Boutquin.Domain.Enumerations.Currency"/> (Phase-1 / M-5).
/// Proves the EF currency-bearing entities persist byte-identically (same
/// ISO-4217 numeric values, same stored string) after the type swap, and
/// pins the <c>Unspecified = 0</c> default-sentinel behaviour.
/// </summary>
public sealed class CurrencyUnificationTests
{
    // Representative currencies carried by the seed data / entities, with their
    // ISO-4217 numeric codes. These values must be unchanged by the swap from the
    // former CurrencyCode enum (identical numeric backing is what makes the migration
    // data-safe), so existing rows read back to the same logical currency.
    public static IEnumerable<object[]> SeedCurrencies =>
    [
        [Currency.USD, 840], [Currency.CAD, 124], [Currency.MXN, 484],
        [Currency.GBP, 826], [Currency.EUR, 978], [Currency.JPY, 392],
        [Currency.CNY, 156], [Currency.INR, 356], [Currency.AUD, 36],
        [Currency.BRL, 986], [Currency.RUB, 643], [Currency.KRW, 410],
        [Currency.HKD, 344],
    ];

    /// <summary>
    /// AC-3: each currency carries the same ISO-4217 numeric value it did before the swap.
    /// </summary>
    [Theory]
    [MemberData(nameof(SeedCurrencies))]
    public void Currency_NumericValue_IsPreservedIso4217(Currency currency, int expectedNumeric)
    {
        ((int)currency).Should().Be(expectedNumeric);
    }

    /// <summary>
    /// AC-3: the EF string conversion (the configured <c>HasConversion&lt;string&gt;()</c>) round-trips
    /// byte-identically — the stored column value is the unchanged ISO-4217 alphabetic code.
    /// </summary>
    [Theory]
    [MemberData(nameof(SeedCurrencies))]
    public void Currency_EnumToStringConverter_RoundTripsByteIdentical(Currency currency, int _)
    {
        var converter = new EnumToStringConverter<Currency>();

        var stored = (string)converter.ConvertToProvider(currency)!;
        stored.Should().Be(currency.ToString());

        var roundTripped = (Currency)converter.ConvertFromProvider(stored)!;
        roundTripped.Should().Be(currency);
    }

    /// <summary>
    /// AC-3: the three currency-bearing EF entities carry the unified
    /// <see cref="Boutquin.Domain.Enumerations.Currency"/> on their currency members,
    /// with the expected numeric value, after the swap.
    /// </summary>
    [Fact]
    public void CurrencyBearingEntities_CarryUnifiedCurrency()
    {
        var currencyEntity = new CurrencyEntity(Currency.USD, 840, "United States dollar", "$");
        currencyEntity.Code.Should().Be(Currency.USD);
        ((int)currencyEntity.Code).Should().Be(840);

        var fxRate = new FxRateEntity(new DateOnly(2026, 1, 1), Currency.USD, Currency.CAD, 1.35m);
        fxRate.BaseCurrencyCode.Should().Be(Currency.USD);
        fxRate.QuoteCurrencyCode.Should().Be(Currency.CAD);
        ((int)fxRate.BaseCurrencyCode).Should().Be(840);
        ((int)fxRate.QuoteCurrencyCode).Should().Be(124);

        var country = new Country(CountryCode.US, "United States", 840, Currency.USD, ContinentCode.NA);
        country.CurrencyCode.Should().Be(Currency.USD);
        ((int)country.CurrencyCode).Should().Be(840);
    }

    /// <summary>
    /// AC-5b: the unified enum has a usable <c>Unspecified = 0</c> sentinel, so
    /// <c>default(Currency)</c> is a defined value (the former CurrencyCode enum had no 0 member).
    /// </summary>
    [Fact]
    public void DefaultCurrency_IsUnspecifiedSentinel()
    {
        default(Currency).Should().Be(Currency.Unspecified);
        ((int)Currency.Unspecified).Should().Be(0);
        Enum.IsDefined(Currency.Unspecified).Should().BeTrue();
    }
}
