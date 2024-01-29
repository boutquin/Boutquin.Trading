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
/// This class is responsible for defining the structure and constraints for the <see cref="ExchangeHoliday"/> entity in the database.
/// </summary>
public sealed class ExchangeHolidayConfiguration : IEntityTypeConfiguration<ExchangeHoliday>
{
    /// <summary>
    /// Configures the entity of type <see cref="ExchangeHoliday"/>.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<ExchangeHoliday> builder)
    {
        // Validate parameters
        Guard.AgainstNull(() => builder);

        // Configure primary key
        builder.HasKey(ExchangeHoliday.ExchangeHoliday_Key_Name);

        // Configure Id property with proper column name
        builder.Property(ExchangeHoliday.ExchangeHoliday_Key_Name)
            .HasColumnName(ColumnConstants.Default_Primary_Key_Name);

        // Configure ExchangeCode property with required constraint, max length, and enum conversion
        builder.Property(eh => eh.ExchangeCode)
            .IsRequired()
            .HasMaxLength(ColumnConstants.ExchangeHoliday_ExchangeCode_Length)
            .HasConversion<string>();

        // Configure HolidayDate property with required constraint and column type
        builder.Property(eh => eh.HolidayDate)            
            .HasConversion<DateOnlyConverter>()
            .HasColumnType("Date")
            .IsRequired();

        // Configure Description property with required constraint and max length
        builder.Property(eh => eh.Description)
            .IsRequired()
            .HasMaxLength(ColumnConstants.ExchangeHoliday_Description_Length);

        // Configure Unique Index on ExchangeCode & HolidayDate
        builder.HasIndex(eh => new { eh.ExchangeCode, eh.HolidayDate })
            .IsUnique();

        // Configure Unique Index on Description
        builder.HasIndex(eh => eh.Description)
            .IsUnique();
    }
}
