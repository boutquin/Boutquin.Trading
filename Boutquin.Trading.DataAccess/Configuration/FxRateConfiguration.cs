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
using Boutquin.Trading.Domain.Enums;

namespace Boutquin.Trading.DataAccess.Configuration;

/// <summary>
/// Configures the entity mapping for the <see cref="FxRate"/> entity.
/// </summary>
public sealed class FxRateConfiguration : IEntityTypeConfiguration<FxRate>
{
    /// <summary>
    /// Configures the entity of type <see cref="FxRate"/>.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<FxRate> builder)
    {
        // Validate parameters
        Guard.AgainstNull(builder, nameof(builder));

        // Configure primary key
        builder.HasKey(FxRate.FxRate_Key_Name);

        // Configure RateDate property with required constraint
        builder.Property(c => c.RateDate)
            .IsRequired();

        // Configure BaseCurrencyCode property with required constraint, max length, and enum conversion
        builder.Property(c => c.BaseCurrencyCode)
            .IsRequired()
            .HasMaxLength(ColumnConstants.FxRate_BaseCurrencyCode_Length)
            .HasConversion(
                code => code.ToString(),
                code => (CurrencyCode)Enum.Parse(typeof(CurrencyCode), code));

        // Configure QuoteCurrencyCode property with required constraint, max length, and enum conversion
        builder.Property(c => c.QuoteCurrencyCode)
            .IsRequired()
            .HasMaxLength(ColumnConstants.FxRate_QuoteCurrencyCode_Length)
            .HasConversion(
                code => code.ToString(),
                code => (CurrencyCode)Enum.Parse(typeof(CurrencyCode), code));

        // Configure RateDate property with required constraint and scale & precision
        builder.Property(c => c.Rate)
            .IsRequired()
            .HasPrecision(ColumnConstants.SecurityPrice_Price_Precision, ColumnConstants.SecurityPrice_Price_Scale);

        // Configure Unique Index on RateDate, BaseCurrencyCode & QuoteCurrencyCode
        builder.HasIndex(c => new { c.RateDate, c.BaseCurrencyCode, c.QuoteCurrencyCode })
            .IsUnique();
    }
}