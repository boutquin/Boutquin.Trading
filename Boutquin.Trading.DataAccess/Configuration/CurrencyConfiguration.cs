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

public sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    /// <summary>
    /// Configures the entity of type <see cref="Currency"/>.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        // Validate parameters
        Guard.AgainstNull(builder, nameof(builder));

        // Configure the primary key
        builder.HasKey(c => c.Code);

        // Configure properties
        builder.Property(c => c.Code)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(ColumnConstants.Currency_Code_Length);

        builder.Property(c => c.NumericCode)
            .IsRequired();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Currency_Name_Length);

        builder.Property(c => c.Symbol)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Currency_Symbol_Length);
    }
}