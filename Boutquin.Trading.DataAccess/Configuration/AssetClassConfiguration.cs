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
namespace Boutquin.Trading.DataAccess.Configuration;

using Entities;

/// <summary>
/// This class is responsible for defining the structure and constraints for the <see cref="AssetClass"/> entity in the database.
/// </summary>
public sealed class AssetClassConfiguration : IEntityTypeConfiguration<AssetClass>
{
    /// <summary>
    /// Configures the entity mapping for the <see cref="AssetClass"/> entity.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<AssetClass> builder)
    {
        // Validate parameters
        Guard.AgainstNull(() => builder);

        // Configure the primary key
        builder.HasKey(ac => ac.Id);

        // Configure Id property with required constrain and enum conversion
        builder.Property(ac => ac.Id)
            .IsRequired()
            .HasConversion<int>();

        // Configure Description property with required constraint and max length
        builder.Property(ac => ac.Description)
            .IsRequired()
            .HasMaxLength(ColumnConstants.AssetClass_Description_Length);

        // Configure Unique Index on Description
        builder.HasIndex(ac => ac.Description)
            .IsUnique();

        // Seed the asset class table with the major asset classes
        builder.HasData(
            new AssetClass(AssetClassCode.CashAndCashEquivalents),
            new AssetClass(AssetClassCode.FixedIncome),
            new AssetClass(AssetClassCode.Equities),
            new AssetClass(AssetClassCode.RealEstate),    
            new AssetClass(AssetClassCode.Commodities),
            new AssetClass(AssetClassCode.Alternatives),
            new AssetClass(AssetClassCode.CryptoCurrencies),
            new AssetClass(AssetClassCode.Other));
    }
}
