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

using Microsoft.EntityFrameworkCore;

using CurrencyEntity = Boutquin.Trading.DataAccess.Entities.Currency;
using FxRate = Boutquin.Trading.DataAccess.Entities.FxRate;

namespace Boutquin.Trading.Tests.UnitTests.DataAccess;

/// <summary>
/// Index &amp; constraint tests (SQLite): proves a unique index or primary key is enforced (or,
/// for the one "allowed" case, deliberately NOT enforced) against a real SQLite database — the
/// EF in-memory provider used by <see cref="SecurityMasterContextSeedTests"/> and
/// <see cref="SecurityMasterContextCrudTests"/> ignores indexes entirely, so these constraints
/// are only observable here.
/// </summary>
/// <remarks>
/// <see cref="ExchangeHoliday_SameDescriptionDifferentExchanges_Allowed"/> validates that the
/// removed unique index on <c>ExchangeHoliday.Description</c> is in fact gone — two holidays may
/// share a description across different exchanges. <see cref="ExchangeHoliday_SameExchangeAndDate_Rejected"/>
/// validates the composite <c>(ExchangeCode, HolidayDate)</c> unique index that was left in
/// place. The remaining four tests exercise the other three unique indexes and the
/// <c>Currency</c> primary key. Each test opens its own SQLite <c>:memory:</c> connection via
/// <see cref="SecurityMasterContextFixture.CreateSqlite"/> and disposes the context before the
/// connection (a <c>:memory:</c> database vanishes the instant its owning connection closes), so
/// no test depends on another's state or on execution order.
/// </remarks>
public sealed class SecurityMasterContextConstraintTests
{
    /// <summary>
    /// Forces the private <c>_id</c> backing field of a not-yet-saved entity to a synthetic
    /// positive value, standing in for the identity a real auto-increment primary key would
    /// assign.
    /// </summary>
    /// <remarks>
    /// Mirrors the identical mechanism used against <c>Security</c> in
    /// <c>SecurityMasterContextCrudTests</c>: both <see cref="ExchangeHoliday"/> and
    /// <see cref="FxRate"/> seed their private <c>_id</c> field to <c>-1</c> in their
    /// constructors (not <c>0</c>), so EF's on-add value-generation heuristic treats every
    /// freshly-constructed, not-yet-saved instance as already carrying an explicit key value of
    /// <c>-1</c>. Adding TWO such instances to the same context in one call collides in EF's
    /// in-memory identity map (<c>InvalidOperationException</c>) before the command ever reaches
    /// SQLite — masking the unique-index/PK behavior these tests exist to prove. Assigning each
    /// instance a distinct synthetic id up front (the same private-field mechanism EF's own
    /// materializer uses) avoids the identity-map collision without touching
    /// <c>ExchangeHoliday.cs</c> / <c>FxRate.cs</c>, and without weakening what's asserted: the
    /// real (ExchangeCode, HolidayDate) / (RateDate, BaseCurrencyCode, QuoteCurrencyCode) unique
    /// indexes still fire at the database on <c>SaveChanges()</c>.
    /// </remarks>
    private static void AssignSyntheticId<TEntity>(TEntity entity, int id)
        where TEntity : class
    {
        typeof(TEntity)
            .GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(entity, id);
    }

