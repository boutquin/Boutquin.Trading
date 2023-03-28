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

using Boutquin.Trading.Domain.Enums;

namespace Boutquin.Trading.Domain.Entities;

using System;
using Boutquin.Domain.Helpers;

/// <summary>
/// Represents a country.
/// </summary>
public sealed class Country
{
    /// <summary>
    /// Gets the code of the country.
    /// </summary>
    public CountryCode Code { get; }

    /// <summary>
    /// Gets the name of the country.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the numeric code of the country.
    /// </summary>
    public int NumericCode { get; }

    /// <summary>
    /// Gets the currency code of the country.
    /// </summary>
    public CurrencyCode CurrencyCode { get; }

    /// <summary>
    /// Gets the continent code of the country.
    /// </summary>
    public ContinentCode ContinentCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Country"/> class.
    /// </summary>
    /// <param name="code">The code of the country.</param>
    /// <param name="name">The name of the country.</param>
    /// <param name="numericCode">The numeric code of the country.</param>
    /// <param name="currencyCode">The currency code of the country.</param>
    /// <param name="continentCode">The continent code of the country.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="name"/> length is not within the valid range, or when <paramref name="code"/>, <paramref name="currencyCode"/>, or <paramref name="continentCode"/> are not defined in their respective enumerations.
    /// </exception>
    public Country(
        CountryCode code, 
        string name, 
        int numericCode, 
        CurrencyCode currencyCode, 
        ContinentCode continentCode)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(code, nameof(code));
        Guard.AgainstNullOrWhiteSpace(name, nameof(name), ColumnConstants.Country_Name_Length);
        Guard.AgainstNegativeOrZero(numericCode, nameof(numericCode));
        Guard.AgainstUndefinedEnumValue(currencyCode, nameof(currencyCode));
        Guard.AgainstUndefinedEnumValue(continentCode, nameof(continentCode));

        Code = code;
        Name = name;
        NumericCode = numericCode;
        CurrencyCode = currencyCode;
        ContinentCode = continentCode;
    }
}
