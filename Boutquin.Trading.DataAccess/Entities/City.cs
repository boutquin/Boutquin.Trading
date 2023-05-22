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
namespace Boutquin.Trading.DataAccess.Entities;

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
    /// <param name="timeZoneCode">The ISO 8601 time zone code associated with the city.</param>
    /// <param name="countryCode">The ISO 3166-1:2020 alpha-2 country code associated with the city.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the <paramref name="name"/> is null, empty or 
    /// longer than the allowed length.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="timeZoneCode"/>, or <paramref name="countryCode"/> 
    /// are not defined in their respective enumerations.
    /// </exception>
    public City(
        string name,
        TimeZoneCode timeZoneCode,
        CountryCode countryCode)
    {
        // Validate parameters
        Guard.AgainstNullOrWhiteSpaceAndOverflow(() => name, ColumnConstants.City_Name_Length);
        Guard.AgainstUndefinedEnumValue(() => timeZoneCode);
        Guard.AgainstUndefinedEnumValue(() => countryCode);

        _id = -1;
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
    /// Gets the ISO 8601 time zone code associated with the city.
    /// </summary>
    public TimeZoneCode TimeZoneCode { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the ISO 3166-1:2020 alpha-2 country code associated with the city.
    /// </summary>
    public CountryCode CountryCode { get; private set; } // Setter is for EF

    /// <summary>
    /// The name of the primary key column in the City table.
    /// </summary>
    public const string City_Key_Name = nameof(_id);
}
