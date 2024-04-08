// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.DataAccess.Configuration;

using Entities;

/// <summary>
/// This class is responsible for defining the structure and constraints for the <see cref="Exchange"/> entity in the database.
/// </summary>
public sealed class ExchangeConfiguration : IEntityTypeConfiguration<Exchange>
{
    /// <summary>
    /// Configures the entity of type <see cref="Exchange"/>.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<Exchange> builder)
    {
        // Validate parameters
        Guard.AgainstNull(() => builder); // Throws ArgumentNullException

        // Configure the primary key
        builder.HasKey(e => e.Code);

        // Configure Code property with required constraint, max length, and enum conversion
        builder.Property(e => e.Code)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Exchange_Code_Length)
            .HasConversion<string>();

        // Configure Name property with required constraint and max length
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Exchange_Name_Length);

        // Configure navigation for City property
        builder
            .HasOne(e => e.City)
            .WithMany()
            .HasForeignKey("CityId")
            .IsRequired();

        // Configure navigation for ExchangeSchedules collection
        builder
            .HasMany(e => e.ExchangeSchedules)
            .WithOne()
            .HasForeignKey(x => x.ExchangeCode);

        // Configure navigation for ExchangeHolidays collection
        builder
            .HasMany(e => e.ExchangeHolidays)
            .WithOne()
            .HasForeignKey(x => x.ExchangeCode);

        // Configure Unique Index on Name
        builder.HasIndex(e => e.Name)
            .IsUnique();

        // Seed the Exchange table using the City IDs as foreign keys
        builder.HasData(
            new { Code = ExchangeCode.XNYS, Name = "New York Stock Exchange", CityId = IdLookup.GetCityId("New York") },
            new { Code = ExchangeCode.XNAS, Name = "NASDAQ Stock Market", CityId = IdLookup.GetCityId("New York") },
            new { Code = ExchangeCode.XTSE, Name = "Tokyo Stock Exchange", CityId = IdLookup.GetCityId("Tokyo") },
            new { Code = ExchangeCode.XSHG, Name = "Shanghai Stock Exchange", CityId = IdLookup.GetCityId("Shanghai") },
            new { Code = ExchangeCode.XHKG, Name = "Hong Kong Stock Exchange", CityId = IdLookup.GetCityId("Hong Kong") },
            new { Code = ExchangeCode.XPAR, Name = "Euronext Paris", CityId = IdLookup.GetCityId("Paris") },
            new { Code = ExchangeCode.XLON, Name = "London Stock Exchange", CityId = IdLookup.GetCityId("London") },
            new { Code = ExchangeCode.XETR, Name = "Deutsche Boerse XETRA", CityId = IdLookup.GetCityId("Frankfurt") },
            new { Code = ExchangeCode.XMOS, Name = "Moscow Exchange", CityId = IdLookup.GetCityId("Moscow") },
            new { Code = ExchangeCode.XTOR, Name = "Toronto Stock Exchange", CityId = IdLookup.GetCityId("Toronto") }
        );
    }
}
