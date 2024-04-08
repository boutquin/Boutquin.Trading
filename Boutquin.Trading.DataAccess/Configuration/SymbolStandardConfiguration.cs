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
/// This class is responsible for defining the structure and constraints for the <see cref="SymbolStandard"/> entity in the database.
/// </summary>
public sealed class SymbolStandardConfiguration : IEntityTypeConfiguration<SymbolStandard>
{
    /// <summary>
    /// Configures the entity mapping for the <see cref="SymbolStandard"/> entity.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<SymbolStandard> builder)
    {
        // Validate parameters
        Guard.AgainstNull(() => builder); // Throws ArgumentNullException

        // Configure the primary key
        builder.HasKey(ac => ac.Id);

        // Configure Id property with required constrain and enum conversion
        builder.Property(ac => ac.Id)
            .IsRequired()
            .HasConversion<int>();

        // Configure Description property with required constraint and max length
        builder.Property(ac => ac.Description)
            .IsRequired()
            .HasMaxLength(ColumnConstants.SymbolStandard_Description_Length);

        // Configure Unique Index on Description
        builder.HasIndex(ac => ac.Description)
            .IsUnique();

        // Seed the symbol standards table with the major symbol standards
        builder.HasData(
            new SymbolStandard(SecuritySymbolStandard.Cusip),
            new SymbolStandard(SecuritySymbolStandard.Isin),
            new SymbolStandard(SecuritySymbolStandard.Sedol),
            new SymbolStandard(SecuritySymbolStandard.Ric),
            new SymbolStandard(SecuritySymbolStandard.BloombergTicker));
    }
}
