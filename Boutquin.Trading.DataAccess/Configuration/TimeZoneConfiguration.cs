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

using Boutquin.Domain.Extensions;

using TimeZone = Domain.Entities.TimeZone;

/// <summary>
/// This class is responsible for defining the structure and constraints for the <see cref="Domain.Entities.TimeZone"/> entity in the database.
/// </summary>
public sealed class TimeZoneConfiguration : IEntityTypeConfiguration<TimeZone>
{
    /// <summary>
    /// Configures the entity mapping for the <see cref="TimeZone"/> entity.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<TimeZone> builder)
    {
        // Validate parameters
        Guard.AgainstNull(() => builder);
        
        // Configure the primary key
        builder.HasKey(tz => tz.Code);

        // Configure Code property with required constraint, max length, and enum conversion
        builder.Property(tz => tz.Code)
            .IsRequired()
            .HasMaxLength(ColumnConstants.TimeZone_Code_Length)
            .HasConversion<string>();


        // Configure Name property with required constraint and max length
        builder.Property(tz => tz.Name)
            .IsRequired()
            .HasMaxLength(ColumnConstants.TimeZone_Name_Length);

        // Configure TimeZoneOffset property with required constraint and max length
        builder.Property(tz => tz.TimeZoneOffset)
            .IsRequired()
            .HasMaxLength(ColumnConstants.TimeZone_TimeZoneOffset_Length);

        // Configure UsesDaylightSaving property with required constraint and max length
        builder.Property(tz => tz.UsesDaylightSaving)
            .IsRequired();

        // Configure Unique Index on Name
        builder.HasIndex(c => c.Name)
            .IsUnique();

        // Seed the TimeZone table with the values from TimeZoneCode enum
        builder.HasData(
            CreateTimeZone(TimeZoneCode.UTC, "Coordinated Universal Time", false),
            CreateTimeZone(TimeZoneCode.CET, "Central European Time", false),
            CreateTimeZone(TimeZoneCode.GMT, "Greenwich Mean Time", false),
            CreateTimeZone(TimeZoneCode.EST, "Eastern Standard Time", false),
            CreateTimeZone(TimeZoneCode.CST, "China Standard Time", false),
            CreateTimeZone(TimeZoneCode.JST, "Japan Standard Time", false),
            CreateTimeZone(TimeZoneCode.HKT, "Hong Kong Time", false),
            CreateTimeZone(TimeZoneCode.MSK, "Moscow Standard Time", false),
            CreateTimeZone(TimeZoneCode.AEST, "Australian Eastern Standard Time", false)
        );
    }

    /// <summary>
    /// Creates a <see cref="TimeZone"/> instance based on the given <see cref="TimeZoneCode"/>, name, and usesDaylightSaving flag.
    /// </summary>
    /// <param name="code">The <see cref="TimeZoneCode"/> enumeration value.</param>
    /// <param name="name">The name of the time zone.</param>
    /// <param name="usesDaylightSaving">A boolean flag indicating if the time zone uses daylight saving.</param>
    /// <returns>A <see cref="TimeZone"/> instance with the given values.</returns>
    private static TimeZone CreateTimeZone(TimeZoneCode code, string name, bool usesDaylightSaving)
    {
        var timeZoneOffset = code.GetDescription();
        return new TimeZone(code, name, timeZoneOffset, usesDaylightSaving);
    }
}
