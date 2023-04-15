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
/// Represents an exchange where securities are traded.
/// </summary>
public sealed class Exchange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Exchange"/> class.
    /// </summary>
    /// <param name="exchangeCode">The ISO 10383 market identifier code of the exchange.</param>
    /// <param name="name">The exchange name.</param>
    /// <param name="city">The city where the exchange resides.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="exchangeCode"/> is not defined in 
    /// the <see cref="ExchangeCode"/> enumeration.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the <paramref name="name"/> is null, empty or 
    /// longer than the allowed length.</exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="city"/> is null.
    /// </exception>
    public Exchange(
        ExchangeCode exchangeCode,
        string name,
        City city)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(() => exchangeCode);
        Guard.AgainstNullOrWhiteSpaceAndOverflow(() => name, ColumnConstants.Exchange_Name_Length);
        Guard.AgainstNull(() => city);

        Code = exchangeCode;
        Name = name;
        City = city;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Exchange"/> class.
    /// </summary>
    public Exchange()
    {
    }

    /// <summary>
    /// Gets the ISO 10383 market identifier code of the exchange.
    /// </summary>
    public ExchangeCode Code { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the exchange name.
    /// </summary>
    public string Name { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the city where the exchange resides.
    /// </summary>
    public City City { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the collection of ExchangeSchedule entities associated with the Exchange.
    /// </summary>
    public ICollection<ExchangeSchedule> ExchangeSchedules { get; private set; } // Setter is for EF
        = new HashSet<ExchangeSchedule>();

    /// <summary>
    /// Gets the collection of ExchangeHoliday entities associated with the Exchange.
    /// </summary>
    public ICollection<ExchangeHoliday> ExchangeHolidays { get; private set; } // Setter is for EF
        = new HashSet<ExchangeHoliday>();
}
