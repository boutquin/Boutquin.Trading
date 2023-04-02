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
using Boutquin.Trading.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boutquin.Trading.DataAccess.Configuration;

/// <summary>
/// Configures the entity mapping for the <see cref="ExchangeHoliday"/> entity.
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
        Guard.AgainstNull(builder, nameof(builder));

        // Configure primary key
        builder.HasKey(ExchangeHoliday.ExchangeHoliday_Key_Name);

        // Configure ExchangeCode property with required constraint, max length, and enum conversion
        builder.Property(c => c.ExchangeCode)
            .IsRequired()
            .HasMaxLength(ColumnConstants.ExchangeHoliday_ExchangeCode_Length)
            .HasConversion(
                code => code.ToString(),
                code => (ExchangeCode)Enum.Parse(typeof(ExchangeCode), code));
    }
}
