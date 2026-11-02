// Copyright (c) 2023-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
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
    /// are not defined in the <see cref="Boutquin.Domain.Enumerations.Currency"/> enumeration or
    /// when <paramref name="rate"/> is non-positive.
    /// </exception>
    public FxRate(
        DateOnly rateDate,
        DomainCurrency baseCurrencyCode,
        DomainCurrency quoteCurrencyCode,
        decimal rate
        )
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(() => baseCurrencyCode); // Throws ArgumentOutOfRangeException
        Guard.AgainstUndefinedEnumValue(() => quoteCurrencyCode); // Throws ArgumentOutOfRangeException
        Guard.AgainstNegativeOrZero(() => rate); // Throws ArgumentOutOfRangeException

        // Assign the CLR default (0) so EF Core's on-add value generation assigns the
        // database-generated key (a non-default sentinel would suppress generation).
        _id = 0;
        RateDate = rateDate;
        BaseCurrencyCode = baseCurrencyCode;
        QuoteCurrencyCode = quoteCurrencyCode;
        Rate = rate;
    }

    /// <summary>
    /// The foreign exchange rate identifier.
    /// </summary>
    private readonly int _id; // Private key for EF

    /// <summary>
    /// The rate date.
    /// </summary>
    public DateOnly RateDate { get; private set; } // Setter is for EF

    /// <summary>
    /// The base ISO 4217 currency code.
    /// </summary>
    public DomainCurrency BaseCurrencyCode { get; private set; } // Setter is for EF

    /// <summary>
    /// The quote ISO 4217 currency code.
    /// </summary>
    public DomainCurrency QuoteCurrencyCode { get; private set; } // Setter is for EF

    /// <summary>
    /// The exchange rate value.
    /// </summary>
    public decimal Rate { get; private set; } // Setter is for EF

    /// <summary>
    /// The name of the primary key column in the FxRate table.
    /// </summary>
    public const string FxRate_Key_Name = nameof(_id);
}
