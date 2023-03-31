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

using Boutquin.Domain.Helpers;
using Boutquin.Trading.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TimeZone = Boutquin.Trading.Domain.Entities.TimeZone;

namespace Boutquin.Trading.DataAccess.Configuration;

/// <summary>
/// Configures the entity mapping for the <see cref="TimeZone"/> entity.
/// </summary>
public sealed class TimeZoneConfiguration : IEntityTypeConfiguration<TimeZone>
{
    /// <summary>
    /// Configures the entity of type <see cref="TimeZone"/>.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<TimeZone> builder)
    {
        // Validate parameters
        Guard.AgainstNull(builder, nameof(builder));
        // Configure the primary key
        builder.HasKey(tz => tz.Code);

        // Configure properties
        builder.Property(tz => tz.Code)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(ColumnConstants.TimeZone_Code_Length);

        builder.Property(tz => tz.Name)
            .IsRequired()
            .HasMaxLength(ColumnConstants.TimeZone_Name_Length);

        builder.Property(tz => tz.TimeZoneOffset)
            .IsRequired()
            .HasMaxLength(ColumnConstants.TimeZone_TimeZoneOffset_Length);

        builder.Property(tz => tz.UsesDaylightSaving)
            .IsRequired();
    }
}
