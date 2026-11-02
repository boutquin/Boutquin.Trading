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

using Boutquin.Trading.DataAccess;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Boutquin.Trading.Tests.UnitTests.DataAccess;

/// <summary>
/// Factory for producing seeded <see cref="SecurityMasterContext"/> instances against the
/// two providers the EF integration tests use: the EF in-memory provider (entity shapes,
/// seed data, navigation properties) and SQLite (index and unique-constraint enforcement,
/// which the in-memory provider ignores).
/// </summary>
public static class SecurityMasterContextFixture
{
    /// <summary>
    /// Creates a <see cref="SecurityMasterContext"/> backed by a uniquely-named EF in-memory
    /// database, with seed data applied via <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreated()"/>.
    /// The caller owns disposal of the returned context.
    /// </summary>
    /// <returns>A ready-to-query, seeded in-memory context.</returns>
    public static SecurityMasterContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<SecurityMasterContext>()
            // A fresh, uniquely-named store per call keeps every test isolated — no test
            // depends on another's writes or on execution order.
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new SecurityMasterContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Creates a <see cref="SecurityMasterContext"/> backed by a private SQLite
    /// <c>:memory:</c> database, with seed data applied via
    /// <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreated()"/>.
    /// </summary>
    /// <remarks>
    /// A SQLite <c>:memory:</c> database exists only while its owning <see cref="SqliteConnection"/>
    /// is open — closing the connection destroys the database. The connection must therefore
    /// outlive the context, so it is returned to the caller, who is responsible for disposing
    /// BOTH the context and the connection (dispose the context first, then the connection).
    /// </remarks>
    /// <returns>A tuple of the seeded context and its still-open backing connection.</returns>
    public static (SecurityMasterContext Context, SqliteConnection Connection) CreateSqlite()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        try
        {
            var options = new DbContextOptionsBuilder<SecurityMasterContext>()
                .UseSqlite(connection)
                .Options;
            var context = new SecurityMasterContext(options);
            context.Database.EnsureCreated();
            return (context, connection);
        }
        catch
        {
            // If context construction or schema creation fails, the connection we opened
            // above would otherwise leak until finalization — dispose it before propagating.
            connection.Dispose();
            throw;
        }
    }
}
