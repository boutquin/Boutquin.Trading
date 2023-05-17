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
namespace Boutquin.Trading.Domain.Entities;

using Enums;

/// <summary>
/// Represents a holiday for a stock exchange.
/// </summary>
public sealed class ExchangeHoliday
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExchangeHoliday"/> class.
    /// </summary>
    /// <param name="exchangeCode">The ISO 10383 market identifier code of the exchange.</param>
    /// <param name="holidayDate">The holiday date.</param>
    /// <param name="description">The holiday description.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the <paramref name="exchangeCode"/> is not defined 
    /// in the <see cref="ExchangeCode"/> enumeration.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the <paramref name="description"/> is null, empty or 
    /// longer than the allowed length.
    /// </exception>
    public ExchangeHoliday(
        ExchangeCode exchangeCode,
        DateOnly holidayDate,
        string description)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(() => exchangeCode);
        Guard.AgainstNullOrWhiteSpaceAndOverflow(() => description, ColumnConstants.ExchangeHoliday_Description_Length);

        _id = -1;
        ExchangeCode = exchangeCode;
        HolidayDate = holidayDate;
        Description = description;
    }

    /// <summary>
    /// The identifier of the exchange holiday.
    /// </summary>
    private int _id; // Private key for EF

    /// <summary>
    /// Gets the ISO 10383 market identifier code of the exchange.
    /// </summary>
    public ExchangeCode ExchangeCode { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the holiday date.
    /// </summary>
    public DateOnly HolidayDate { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the holiday description.
    /// </summary>
    public string Description { get; private set; } // Setter is for EF

    /// <summary>
    /// The name of the primary key column in the ExchangeHoliday table.
    /// </summary>
    public const string ExchangeHoliday_Key_Name = nameof(_id);
}
