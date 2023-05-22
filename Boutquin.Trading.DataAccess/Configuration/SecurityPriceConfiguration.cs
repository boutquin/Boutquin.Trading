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
/// This class is responsible for defining the structure and constraints for the <see cref="SecurityPrice"/> entity in the database.
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
        Guard.AgainstNull(() => builder);

        // Configure primary key
        builder.HasKey(SecurityPrice.SecurityPrice_Key_Name);

        // Configure Id property with proper column name
        builder.Property(SecurityPrice.SecurityPrice_Key_Name)
            .HasColumnName(ColumnConstants.Default_Primary_Key_Name);

        // Configure TradeDate property with required constraint and column type
        builder.Property(sp => sp.TradeDate)
            .HasConversion<DateOnlyConverter>()
            .HasColumnType("Date")
            .IsRequired();

        // Configure OpenPrice property with required constraint and precision
        builder.Property(sp => sp.OpenPrice)
            .IsRequired()
            .HasPrecision(ColumnConstants.SecurityPrice_Price_Precision, ColumnConstants.SecurityPrice_Price_Scale);

        // Configure HighPrice property with required constraint and precision
        builder.Property(sp => sp.HighPrice)
            .IsRequired()
            .HasPrecision(ColumnConstants.SecurityPrice_Price_Precision, ColumnConstants.SecurityPrice_Price_Scale);

        // Configure LowPrice property with required constraint and precision
        builder.Property(sp => sp.LowPrice)
            .IsRequired()
            .HasPrecision(ColumnConstants.SecurityPrice_Price_Precision, ColumnConstants.SecurityPrice_Price_Scale);

        // Configure ClosePrice property with required constraint and precision
        builder.Property(sp => sp.ClosePrice)
            .IsRequired()
            .HasPrecision(ColumnConstants.SecurityPrice_Price_Precision, ColumnConstants.SecurityPrice_Price_Scale);

        // Configure Volume property with required constraint
        builder.Property(sp => sp.Volume)
            .IsRequired();

        // Configure Dividend property with required constraint and precision
        builder.Property(sp => sp.Dividend)
            .IsRequired()
            .HasPrecision(ColumnConstants.SecurityPrice_Price_Precision, ColumnConstants.SecurityPrice_Price_Scale);

        // Configure Unique Index on TradeDate & SecurityId
        builder.HasIndex(sp => new { sp.TradeDate, sp.SecurityId })
            .IsUnique();
    }
}
