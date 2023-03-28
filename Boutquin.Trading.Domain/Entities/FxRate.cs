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
using System.Xml.Linq;
using Boutquin.Trading.Domain.Enums;

namespace Boutquin.Trading.Domain.Entities;

/// <summary>
/// Represents a foreign exchange rate.
/// </summary>
public sealed class FxRate
{
    /// <summary>
    /// Gets the foreign exchange rate identifier.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the base currency code.
    /// </summary>
    public CurrencyCode BaseCurrencyCode { get; }

    /// <summary>
    /// Gets the quote currency code.
    /// </summary>
    public CurrencyCode QuoteCurrencyCode { get; }

    /// <summary>
    /// Gets the exchange rate value.
    /// </summary>
    public decimal Rate { get; }

    /// <summary>
    /// Gets the rate date.
    /// </summary>
    public DateTime RateDate { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxRate"/> class.
    /// </summary>
    /// <param name="id">The foreign exchange rate identifier.</param>
    /// <param name="baseCurrencyCode">The base currency code.</param>
    /// <param name="quoteCurrencyCode">The quote currency code.</param>
    /// <param name="rate">The exchange rate value.</param>
    /// <param name="rateDate">The rate date.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="id"/> is less than or equal to 0, or when <paramref name="baseCurrencyCode"/> or <paramref name="quoteCurrencyCode"/> is not defined in the enumeration.
    /// </exception>
    public FxRate(
        int id, 
        CurrencyCode baseCurrencyCode, 
        CurrencyCode quoteCurrencyCode, 
        decimal rate, 
        DateTime rateDate)
    {
        // Validate parameters
        Guard.AgainstNegativeOrZero(id, nameof(id));
        Guard.AgainstUndefinedEnumValue(baseCurrencyCode, nameof(baseCurrencyCode));
        Guard.AgainstUndefinedEnumValue(quoteCurrencyCode, nameof(quoteCurrencyCode));
        Guard.AgainstNegativeOrZero(rate, nameof(rate));

        Id = id;
        BaseCurrencyCode = baseCurrencyCode;
        QuoteCurrencyCode = quoteCurrencyCode;
        Rate = rate;
        RateDate = rateDate;
    }
}
