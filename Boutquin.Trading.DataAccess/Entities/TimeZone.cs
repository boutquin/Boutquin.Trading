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
/// Represents an ISO 8601 Time Zone.
/// </summary>
public sealed class TimeZone
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZone"/> class.
    /// </summary>
    /// <param name="code">The ISO 8601 Time Zone Code.</param>
    /// <param name="name">The name of the time zone.</param>
    /// <param name="timeZoneOffset">The time zone offset.</param>
    /// <param name="usesDaylightSaving">A value indicating whether the time zone uses daylight saving time.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="timeZoneOffset"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="name"/> length or <paramref name="timeZoneOffset"/> length is not within the valid range, or 
    /// when <paramref name="code"/> is not defined in the <see cref="TimeZoneCode"/> enumeration.
    /// </exception>
    public TimeZone(
        TimeZoneCode code,
        string name,
        string timeZoneOffset,
        bool usesDaylightSaving)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(() => code);
        Guard.AgainstNullOrWhiteSpaceAndOverflow(() => name, ColumnConstants.TimeZone_Name_Length);
        Guard.AgainstNullOrWhiteSpaceAndOverflow(() => timeZoneOffset, ColumnConstants.TimeZone_TimeZoneOffset_Length);

        Name = name;
        TimeZoneOffset = timeZoneOffset;
        Code = code;
        UsesDaylightSaving = usesDaylightSaving;
    }

    /// <summary>
    /// Gets the ISO 8601 Time Zone Code.
    /// </summary>
    public TimeZoneCode Code { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the name of the time zone.
    /// </summary>
    public string Name { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the time zone offset.
    /// </summary>
    public string TimeZoneOffset { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets a value indicating whether the time zone uses daylight saving time.
    /// </summary>
    public bool UsesDaylightSaving { get; private set; } // Setter is for EF
}
