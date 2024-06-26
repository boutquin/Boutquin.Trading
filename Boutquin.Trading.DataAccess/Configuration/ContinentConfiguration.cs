﻿// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
/// This class is responsible for defining the structure and constraints for the <see cref="Continent"/> entity in the database.
/// </summary>
public sealed class ContinentConfiguration : IEntityTypeConfiguration<Continent>
{
    /// <summary>
    /// Configures the entity of type <see cref="Continent"/>.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<Continent> builder)
    {
        // Validate parameters
        Guard.AgainstNull(() => builder); // Throws ArgumentNullException

        // Configure the primary key
        builder.HasKey(c => c.Code);

        // Configure Code property with required constraint, max length, and enum conversion
        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Continent_Code_Length)
            .HasConversion<string>();

        // Configure Name property with required constraint and max length
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Continent_Name_Length);

        // Configure Unique Index on Name
        builder.HasIndex(c => c.Name)
            .IsUnique();

        // Seed the currencies table with the major currencies
        builder.HasData(
            new Continent(ContinentCode.AF),
            new Continent(ContinentCode.AN),
            new Continent(ContinentCode.AS),
            new Continent(ContinentCode.EU),
            new Continent(ContinentCode.NA),
            new Continent(ContinentCode.OC),
            new Continent(ContinentCode.SA));
    }
}
