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
/// Represents a holiday for a stock exchange.
/// </summary>
public sealed class ExchangeHoliday
{
    /// <summary>
    /// Gets the identifier of the exchange holiday.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the exchange code.
    /// </summary>
    public ExchangeCode ExchangeCode { get; }

    /// <summary>
    /// Gets the holiday date.
    /// </summary>
    public DateTime HolidayDate { get; }

    /// <summary>
    /// Gets the holiday description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExchangeHoliday"/> class.
    /// </summary>
    /// <param name="id">The identifier of the exchange holiday.</param>
    /// <param name="exchangeCode">The exchange code.</param>
    /// <param name="holidayDate">The holiday date.</param>
    /// <param name="description">The holiday description.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the id is less than or equal to 0 or the exchangeCode is not defined in the enumeration.</exception>
    /// <exception cref="ArgumentException">Thrown when the description is null, empty or longer than the allowed length.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the description is null.</exception>
    public ExchangeHoliday(
        int id, 
        ExchangeCode exchangeCode, 
        DateTime holidayDate, 
        string description)
    {
        // Validate parameters
        Guard.AgainstNegativeOrZero(id, nameof(id));
        Guard.AgainstUndefinedEnumValue(exchangeCode, nameof(exchangeCode));
        Guard.AgainstNullOrWhiteSpaceAndOverflow(description, nameof(description), ColumnConstants.ExchangeHoliday_Description_Length);

        Id = id;
        ExchangeCode = exchangeCode;
        HolidayDate = holidayDate;
        Description = description;
    }
}
