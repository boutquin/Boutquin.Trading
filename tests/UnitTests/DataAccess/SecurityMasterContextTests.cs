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
/// Smoke tests proving <see cref="SecurityMasterContextFixture"/> yields a usable,
/// seeded <c>SecurityMasterContext</c> on BOTH providers the integration-test suite relies on:
/// the EF in-memory provider and SQLite. Each test constructs a context through the real
/// <c>SecurityMasterContext(DbContextOptions&lt;SecurityMasterContext&gt;)</c> constructor,
/// applies the entity configurations and seed data via <c>EnsureCreated()</c> (both inside the
/// fixture), and queries seeded reference data back out — the foundation the rest of this
/// project's EF integration tests build on.
/// </summary>
public sealed class SecurityMasterContextTests
{
    // Seed cardinalities applied by EnsureCreated via the entity configurations:
    //   ContinentConfiguration seeds the 7 continents (AF, AN, AS, EU, NA, OC, SA);
    //   AssetClassConfiguration seeds the 8 asset classes (CashAndCashEquivalents..Other).
    // A misconfigured provider, a missing EnsureCreated, or an unwired context would leave
    // these tables empty (count 0), so the >= assertions below are load-bearing: they read
    // the real seeded row counts off the live query path, not a constructed-in-test value.
    private const int MinSeededContinents = 7;
    private const int MinSeededAssetClasses = 8;

    /// <summary>
    /// Verifies the fixture's in-memory context is constructed, created, and seeded, and that
    /// seed data is queryable back out — proving the context is usable.
    /// </summary>
    [Fact]
    public void InMemoryProvider_YieldsUsableSeededContext()
    {
        using var context = SecurityMasterContextFixture.CreateInMemory();

        context.Continents.Count().Should().BeGreaterThanOrEqualTo(MinSeededContinents);
        context.AssetClasses.Count().Should().BeGreaterThanOrEqualTo(MinSeededAssetClasses);
    }

    /// <summary>
    /// Verifies the fixture's SQLite context is constructed, created, and seeded, and that
    /// seed data is queryable back out. The backing connection is kept open for the whole
    /// query (its <c>:memory:</c> database would vanish if closed early) and both the context
    /// and the connection are disposed in the correct order (context first, then connection).
    /// </summary>
    [Fact]
    public void SqliteProvider_YieldsUsableSeededContext()
    {
        var (context, connection) = SecurityMasterContextFixture.CreateSqlite();
        try
        {
            context.Continents.Count().Should().BeGreaterThanOrEqualTo(MinSeededContinents);
            context.AssetClasses.Count().Should().BeGreaterThanOrEqualTo(MinSeededAssetClasses);
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }
}
