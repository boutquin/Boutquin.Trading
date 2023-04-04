﻿// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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

using Boutquin.Domain.Helpers;
using Boutquin.Trading.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Boutquin.Trading.DataAccess.Configuration;

/// <summary>
/// This class is responsible for defining the structure and constraints for the <see cref="City"/> entity in the database.
/// </summary>
public sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    /// <summary>
    /// Configures the entity mapping for the <see cref="City"/> entity.
    /// </summary>
    /// <param name="builder">An API surface for configuring an entity type.</param>
    /// <exception cref="ArgumentNullException">Thrown when the builder is null.</exception>
    public void Configure(EntityTypeBuilder<City> builder)
    {
        // Validate parameters
        Guard.AgainstNull(builder, nameof(builder));

        // Configure primary key
        builder.HasKey(City.City_Key_Name);

        // Configure Id property with proper column name
        builder.Property(City.City_Key_Name)
            .HasColumnName(ColumnConstants.Default_Primary_Key_Name);

        // Configure Name property with required constraint and max length
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(ColumnConstants.City_Name_Length);

        // Configure TimeZoneCode property with required constraint, max length, and enum conversion
        builder.Property(c => c.TimeZoneCode)
            .IsRequired()
            .HasMaxLength(ColumnConstants.City_TimeZoneCode_Length)
            .HasConversion<string>();

        // Configure CountryCode property with required constraint, max length, and enum conversion
        builder.Property(c => c.CountryCode)
            .IsRequired()
            .HasMaxLength(ColumnConstants.City_CountryCode_Length)
            .HasConversion<string>();

        // Configure Unique Index on CountryCode & Name
        builder.HasIndex(c => new { c.CountryCode, c.Name })
            .IsUnique();
    }
}
