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
/// Configures the entity mapping for the <see cref="Security"/> entity.
/// </summary>
public sealed class SecurityConfiguration : IEntityTypeConfiguration<Security>
{
    /// <summary>
    /// Configures the entity of type <see cref="Security"/>.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<Security> builder)
    {
        // Validate parameters
        Guard.AgainstNull(builder, nameof(builder));

        // Configure primary key
        builder.HasKey(Security.Security_Key_Name);

        // Configure Name property with required constraint and max length
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Security_Name_Length);

        // Configure ExchangeCode property with required constraint, max length, and enum conversion
        builder.Property(s => s.ExchangeCode)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Security_ExchangeCode_Length)
            .HasConversion<string>();

        // Configure AssetClassCode property with required constraint, max length, and enum conversion
        builder.Property(s => s.AssetClassCode)
            .IsRequired().HasMaxLength(ColumnConstants.Security_AssetClassCode_Length)
            .HasConversion<string>();

        // Configure Unique Index on Name
        builder.HasIndex(c => c.Name)
            .IsUnique();
    }
}
