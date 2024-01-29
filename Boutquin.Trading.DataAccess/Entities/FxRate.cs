// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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

/// <summary>
/// Represents a foreign exchange rate.
/// </summary>
public sealed class FxRate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FxRate"/> class.
    /// </summary>
    /// <param name="rateDate">The rate date.</param>
    /// <param name="baseCurrencyCode">The base ISO 4217 currency code.</param>
    /// <param name="quoteCurrencyCode">The quote ISO 4217 currency code.</param>
    /// <param name="rate">The exchange rate value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="rate"/> is less than or equal to 0, or 
    /// when <paramref name="baseCurrencyCode"/> or <paramref name="quoteCurrencyCode"/> 
    /// are not defined in the <see cref="CurrencyCode"/> enumeration.
    /// </exception>
    public FxRate(
        DateOnly rateDate,
        CurrencyCode baseCurrencyCode,
        CurrencyCode quoteCurrencyCode,
        decimal rate
        )
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(() => baseCurrencyCode);
        Guard.AgainstUndefinedEnumValue(() => quoteCurrencyCode);
        Guard.AgainstNegativeOrZero(() => rate);

        _id = -1;
        RateDate = rateDate;
        BaseCurrencyCode = baseCurrencyCode;
        QuoteCurrencyCode = quoteCurrencyCode;
        Rate = rate;
    }

    /// <summary>
    /// The foreign exchange rate identifier.
    /// </summary>
    private int _id; // Private key for EF

    /// <summary>
    /// Gets the rate date.
    /// </summary>
    public DateOnly RateDate { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the base ISO 4217 currency code.
    /// </summary>
    public CurrencyCode BaseCurrencyCode { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the quote ISO 4217 currency code.
    /// </summary>
    public CurrencyCode QuoteCurrencyCode { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the exchange rate value.
    /// </summary>
    public decimal Rate { get; private set; } // Setter is for EF

    /// <summary>
    /// The name of the primary key column in the FxRate table.
    /// </summary>
    public const string FxRate_Key_Name = nameof(_id);
}
