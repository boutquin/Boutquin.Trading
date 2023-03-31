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

namespace Boutquin.Trading.DataAccess.Configuration;

/// <summary>
/// This class is responsible for defining the structure and constraints for the <see cref="AssetClass"/> entity in the database.
/// </summary>
public sealed class AssetClassConfiguration : IEntityTypeConfiguration<AssetClass>
{
    /// <summary>
    /// Configures the entity mapping for the <see cref="City"/> entity.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<AssetClass> builder)
    {
        // Validate parameters
        Guard.AgainstNull(builder, nameof(builder));

        // Configure the primary key
        builder.HasKey(ac => ac.Code);

        // Configure properties
        builder.Property(ac => ac.Code)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(ColumnConstants.AssetClass_Code_Length);

        builder.Property(ac => ac.Name)
            .IsRequired()
            .HasMaxLength(ColumnConstants.AssetClass_Name_Length);

        builder.Property(ac => ac.Description)
            .HasMaxLength(ColumnConstants.AssetClass_Description_Length);
    }
}
