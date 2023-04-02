﻿// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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
/// Represents a city entity.
/// </summary>
public sealed class City
{
    /// <summary>
    /// Initializes a new instance of the <see cref="City"/> class.
    /// </summary>
    /// <param name="id">The city identifier.</param>
    /// <param name="name">The city name.</param>
    /// <param name="timeZoneCode">The time zone code associated with the city.</param>
    /// <param name="countryCode">The country code associated with the city.</param>
    /// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
    public City(
        string name,
        TimeZoneCode timeZoneCode,
        CountryCode countryCode)
    {
        // Validate parameters
        Guard.AgainstNullOrWhiteSpaceAndOverflow(name, nameof(name), ColumnConstants.City_Name_Length);
        Guard.AgainstUndefinedEnumValue(timeZoneCode, nameof(timeZoneCode));
        Guard.AgainstUndefinedEnumValue(countryCode, nameof(countryCode));

        Name = name;
        TimeZoneCode = timeZoneCode;
        CountryCode = countryCode;
    }

    /// <summary>
    /// The city identifier.
    /// </summary>
    private int _id; // Private key for EF

    /// <summary>
    /// Gets the city name.
    /// </summary>
    public string Name { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the time zone code associated with the city.
    /// </summary>
    public TimeZoneCode TimeZoneCode { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the country code associated with the city.
    /// </summary>
    public CountryCode CountryCode { get; private set; } // Setter is for EF

    /// <summary>
    /// The name of the primary key column in the City table.
    /// </summary>
    public const string City_Key_Name = nameof(City._id);
}
