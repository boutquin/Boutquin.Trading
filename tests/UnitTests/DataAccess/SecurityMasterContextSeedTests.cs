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

namespace Boutquin.Trading.Tests.UnitTests.DataAccess;

/// <summary>
/// Seed-data verification tests (in-memory): for each of the 8 reference tables the entity
/// configurations seed via <c>HasData(...)</c>, asserts the seeded row count after
/// <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreated()"/>
/// meets the known minimum. Each test constructs its own uniquely-named in-memory
/// <c>SecurityMasterContext</c> via <see cref="SecurityMasterContextFixture.CreateInMemory"/> —
/// isolated, no shared state, no dependency on execution order.
/// </summary>
/// <remarks>
/// Covers only that seed data is present for each reference table. Whether an entity can be
/// saved and reloaded, and whether its required properties are enforced, is covered separately
/// by <see cref="SecurityMasterContextCrudTests"/>. Assertions use <c>&gt;=</c> rather than
/// <c>==</c> so the tests stay robust to seed-data growth.
/// </remarks>
public sealed class SecurityMasterContextSeedTests
{
    // Known seed minimums, applied by EnsureCreated via the entity configurations. A
    // misconfigured provider, a missing EnsureCreated, or a broken/removed HasData(...) seed
    // would leave the corresponding table below its minimum (or empty), so each assertion below
    // is load-bearing against the real materialized seed, not a constructed-in-test value.
    private const int MinAssetClasses = 8;
    private const int MinContinents = 7;
    private const int MinCountries = 1;
    private const int MinCities = 9;
    private const int MinExchanges = 1;
    private const int MinSymbolStandards = 5;
    private const int MinTimeZones = 1;

    // Boutquin.Domain.Enumerations.Currency (aliased Currency in this project's GlobalUsings) is
    // the full ISO 4217 catalogue — 178 members, including the Unspecified sentinel.
    // CurrencyConfiguration.Configure seeds only 13 explicit Currency rows (USD, CAD, MXN, GBP,
    // EUR, JPY, CNY, INR, AUD, BRL, RUB, KRW, HKD) — a curated subset, not the full catalogue.
    // The assertion below therefore pins the true seeded minimum rather than the (much larger)
    // enum member count.
    private const int MinSeededCurrencies = 13;

    /// <summary>Validates <c>AssetClassConfiguration</c> seeds CashAndCashEquivalents..Other.</summary>
    [Fact]
    public void AssetClass_SeedRowCount_MeetsSpecMinimum()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();

        context.AssetClasses.Count().Should().BeGreaterThanOrEqualTo(MinAssetClasses);
    }

    /// <summary>Validates <c>ContinentConfiguration</c> seeds AF, AN, AS, EU, NA, OC, SA.</summary>
    [Fact]
    public void Continent_SeedRowCount_MeetsSpecMinimum()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();

        context.Continents.Count().Should().BeGreaterThanOrEqualTo(MinContinents);
    }

    /// <summary>
    /// Validates <c>CurrencyConfiguration</c> seeds its curated 13-row currency set. See the
    /// note above <see cref="MinSeededCurrencies"/> for why this asserts the true seeded count
    /// rather than the full <c>Currency</c> enum member count.
    /// </summary>
    [Fact]
    public void Currency_SeedRowCount_MeetsActualSeededMinimum()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();

        context.Currencies.Count().Should().BeGreaterThanOrEqualTo(MinSeededCurrencies);
    }

    /// <summary>Validates <c>CountryConfiguration</c> seeds country-continent relationships.</summary>
    [Fact]
    public void Country_SeedRowCount_MeetsSpecMinimum()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();

        context.Countries.Count().Should().BeGreaterThanOrEqualTo(MinCountries);
    }

    /// <summary>Validates <c>CityConfiguration</c> seeds New York through Toronto.</summary>
    [Fact]
    public void City_SeedRowCount_MeetsSpecMinimum()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();

        context.Cities.Count().Should().BeGreaterThanOrEqualTo(MinCities);
    }

    /// <summary>Validates <c>ExchangeConfiguration</c> seeds exchange-city relationships.</summary>
    [Fact]
    public void Exchange_SeedRowCount_MeetsSpecMinimum()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();

        context.Exchanges.Count().Should().BeGreaterThanOrEqualTo(MinExchanges);
    }

    /// <summary>Validates <c>SymbolStandardConfiguration</c> seeds Cusip, Isin, Sedol, Ric, Bloomberg.</summary>
    [Fact]
    public void SymbolStandard_SeedRowCount_MeetsSpecMinimum()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();

        context.SymbolStandards.Count().Should().BeGreaterThanOrEqualTo(MinSymbolStandards);
    }

    /// <summary>Validates <c>TimeZoneConfiguration</c> seeds time zone entries.</summary>
    [Fact]
    public void TimeZone_SeedRowCount_MeetsSpecMinimum()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();

        context.TimeZones.Count().Should().BeGreaterThanOrEqualTo(MinTimeZones);
    }
}
