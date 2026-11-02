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

using System.Reflection;

using Boutquin.Trading.DataAccess;

using Microsoft.EntityFrameworkCore;

using FxRate = Boutquin.Trading.DataAccess.Entities.FxRate;

namespace Boutquin.Trading.Tests.UnitTests.DataAccess;

/// <summary>
/// Entity CRUD / relationship tests (in-memory): for each mapped entity, proves the entity
/// round-trips through a <see cref="SecurityMasterContextFixture.CreateInMemory"/> context —
/// saved, then reloaded from the store (never just the locally tracked instance) — that a
/// required property is actually enforced, and that <see cref="Position"/> is NOT part of the EF
/// model. Each test constructs its own uniquely-named in-memory context — isolated, no shared
/// state, no dependency on execution order.
/// </summary>
/// <remarks>
/// Covers that entities can be saved and reloaded and that their required properties are
/// enforced. Whether seed data is present for each reference table is covered separately by
/// <see cref="SecurityMasterContextSeedTests"/>.
/// </remarks>
public sealed class SecurityMasterContextCrudTests
{
    /// <summary>
    /// Forces the private <c>_id</c> backing field of a not-yet-saved <see cref="Security"/> to a
    /// synthetic positive value, standing in for the identity a real auto-increment primary key
    /// would assign.
    /// </summary>
    /// <remarks>
    /// EF Core's on-add value-generation heuristic treats a key property's CURRENT value as an
    /// intentionally-provided key whenever it is non-default, and <see cref="Security"/>'s
    /// constructor seeds <c>_id</c> to <c>-1</c> (not <c>0</c>) — confirmed by direct
    /// <c>ChangeTracker</c> inspection that the in-memory provider persists <c>-1</c> verbatim
    /// rather than generating a new value. <c>SecurityId.Create(int)</c>
    /// (which backs <see cref="Security.Id"/>) then throws for any non-positive id, so a freshly
    /// saved <see cref="Security"/>'s id can never be read back through the public API. This
    /// reflection assignment — the same private-field mechanism EF's own materializer uses —
    /// unblocks the FK relationship tests below without touching <c>Security.cs</c> or
    /// <c>SecurityConfiguration.cs</c>.
    /// </remarks>
    private static void AssignSyntheticPersistedId(Security security, int id)
    {
        typeof(Security)
            .GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(security, id);
    }

    /// <summary>
    /// Verifies a <see cref="Security"/> saved through the context reloads with the same Name,
    /// AssetClassCode, and Exchange navigation after the local change tracker is cleared — proving
    /// the values came back from the in-memory store, not the tracked instance still held in
    /// memory.
    /// </summary>
    [Fact]
    public void Security_CanSaveAndLoad()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();
        var exchange = context.Exchanges.First();
        const string securityName = "Acme Corp - Security_CanSaveAndLoad";
        var security = new Security(securityName, AssetClassCode.Equities, exchange);

        context.Securities.Add(security);
        context.SaveChanges();

        // Force a real reload from the store rather than reading back the tracked instance.
        context.ChangeTracker.Clear();

