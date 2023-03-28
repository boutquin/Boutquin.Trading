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
    public ExchangeCode ExchangeCode { get; }

    /// <summary>
    /// Gets the identifier of the asset class.
    /// </summary>
    public AssetClassCode AssetClassCode { get; }

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
    /// <param name="exchangeCode">The market identifier code of the exchange.</param>
    /// <param name="assetClassCode">The identifier of the asset class.</param>
    public Security(
        int id, 
        string name, 
        ExchangeCode exchangeCode,
        AssetClassCode assetClassCode)
    {
        // Validate parameters
        Guard.AgainstNegativeOrZero(id, nameof(id));
        Guard.AgainstNullOrWhiteSpace(name, nameof(name), ColumnConstants.Security_Name_Length);
        Guard.AgainstUndefinedEnumValue(exchangeCode, nameof(exchangeCode));
        Guard.AgainstUndefinedEnumValue(assetClassCode, nameof(assetClassCode));

        Id = id;
        Name = name;
        ExchangeCode = exchangeCode;
        AssetClassCode = assetClassCode;
    }
}
