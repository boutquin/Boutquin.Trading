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
/// Configures the entity mapping for the <see cref="SecurityPrice"/> entity.
/// </summary>
public sealed class SecurityPriceConfiguration : IEntityTypeConfiguration<SecurityPrice>
{
    /// <summary>
    /// Configures the entity of type <see cref="SecurityPrice"/>.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<SecurityPrice> builder)
    {
        // Validate parameters
        Guard.AgainstNull(builder, nameof(builder));

        // Configure primary key
        builder.HasKey(SecurityPrice.SecurityPrice_Key_Name);

        // Configure TradeDate property with required constraint
        builder.Property(c => c.TradeDate)
            .IsRequired();

        // Configure OpenPrice property with required constraint
        builder.Property(c => c.OpenPrice)
            .IsRequired();

        // Configure HighPrice property with required constraint
        builder.Property(c => c.HighPrice)
            .IsRequired();

        // Configure LowPrice property with required constraint
        builder.Property(c => c.LowPrice)
            .IsRequired();

        // Configure ClosePrice property with required constraint
        builder.Property(c => c.ClosePrice)
            .IsRequired();

        // Configure Volume property with required constraint
        builder.Property(c => c.Volume)
            .IsRequired();

        // Configure Dividend property with required constraint
        builder.Property(c => c.Dividend)
            .IsRequired();

        // Configure Unique Index on TradeDate & SecurityId
        builder.HasIndex(c => new { c.TradeDate, c.SecurityId })
            .IsUnique();
    }
}
