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

/// <summary>
/// Represents a currency.
/// </summary>
public sealed class Currency
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Currency"/> class.
    /// </summary>
    /// <param name="code">The code of the currency.</param>
    /// <param name="numericCode">The numeric code of the currency.</param>
    /// <param name="name">The name of the currency.</param>
    /// <param name="symbol">The symbol of the currency.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="symbol"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="name"/> length, <paramref name="symbol"/> length, or <paramref name="numericCode"/> is not within the valid range, or when <paramref name="code"/> is not defined in the enumeration.
    /// </exception>
    public Currency(
        CurrencyCode code,
        int numericCode,
        string name,
        string symbol)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(code, nameof(code));
        Guard.AgainstNegative(numericCode, nameof(numericCode)); // TODO: OrZero???
        Guard.AgainstNullOrWhiteSpaceAndOverflow(name, nameof(name), ColumnConstants.Currency_Name_Length);
        Guard.AgainstNullOrWhiteSpaceAndOverflow(symbol, nameof(symbol), ColumnConstants.Currency_Symbol_Length);

        Code = code;
        NumericCode = numericCode;
        Name = name;
        Symbol = symbol;
    }

    /// <summary>
    /// Gets the ISO 4217 currency code.
    /// </summary>
    public CurrencyCode Code { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the ISO 4217 currency numeric code.
    /// </summary>
    public int NumericCode { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the currency name.
    /// </summary>
    public string Name { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the currency symbol.
    /// </summary>
    public string Symbol { get; private set; } // Setter is for EF
}
