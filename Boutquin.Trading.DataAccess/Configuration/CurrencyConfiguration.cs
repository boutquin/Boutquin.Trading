﻿// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
/// This class is responsible for defining the structure and constraints for the <see cref="Currency"/> entity in the database.
/// </summary>
public sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    /// <summary>
    /// Configures the entity mapping for the <see cref="Currency"/> entity.
    /// </summary>
    /// <param name="builder">The builder to be used for configuring the entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        // Validate parameters
        Guard.AgainstNull(() => builder); // Throws ArgumentNullException

        // Configure the primary key
        builder.HasKey(c => c.Code);

        // Configure Code property with required constraint, max length, and enum conversion
        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Currency_Code_Length)
            .HasConversion<string>();

        // Configure NumericCode property with required constraint
        builder.Property(c => c.NumericCode)
            .IsRequired();

        // Configure Name property with required constraint and max length
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Currency_Name_Length);

        // Configure Symbol property with required constraint and max length
        builder.Property(c => c.Symbol)
            .IsRequired()
            .HasMaxLength(ColumnConstants.Currency_Symbol_Length);

        // Configure Unique Index on Name
        builder.HasIndex(c => c.Name)
            .IsUnique();

        // Configure Unique Index on NumericCode
        builder.HasIndex(c => c.NumericCode)
            .IsUnique();

        // Seed the currencies table with the major currencies
        builder.HasData(
            new Currency(CurrencyCode.USD, 840, "United States dollar", "$"),
            new Currency(CurrencyCode.CAD, 124, "Canadian dollar", "$"),
            new Currency(CurrencyCode.MXN, 484, "Mexican peso", "$"),
            new Currency(CurrencyCode.GBP, 826, "British pound", "£"),
            new Currency(CurrencyCode.EUR, 978, "Euro", "€"),
            new Currency(CurrencyCode.JPY, 392, "Japanese yen", "¥"),
            new Currency(CurrencyCode.CNY, 156, "Chinese yuan", "¥"),
            new Currency(CurrencyCode.INR, 356, "Indian rupee", "₹"),
            new Currency(CurrencyCode.AUD, 36, "Australian dollar", "$"),
            new Currency(CurrencyCode.BRL, 986, "Brazilian real", "R$"),
            new Currency(CurrencyCode.RUB, 643, "Russian ruble", "₽"),
            new Currency(CurrencyCode.KRW, 410, "South Korean won", "₩"),
            new Currency(CurrencyCode.HKD, 344, "Hong Kong dollar", "HK$")
        );
    }
}
