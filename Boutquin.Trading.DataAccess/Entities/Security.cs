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
namespace Boutquin.Trading.DataAccess.Entities;

using Domain.ValueObjects;

/// <summary>
/// Represents a security.
/// </summary>
public sealed class Security
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Security"/> class.
    /// </summary>
    /// <param name="name">The name of the security.</param>
    /// <param name="assetClassCode">The identifier of the asset class.</param>
    /// <param name="exchange">The exchange where the security is traded.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="assetClassCode"/> is not defined in the <see cref="AssetClassCode"/> enumeration.
    /// </exception>
    public Security(
        string name,
        AssetClassCode assetClassCode,
        Exchange exchange
        )
    {
        // Validate parameters
        Guard.AgainstNullOrWhiteSpaceAndOverflow(() => name, 
            ColumnConstants.Security_Name_Length); // Throws ArgumentException for null or empty and ArgumentOutOfRangeException for overflow
        Guard.AgainstUndefinedEnumValue(() => assetClassCode); // Throws ArgumentOutOfRangeException
        Guard.AgainstNull(() => exchange); // Throws ArgumentNullException

        _id = -1;
        Name = name;
        AssetClassCode = assetClassCode;
        Exchange = exchange;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Security"/> class.
    /// </summary>
    public Security()
    {
    }

    /// <summary>
    /// The internal security ID value.
    /// </summary>
    private int _id;

    /// <summary>
    /// Gets the identifier of the security.
    /// </summary>
    public SecurityId Id => SecurityId.Create(_id);

    /// <summary>
    /// Gets the name of the security.
    /// </summary>
    public string Name { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the identifier of the asset class.
    /// </summary>
    public AssetClassCode AssetClassCode { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets ISO 10383 market identifier code of the exchange.
    /// </summary>
    public Exchange Exchange { get; private set; } // Setter is for EF

    /// <summary>
    /// Navigation property to the related SecuritySymbols.
    /// </summary>
    public ICollection<SecuritySymbol> SecuritySymbols { get; private set; } // Setter is for EF 
        = [];

    /// <summary>
    /// Navigation property to the related SecurityPrices.
    /// </summary>
    public ICollection<SecurityPrice> SecurityPrices { get; private set; } // Setter is for EF 
        = [];

    /// <summary>
    /// The name of the primary key column in the Security table.
    /// </summary>
    public const string Security_Key_Name = nameof(_id);
}
