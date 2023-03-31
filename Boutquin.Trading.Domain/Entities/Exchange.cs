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
    /// Gets the market identifier code.
    /// </summary>
    public ExchangeCode Code { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the exchange name.
    /// </summary>
    public string Name { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the city identifier.
    /// </summary>
    public int CityId { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets or sets the collection of ExchangeSchedule entities associated with the Exchange.
    /// </summary>
    public ICollection<ExchangeSchedule> ExchangeSchedules { get; private set; } // Setter is for EF
        = new HashSet<ExchangeSchedule>();

    /// <summary>
    /// Gets or sets the collection of ExchangeHoliday entities associated with the Exchange.
    /// </summary>
    public ICollection<ExchangeHoliday> ExchangeHolidays { get; private set; } // Setter is for EF
        = new HashSet<ExchangeHoliday>();   

    /// <summary>
    /// Initializes a new instance of the <see cref="Exchange"/> class.
    /// </summary>
    /// <param name="exchangeCode">The market identifier code.</param>
    /// <param name="name">The exchange name.</param>
    /// <param name="cityId">The city identifier.</param>
    /// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
    public Exchange(
        ExchangeCode exchangeCode, 
        string name, 
        int cityId)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(exchangeCode, nameof(exchangeCode));
        Guard.AgainstNullOrWhiteSpaceAndOverflow(name, nameof(name), ColumnConstants.Exchange_Name_Length);
        Guard.AgainstNegativeOrZero(cityId, nameof(cityId));

        Code = exchangeCode;
        Name = name;
        CityId = cityId;
    }

    public Exchange()
    {
    }
}
