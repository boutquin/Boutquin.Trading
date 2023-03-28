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

using Boutquin.Domain.Helpers;
using Boutquin.Trading.Domain.Entities;
using Boutquin.Trading.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;


/// <summary>
/// Represents the configuration for the City entity.
/// This class is responsible for defining the structure and constraints for the City entity in the database.
/// </summary>
public class CityConfiguration : IEntityTypeConfiguration<City>
{
    /// <summary>
    /// Configures the entity of type City.
    /// </summary>
    /// <param name="builder">An API surface for configuring an entity type.</param>
    /// <exception cref="ArgumentNullException">Thrown when the builder is null.</exception>
    public void Configure(EntityTypeBuilder<City> builder)
    {
        // Validate parameters
        Guard.AgainstNull(builder, nameof(builder));

        // Configure primary key
        builder.HasKey(c => c.Id);

        // Configure Name property with required constraint and max length
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(ColumnConstants.City_Name_Length);

        // Configure TimeZoneCode property with required constraint, max length, and enum conversion
        builder.Property(c => c.TimeZoneCode)
            .IsRequired()
            .HasMaxLength(ColumnConstants.City_TimeZoneCode_Length)
            .HasConversion(
                tz => tz.ToString(),
                tz => (TimeZoneCode)Enum.Parse(typeof(TimeZoneCode), tz));

        // Configure CountryCode property with required constraint, max length, and enum conversion
        builder.Property(c => c.CountryCode)
            .IsRequired()
            .HasMaxLength(ColumnConstants.City_CountryCode_Length)
            .HasConversion(
                cc => cc.ToString(),
                cc => (CountryCode)Enum.Parse(typeof(CountryCode), cc));
    }
}
