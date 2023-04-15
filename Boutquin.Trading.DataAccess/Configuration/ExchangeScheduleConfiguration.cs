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

namespace Boutquin.Trading.DataAccess.Configuration;

/// <summary>
/// This class is responsible for defining the structure and constraints for the <see cref="ExchangeSchedule"/> entity in the database.
/// </summary>
public sealed class ExchangeScheduleConfiguration : IEntityTypeConfiguration<ExchangeSchedule>
{
    /// <summary>
    /// Configures the entity of type <see cref="ExchangeSchedule"/>.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<ExchangeSchedule> builder)
    {
        // Validate parameters
        Guard.AgainstNull(() => builder);

        // Configure primary key
        builder.HasKey(ExchangeSchedule.ExchangeSchedule_Key_Name);

        // Configure Id property with proper column name
        builder.Property(ExchangeSchedule.ExchangeSchedule_Key_Name)
            .HasColumnName(ColumnConstants.Default_Primary_Key_Name);

        // Configure ExchangeCode property with required constraint, max length, and enum conversion
        builder.Property(es => es.ExchangeCode)
            .IsRequired()
            .HasMaxLength(ColumnConstants.ExchangeSchedule_ExchangeCode_Length)
            .HasConversion<string>();

        // Configure DayOfWeek  property with required constraint
        builder.Property(es => es.DayOfWeek)
            .IsRequired();

        // Configure CloseTime property with required constraint
        builder.Property(es => es.CloseTime)
            .IsRequired();

        // Configure OpenTime property with required constraint
        builder.Property(es => es.OpenTime)
            .IsRequired();

        // Configure Unique Index on ExchangeCode & DayOfWeek
        builder.HasIndex(es => new { es.ExchangeCode, es.DayOfWeek })
            .IsUnique();
    }
}
