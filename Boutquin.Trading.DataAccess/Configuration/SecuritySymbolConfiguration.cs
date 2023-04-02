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
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Boutquin.Trading.Domain.Enums;

namespace Boutquin.Trading.DataAccess.Configuration;


/// <summary>
/// Configures the entity mapping for the <see cref="SecuritySymbol"/> entity.
/// </summary>
public sealed class SecuritySymbolConfiguration : IEntityTypeConfiguration<SecuritySymbol>
{
    /// <summary>
    /// Configures the entity of type <see cref="SecuritySymbol"/>.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<SecuritySymbol> builder)
    {
        // Validate parameters
        Guard.AgainstNull(builder, nameof(builder));

        // Configure SecurityId  property with required constraint
        builder.Property(c => c.SecurityId)
            .IsRequired();

        // Configure primary key
        builder.HasKey(SecuritySymbol.SecuritySymbol_Key_Name);

        // Configure Symbol property with required constraint and max length
        builder.Property(c => c.Symbol)
            .IsRequired()
            .HasMaxLength(ColumnConstants.SecuritySymbol_Symbol_Length);

        // Configure Standard property with required constraint, max length, and enum conversion
        builder.Property(c => c.Standard)
            .IsRequired()
            .HasMaxLength(ColumnConstants.SecuritySymbol_Standard_Length)
            .HasConversion<string>();

        // Configure Unique Index on SecurityId & Standard
        builder.HasIndex(c => new { c.SecurityId, c.Standard })
            .IsUnique();
    }
}