        var loaded = context.Securities
            .Include(s => s.Exchange)
            .Single(s => s.Name == securityName);
        loaded.Name.Should().Be(securityName);
        loaded.AssetClassCode.Should().Be(AssetClassCode.Equities);
        loaded.Exchange.Code.Should().Be(exchange.Code);
    }

    /// <summary>
    /// Verifies <see cref="Security.Name"/> is configured <c>.IsRequired()</c>
    /// (<c>SecurityConfiguration.cs</c>) — saving a <see cref="Security"/> whose Name has been
    /// forced to <see langword="null"/> throws on <see cref="DbContext.SaveChanges()"/>. The
    /// public constructor guards against a null name (<c>Guard.AgainstNullOrWhiteSpaceAndOverflow</c>),
    /// so this test uses reflection on the private-setter property to force the invalid state past
    /// the constructor guard and exercise the EF-level required-property enforcement itself.
    /// </summary>
    [Fact]
    public void Security_RequiredName_Enforced()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();
        var exchange = context.Exchanges.First();
        var security = new Security("Valid Name - Security_RequiredName_Enforced", AssetClassCode.Equities, exchange);
        context.Securities.Add(security);

        typeof(Security).GetProperty(nameof(Security.Name))!.SetValue(security, null);

        var act = () => context.SaveChanges();

        act.Should().Throw<DbUpdateException>();
    }

    /// <summary>
    /// Verifies the Security → SecuritySymbol foreign-key relationship (configured in
    /// <c>SecurityConfiguration.cs</c> via <c>HasMany(s => s.SecuritySymbols).WithOne().HasForeignKey(s => s.SecurityId)</c>)
    /// round-trips both directions after a store reload — the symbol carries the security's id,
    /// and the security's navigation collection contains it back. See
    /// <see cref="AssignSyntheticPersistedId"/> for why the id is assigned synthetically rather
    /// than read back from <see cref="Security.Id"/> after save.
    /// </summary>
    [Fact]
    public void SecuritySymbol_CanSaveWithValidSecurity()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();
        var exchange = context.Exchanges.First();
        const string securityName = "FK Test Security - SecuritySymbol_CanSaveWithValidSecurity";
        const int securityId = 5001;
        var security = new Security(securityName, AssetClassCode.Equities, exchange);
        AssignSyntheticPersistedId(security, securityId);
        context.Securities.Add(security);
        context.SaveChanges();

        const string symbolValue = "ACME-CUSIP";
        var symbol = new SecuritySymbol(securityId, symbolValue, SecuritySymbolStandard.Cusip);
        context.SecuritySymbols.Add(symbol);
        context.SaveChanges();

        context.ChangeTracker.Clear();

        var reloadedSymbol = context.SecuritySymbols.Single(ss => ss.Symbol == symbolValue);
        reloadedSymbol.SecurityId.Should().Be(securityId);

        var reloadedSecurity = context.Securities
            .Include(s => s.SecuritySymbols)
            .Single(s => s.Name == securityName);
        reloadedSecurity.SecuritySymbols.Should().ContainSingle(ss => ss.Symbol == symbolValue);
    }

    /// <summary>
    /// Verifies the Security → SecurityPrice foreign-key relationship (configured in
    /// <c>SecurityConfiguration.cs</c> via <c>HasMany(s => s.SecurityPrices).WithOne().HasForeignKey(s => s.SecurityId)</c>)
    /// round-trips both directions after a store reload. See
    /// <see cref="AssignSyntheticPersistedId"/> for why the id is assigned synthetically rather
    /// than read back from <see cref="Security.Id"/> after save.
    /// </summary>
    [Fact]
    public void SecurityPrice_CanSaveWithValidSecurity()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();
        var exchange = context.Exchanges.First();
        const string securityName = "FK Test Security - SecurityPrice_CanSaveWithValidSecurity";
        const int securityId = 5002;
        var security = new Security(securityName, AssetClassCode.Equities, exchange);
        AssignSyntheticPersistedId(security, securityId);
        context.Securities.Add(security);
        context.SaveChanges();

        var tradeDate = new DateOnly(2026, 3, 2);
        var price = new SecurityPrice(
            tradeDate,
            securityId,
            openPrice: 100.00m,
            highPrice: 105.00m,
            lowPrice: 95.00m,
            closePrice: 102.50m,
            volume: 10_000L,
            dividend: 0m);
        context.SecurityPrices.Add(price);
        context.SaveChanges();

        context.ChangeTracker.Clear();

        var reloadedPrice = context.SecurityPrices
            .Single(p => p.TradeDate == tradeDate && p.SecurityId == securityId);
        reloadedPrice.ClosePrice.Should().Be(102.50m);

        var reloadedSecurity = context.Securities
            .Include(s => s.SecurityPrices)
            .Single(s => s.Name == securityName);
        reloadedSecurity.SecurityPrices.Should().ContainSingle(p => p.TradeDate == tradeDate);
    }

    /// <summary>
    /// Verifies an <see cref="FxRate"/> round-trips through the store, including its
    /// <see cref="DateOnly"/> <c>RateDate</c> conversion (<c>DateOnlyConverter</c>, column type
    /// <c>Date</c> — configured in <c>FxRateConfiguration.cs</c>).
    /// </summary>
    [Fact]
    public void FxRate_CanSaveAndLoad()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();
        var rateDate = new DateOnly(2026, 6, 15);
        var fxRate = new FxRate(rateDate, Currency.USD, Currency.CAD, 1.35m);

        context.FxRates.Add(fxRate);
        context.SaveChanges();

        context.ChangeTracker.Clear();

        var loaded = context.FxRates.Single(r =>
            r.BaseCurrencyCode == Currency.USD
            && r.QuoteCurrencyCode == Currency.CAD
            && r.RateDate == rateDate);
        loaded.RateDate.Should().Be(rateDate);
        loaded.Rate.Should().Be(1.35m);
    }

    /// <summary>
    /// Verifies an <see cref="ExchangeHoliday"/> round-trips through the store, including its
    /// <see cref="DateOnly"/> <c>HolidayDate</c> conversion (<c>DateOnlyConverter</c>, column type
    /// <c>Date</c> — configured in <c>ExchangeHolidayConfiguration.cs</c>).
    /// </summary>
    [Fact]
    public void ExchangeHoliday_CanSaveAndLoad()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();
        var holidayDate = new DateOnly(2026, 12, 25);
        const string description = "Christmas Day - ExchangeHoliday_CanSaveAndLoad";
        var holiday = new ExchangeHoliday(ExchangeCode.XNYS, holidayDate, description);

        context.ExchangeHolidays.Add(holiday);
        context.SaveChanges();

        context.ChangeTracker.Clear();

        var loaded = context.ExchangeHolidays.Single(h => h.Description == description);
        loaded.HolidayDate.Should().Be(holidayDate);
        loaded.ExchangeCode.Should().Be(ExchangeCode.XNYS);
    }

    /// <summary>
    /// Verifies an <see cref="ExchangeSchedule"/> round-trips through the store. Uses Saturday (the
    /// seeded schedules in <c>ExchangeScheduleConfiguration.cs</c> only cover Monday..Friday) so
    /// this test's row is additive rather than colliding with the seeded
    /// (ExchangeCode, DayOfWeek) unique index.
    /// </summary>
    [Fact]
    public void ExchangeSchedule_CanSaveAndLoad()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();
        var openTime = new TimeSpan(9, 30, 0);
        var closeTime = new TimeSpan(16, 0, 0);
        var schedule = new ExchangeSchedule(ExchangeCode.XNYS, DayOfWeek.Saturday, openTime, closeTime);

        context.ExchangeSchedules.Add(schedule);
        context.SaveChanges();

        context.ChangeTracker.Clear();

        var loaded = context.ExchangeSchedules
            .Single(s => s.ExchangeCode == ExchangeCode.XNYS && s.DayOfWeek == DayOfWeek.Saturday);
        loaded.OpenTime.Should().Be(openTime);
        loaded.CloseTime.Should().Be(closeTime);
    }

    /// <summary>
    /// Verifies <see cref="Position"/> is an in-memory domain object, not an EF entity —
    /// <see cref="SecurityMasterContext"/> exposes no <c>DbSet&lt;Position&gt;</c>, so it must be
    /// absent from the context's EF model entirely.
    /// </summary>
    [Fact]
    public void Position_IsNotAnEfEntity()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();

        context.Model.FindEntityType(typeof(Position)).Should().BeNull();
    }
}
