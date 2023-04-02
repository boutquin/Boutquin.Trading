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
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.ValueObjects;

namespace Boutquin.Trading.Domain.Entities;

/// <summary>
/// Represents a security.
/// </summary>
public sealed class Security
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Security"/> class.
    /// </summary>
    /// <param name="name">The name of the security.</param>
    /// <param name="exchangeCode">The market identifier code of the exchange.</param>
    /// <param name="assetClassCode">The identifier of the asset class.</param>
    public Security(
        string name,
        ExchangeCode exchangeCode,
        AssetClassCode assetClassCode)
    {
        // Validate parameters
        Guard.AgainstNullOrWhiteSpaceAndOverflow(name, nameof(name), ColumnConstants.Security_Name_Length);
        Guard.AgainstUndefinedEnumValue(exchangeCode, nameof(exchangeCode));
        Guard.AgainstUndefinedEnumValue(assetClassCode, nameof(assetClassCode));

        Name = name;
        ExchangeCode = exchangeCode;
        AssetClassCode = assetClassCode;
    }

    /// <summary>
    /// The internal security ID value.
    /// </summary>
    private int? _id;

    /// <summary>
    /// Gets the identifier of the security.
    /// </summary>
    public SecurityId Id => SecurityId.Create((int)_id);

    /// <summary>
    /// Gets the name of the security.
    /// </summary>
    public string Name { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the market identifier code of the exchange.
    /// </summary>
    public ExchangeCode ExchangeCode { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the identifier of the asset class.
    /// </summary>
    public AssetClassCode AssetClassCode { get; private set; } // Setter is for EF

    /// <summary>
    /// Navigation property to the related SecuritySymbols.
    /// </summary>
    public ICollection<SecuritySymbol> SecuritySymbols { get; private set; } // Setter is for EF 
        = new List<SecuritySymbol>();

    /// <summary>
    /// The name of the primary key column in the Security table.
    /// </summary>
    public const string Security_Key_Name = nameof(Security._id);
}
