// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System.Reflection;
using Boutquin.Trading.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using TimeZone = Boutquin.Trading.Domain.Entities.TimeZone;

namespace Boutquin.Trading.DataAccess
{
    /// <summary>
    /// The <see cref="SecurityMasterContext"/> class represents the database context for the security master database. It provides a
    /// set of <see cref="DbSet{TEntity}"/> properties that can be used to query and save instances of the entity classes.
    /// </summary>
    /// <summary>
    /// Represents the database context for the security master.
    /// </summary>    
    public class SecurityMasterContext : DbContext
    {
        /// <summary>
        /// Gets or sets the DbSet for the AssetClass entity.
        /// </summary>
        public DbSet<AssetClass> AssetClasses { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for the Continent entity.
        /// </summary>
        public DbSet<Continent> Continents { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for the Country entity.
        /// </summary>
        public DbSet<Country> Countries { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for the Currency entity.
        /// </summary>
        public DbSet<Currency> Currencies { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for the Exchange entity.
        /// </summary>
        public DbSet<Exchange> Exchanges { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for the ExchangeHoliday entity.
        /// </summary>
        public DbSet<ExchangeHoliday> ExchangeHolidays { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for the ExchangeSchedule entity.
        /// </summary>
        public DbSet<ExchangeSchedule> ExchangeSchedules { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for the FXRate entity.
        /// </summary>
        public DbSet<FxRate> FxRates { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for the Security entity.
        /// </summary>
        public DbSet<Security> Securities { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for the SecurityPrice entity.
        /// </summary>
        public DbSet<SecurityPrice> SecurityPrices { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for the SecuritySymbol entity.
        /// </summary>
        public DbSet<SecuritySymbol> SecuritySymbols { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for the TimeZone entity.
        /// </summary>
        public DbSet<TimeZone> TimeZones { get; set; }

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
}
