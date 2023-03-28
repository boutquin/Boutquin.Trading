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

public sealed class SecuritySymbol
{
    /// <summary>
    /// Gets the identifier of the security symbol.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the identifier of the security.
    /// </summary>
    public int SecurityId { get; }

    /// <summary>
    /// Gets the symbol of the security.
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Gets the security symbol standard.
    /// </summary>
    public SecuritySymbolStandard Standard { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecuritySymbol"/> class.
    /// </summary>
    /// <param name="id">The identifier of the security symbol.</param>
    /// <param name="securityId">The identifier of the security.</param>
    /// <param name="symbol">The symbol of the security.</param>
    /// <param name="standard">The security symbol standard.</param>
    public SecuritySymbol(
        int id, 
        int securityId, 
        string symbol, 
        SecuritySymbolStandard standard)
    {
        // Validate parameters
        Guard.AgainstNegativeOrZero(id, nameof(id));
        Guard.AgainstNegativeOrZero(securityId, nameof(securityId));
        Guard.AgainstNullOrWhiteSpace(symbol, nameof(symbol), ColumnConstants.SecuritySymbol_Symbol_Length);
        Guard.AgainstUndefinedEnumValue(standard, nameof(standard));

        Id = id;
        SecurityId = securityId;
        Symbol = symbol;
        Standard = standard;
    }
}
