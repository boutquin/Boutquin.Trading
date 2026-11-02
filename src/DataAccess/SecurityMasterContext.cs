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

namespace Boutquin.Trading.DataAccess;

using System.Reflection;

using Entities;

/// <summary>
/// The <see cref="SecurityMasterContext"/> class represents the database context for the security master database. It provides a
/// set of <see cref="DbSet{TEntity}"/> properties that can be used to query and save instances of the entity classes.
/// </summary>
/// <summary>
/// Represents the database context for the security master.
/// </summary>    
public sealed class SecurityMasterContext : DbContext
{
    /// <summary>
    /// Asset classes tracked by the security master.
    /// </summary>
    public DbSet<AssetClass> AssetClasses { get; set; } = null!;

    /// <summary>
    /// Cities tracked by the security master.
    /// </summary>
    public DbSet<City> Cities { get; set; } = null!;

    /// <summary>
    /// Continents tracked by the security master.
    /// </summary>
    public DbSet<Continent> Continents { get; set; } = null!;

    /// <summary>
    /// Countries tracked by the security master.
    /// </summary>
    public DbSet<Country> Countries { get; set; } = null!;

    /// <summary>
    /// Currencies tracked by the security master.
    /// </summary>
    public DbSet<Currency> Currencies { get; set; } = null!;

    /// <summary>
    /// Exchanges tracked by the security master.
    /// </summary>
    public DbSet<Exchange> Exchanges { get; set; } = null!;

    /// <summary>
    /// Exchange holidays tracked by the security master.
    /// </summary>
    public DbSet<ExchangeHoliday> ExchangeHolidays { get; set; } = null!;

    /// <summary>
    /// Exchange schedules tracked by the security master.
    /// </summary>
    public DbSet<ExchangeSchedule> ExchangeSchedules { get; set; } = null!;

    /// <summary>
    /// Foreign-exchange rates tracked by the security master.
    /// </summary>
    public DbSet<FxRate> FxRates { get; set; } = null!;

    /// <summary>
    /// Securities tracked by the security master.
    /// </summary>
    public DbSet<Security> Securities { get; set; } = null!;

    /// <summary>
    /// Security prices tracked by the security master.
    /// </summary>
    public DbSet<SecurityPrice> SecurityPrices { get; set; } = null!;

    /// <summary>
    /// Security symbols tracked by the security master.
    /// </summary>
    public DbSet<SecuritySymbol> SecuritySymbols { get; set; } = null!;

    /// <summary>
    /// Symbol standards tracked by the security master.
    /// </summary>
    public DbSet<SymbolStandard> SymbolStandards { get; set; } = null!;

    /// <summary>
    /// Time zones tracked by the security master.
    /// </summary>
    public DbSet<TimeZone> TimeZones { get; set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityMasterContext"/> class.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    public SecurityMasterContext(DbContextOptions<SecurityMasterContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Configures the model that was discovered by convention from the entity types exposed in <see cref="DbSet{TEntity}"/> properties on your derived context.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context. Databases (and other extensions) typically define extension methods on this object that allow you to configure aspects of the model that are specific to a given database.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
