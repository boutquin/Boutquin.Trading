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
namespace Boutquin.Trading.DataAccess.Configuration;

using Entities;

/// <summary>
/// This class is responsible for defining the structure and constraints for the <see cref="Country"/> entity in the database.
/// </summary>
public sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    /// <summary>
    /// Configures the entity of type <see cref="Country"/>.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        // Validate parameters
        Guard.AgainstNull(() => builder);

        // Configure the primary key
        builder.HasKey(c => c.Code);

        // Configure Code property with required constraint, max length, and enum conversion
        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Country_Code_Length)
            .HasConversion<string>();

        // Configure Name property with required constraint and max length
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Country_Name_Length);

        // Configure NumericCode property with required constraint
        builder.Property(c => c.NumericCode)
            .IsRequired();

        // Configure CurrencyCode property with required constraint, max length, and enum conversion
        builder.Property(c => c.CurrencyCode)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Country_CurrencyCode_Length)
            .HasConversion<string>();

        // Configure ContinentCode property with required constraint, max length, and enum conversion
        builder.Property(c => c.ContinentCode)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Country_ContinentCode_Length)
            .HasConversion<string>();

        // Configure CurrencyCode navigation property
        builder.HasOne<Currency>()
            .WithMany()
            .HasForeignKey(c => c.CurrencyCode)
            .IsRequired();

        // Configure ContinentCode navigation property
        builder.HasOne<Continent>()
            .WithMany()
            .HasForeignKey(c => c.ContinentCode)
            .IsRequired();

        // Configure Unique Index on Name
        builder.HasIndex(c => c.Name)
            .IsUnique();

        // Seed the Countries table with some sample data
        builder.HasData(
            new Country(CountryCode.CA, "Canada", 124, CurrencyCode.CAD, ContinentCode.NA),
            new Country(CountryCode.CN, "China", 156, CurrencyCode.CNY, ContinentCode.AS),
            new Country(CountryCode.FR, "France", 250, CurrencyCode.EUR, ContinentCode.EU),
            new Country(CountryCode.DE, "Germany", 276, CurrencyCode.EUR, ContinentCode.EU),
            new Country(CountryCode.HK, "Hong Kong", 344, CurrencyCode.HKD, ContinentCode.AS),
            new Country(CountryCode.IN, "India", 356, CurrencyCode.INR, ContinentCode.AS),
            new Country(CountryCode.JP, "Japan", 392, CurrencyCode.JPY, ContinentCode.AS),
            new Country(CountryCode.RU, "Russia", 643, CurrencyCode.RUB, ContinentCode.EU),
            new Country(CountryCode.KR, "South Korea", 410, CurrencyCode.KRW, ContinentCode.AS),
            new Country(CountryCode.GB, "United Kingdom", 826, CurrencyCode.GBP, ContinentCode.EU),
            new Country(CountryCode.US, "United States", 840, CurrencyCode.USD, ContinentCode.NA)
        );

    }
}