    /// <summary>
    /// Verifies <c>ExchangeHolidayConfiguration</c> no longer indexes <c>Description</c>
    /// uniquely — two holidays sharing the same description on two DIFFERENT exchanges (so the
    /// remaining composite <c>(ExchangeCode, HolidayDate)</c> index is not violated either) must
    /// both save without throwing.
    /// </summary>
    [Fact]
    public void ExchangeHoliday_SameDescriptionDifferentExchanges_Allowed()
    {
        var (context, connection) = SecurityMasterContextFixture.CreateSqlite();
        try
        {
            var holidayDate = new DateOnly(2027, 1, 1);
            const string description = "New Year - Phase4ConstraintTests";
            var nyseHoliday = new ExchangeHoliday(ExchangeCode.XNYS, holidayDate, description);
            var lonHoliday = new ExchangeHoliday(ExchangeCode.XLON, holidayDate, description);
            AssignSyntheticId(nyseHoliday, 9001);
            AssignSyntheticId(lonHoliday, 9002);

            context.ExchangeHolidays.AddRange(nyseHoliday, lonHoliday);
            var act = () => context.SaveChanges();

            act.Should().NotThrow();

            context.ChangeTracker.Clear();
            context.ExchangeHolidays.Count(h => h.Description == description).Should().Be(2);
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies <c>ExchangeHolidayConfiguration</c> still indexes <c>(ExchangeCode, HolidayDate)</c>
    /// uniquely — two holidays on the SAME exchange and date (with different, non-colliding
    /// descriptions) must be rejected on save, even though nothing else about them collides.
    /// </summary>
    [Fact]
    public void ExchangeHoliday_SameExchangeAndDate_Rejected()
    {
        var (context, connection) = SecurityMasterContextFixture.CreateSqlite();
        try
        {
            var holidayDate = new DateOnly(2027, 7, 4);
            var first = new ExchangeHoliday(ExchangeCode.XNYS, holidayDate, "Independence Day A - Phase4ConstraintTests");
            var second = new ExchangeHoliday(ExchangeCode.XNYS, holidayDate, "Independence Day B - Phase4ConstraintTests");
            AssignSyntheticId(first, 9011);
            AssignSyntheticId(second, 9012);

            context.ExchangeHolidays.AddRange(first, second);
            var act = () => context.SaveChanges();

            act.Should().Throw<DbUpdateException>();
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies <c>FxRateConfiguration</c> indexes
    /// <c>(RateDate, BaseCurrencyCode, QuoteCurrencyCode)</c> uniquely — two rates for the same
    /// date/currency pair (with different, non-colliding <c>Rate</c> values) must be rejected.
    /// </summary>
    [Fact]
    public void FxRate_DuplicateRateDateBaseCurrencyQuoteCurrency_Rejected()
    {
        var (context, connection) = SecurityMasterContextFixture.CreateSqlite();
        try
        {
            var rateDate = new DateOnly(2027, 3, 15);
            var first = new FxRate(rateDate, Currency.USD, Currency.CAD, 1.30m);
            var second = new FxRate(rateDate, Currency.USD, Currency.CAD, 1.35m);
            AssignSyntheticId(first, 9021);
            AssignSyntheticId(second, 9022);

            context.FxRates.AddRange(first, second);
            var act = () => context.SaveChanges();

            act.Should().Throw<DbUpdateException>();
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies <c>AssetClassConfiguration</c> indexes <c>Description</c>
    /// uniquely. Every <see cref="AssetClassCode"/> enum member is already seeded (there is no
    /// unused Id to attach a genuinely new row to), so this forces the collision onto two already
    /// -seeded rows via an UPDATE rather than an INSERT: it mutates one seeded row's
    /// <c>Description</c> (private EF setter, reached the same way
    /// <c>SecurityMasterContextCrudTests.Security_RequiredName_Enforced</c> reaches
    /// <c>Security.Name</c>) to collide with a DIFFERENT seeded row's <c>Description</c>, then
    /// saves — proving the index fires on update, not just on insert.
    /// </summary>
    [Fact]
    public void AssetClass_DuplicateDescription_Rejected()
    {
        var (context, connection) = SecurityMasterContextFixture.CreateSqlite();
        try
        {
            var equities = context.AssetClasses.Single(a => a.Id == AssetClassCode.Equities);
            var fixedIncome = context.AssetClasses.Single(a => a.Id == AssetClassCode.FixedIncome);

            typeof(AssetClass).GetProperty(nameof(AssetClass.Description))!
                .SetValue(equities, fixedIncome.Description);

            var act = () => context.SaveChanges();

            act.Should().Throw<DbUpdateException>();
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies <c>ContinentConfiguration</c> indexes <c>Name</c> uniquely.
    /// Every <see cref="ContinentCode"/> enum member is already seeded, so — mirroring
    /// <see cref="AssetClass_DuplicateDescription_Rejected"/> — this mutates one seeded
    /// continent's <c>Name</c> (private EF setter) to collide with a DIFFERENT seeded continent's
    /// <c>Name</c>, then saves.
    /// </summary>
    [Fact]
    public void Continent_DuplicateName_Rejected()
    {
        var (context, connection) = SecurityMasterContextFixture.CreateSqlite();
        try
        {
            var africa = context.Continents.Single(c => c.Code == ContinentCode.AF);
            var antarctica = context.Continents.Single(c => c.Code == ContinentCode.AN);

            typeof(Continent).GetProperty(nameof(Continent.Name))!
                .SetValue(africa, antarctica.Name);

            var act = () => context.SaveChanges();

            act.Should().Throw<DbUpdateException>();
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies <c>CurrencyConfiguration</c> keys
    /// <see cref="CurrencyEntity"/> on <c>Code</c> — inserting a second currency row whose
    /// <c>Code</c> duplicates an already-seeded row's <c>Code</c> (<see cref="Currency.USD"/>)
    /// must be rejected by the primary key, even though its <c>NumericCode</c> and <c>Name</c>
    /// are otherwise unused (isolating the assertion to the PK specifically, not the sibling
    /// unique indexes on those two columns).
    /// </summary>
    [Fact]
    public void Currency_DuplicateCode_Rejected()
    {
        var (context, connection) = SecurityMasterContextFixture.CreateSqlite();
        try
        {
            var duplicate = new CurrencyEntity(Currency.USD, 9999, "Duplicate USD - Phase4ConstraintTests", "X");
            context.Currencies.Add(duplicate);

            var act = () => context.SaveChanges();

            act.Should().Throw<DbUpdateException>();
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }
}
