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

using Boutquin.Trading.Domain.Enums;

namespace Boutquin.Trading.Domain.Entities;
public sealed class Security
{
    /// <summary>
    /// Gets the identifier of the security.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the name of the security.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the market identifier code of the exchange.
    /// </summary>
    public ExchangeCode MarketIdentifierCode { get; }

    /// <summary>
    /// Gets the identifier of the asset class.
    /// </summary>
    public int AssetClassId { get; }

    /// <summary>
    /// Navigation property to the related SecuritySymbols.
    /// </summary>
    public ICollection<SecuritySymbol> SecuritySymbols { get; } 
        = new List<SecuritySymbol>();

    /// <summary>
    /// Initializes a new instance of the <see cref="Security"/> class.
    /// </summary>
    /// <param name="id">The identifier of the security.</param>
    /// <param name="name">The name of the security.</param>
    /// <param name="marketIdentifierCode">The market identifier code of the exchange.</param>
    /// <param name="assetClassId">The identifier of the asset class.</param>
    public Security(
        int id, 
        string name, 
        ExchangeCode marketIdentifierCode, 
        int assetClassId)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Id must be greater than 0.");
        }

        if (string.IsNullOrEmpty(name) || name.Length > ColumnConstants.Security_Name_Length)
        {
            throw new ArgumentException($"Name must be non-empty and less than {ColumnConstants.Security_Name_Length} characters.", nameof(name));
        }

        if (!Enum.IsDefined(typeof(ExchangeCode), marketIdentifierCode))
        {
            throw new ArgumentOutOfRangeException(nameof(marketIdentifierCode), "Market identifier code is not defined in the enumeration.");
        }

        if (assetClassId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(assetClassId), "Asset class id must be greater than 0.");
        }

        Id = id;
        Name = name;
        MarketIdentifierCode = marketIdentifierCode;
        AssetClassId = assetClassId;
    }
}
