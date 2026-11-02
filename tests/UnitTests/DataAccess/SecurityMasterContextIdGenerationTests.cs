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

using Boutquin.Trading.DataAccess;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Regression tests proving that a database-generated integer primary key round-trips
/// back through an entity's public strongly-typed <c>.Id</c> accessor after
/// <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChanges()"/>.
/// </summary>
/// <remarks>
/// <para>
/// Guards against a sentinel-seeding defect: the entity constructors used to seed their
/// private <c>_id</c> backing field to <c>-1</c> (a non-CLR-default value). EF Core's
/// on-add value-generation heuristic treats a non-default key value as caller-provided,
/// so the store-generated key was never assigned or read back. Reading
/// <see cref="Security.Id"/> then routed the stale <c>-1</c> through
/// <see cref="SecurityId.Create(int)"/> — which rejects non-positive
/// ids — and threw <see cref="ArgumentOutOfRangeException"/>.
/// </para>
/// <para>
/// The fix leaves <c>_id</c> at its CLR default (<c>0</c>) so EF Core's value generation
/// assigns the database key. <see cref="Security"/> is the only one of the affected
/// entities that exposes a public strongly-typed <c>.Id</c>, so it is the entity under
/// test here; the same sentinel was removed from the other entities sharing the pattern.
/// </para>
/// <para>
/// The in-memory provider is sufficient: it generates integer keys for store-generated
/// keys on <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChanges()"/> exactly
/// when the key value equals the CLR-default sentinel — which is the behavior this test
/// pins down.
/// </para>
/// </remarks>
public sealed class SecurityMasterContextIdGenerationTests
{
    private static SecurityMasterContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<SecurityMasterContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new SecurityMasterContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void Security_SavedThroughContext_HasReadableDatabaseGeneratedId()
    {
        // Arrange — a freshly constructed Security (no id assigned by the caller), attached
        // to a seeded Exchange so the test exercises Security's own key generation rather
        // than colliding with the reference-data seed applied by EnsureCreated().
        using var context = CreateInMemoryContext();
        var exchange = context.Exchanges.First();
        var security = new Security("Test Security", AssetClassCode.Equities, exchange);

        // Act — persist; EF Core must assign the store-generated primary key.
        context.Securities.Add(security);
        context.SaveChanges();

        // Assert — the generated key round-trips through the public strongly-typed Id.
        // SecurityId.Create rejects non-positive ids, so a suppressed generation (the old
        // _id = -1 sentinel behavior) would throw here.
        var readId = () => security.Id;
        readId.Should().NotThrow<ArgumentOutOfRangeException>(
            "EF Core must assign the database-generated key so the public Id is readable");
        ((int)security.Id).Should().BePositive(
            "a persisted entity's database-generated key is a positive identity value");
    }
}
