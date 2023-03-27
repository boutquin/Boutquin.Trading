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

using System;

using Boutquin.Trading.Domain.Enums;

/// <summary>
/// Represents an ISO 8601 Time Zone.
/// </summary>
public sealed class TimeZone
{
    /// <summary>
    /// Gets the ISO 8601 Time Zone Code.
    /// </summary>
    public TimeZoneCode Code { get; }

    /// <summary>
    /// Gets the name of the time zone.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the time zone offset.
    /// </summary>
    public string TimeZoneOffset { get; }

    /// <summary>
    /// Gets a value indicating whether the time zone uses daylight saving time.
    /// </summary>
    public bool IsDaylightSaving { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZone"/> class.
    /// </summary>
    /// <param name="code">The time zone code.</param>
    /// <param name="name">The name of the time zone.</param>
    /// <param name="timeZoneOffset">The time zone offset.</param>
    /// <param name="isDaylightSaving">A value indicating whether the time zone uses daylight saving time.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="timeZoneOffset"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="name"/> length or <paramref name="timeZoneOffset"/> length is not within the valid range, or when <paramref name="code"/> is not defined in the enumeration.
    /// </exception>
    public TimeZone(
        TimeZoneCode code, 
        string name, 
        string timeZoneOffset, 
        bool isDaylightSaving)
    {
        if (!Enum.IsDefined(typeof(TimeZoneCode), code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), "Invalid time zone code.");
        }

        Name = name ?? throw new ArgumentNullException(nameof(name), "Name cannot be null.");

        if (name.Length == 0 || name.Length > ColumnConstants.TimeZone_Name_Length)
        {
            throw new ArgumentOutOfRangeException(nameof(name), $"Name must be between 1 and {ColumnConstants.TimeZone_Name_Length} characters.");
        }

        TimeZoneOffset = timeZoneOffset ?? throw new ArgumentNullException(nameof(timeZoneOffset), "Time zone offset cannot be null.");

        if (timeZoneOffset.Length == 0 || timeZoneOffset.Length > ColumnConstants.TimeZone_TimeZoneOffset_Length)
        {
            throw new ArgumentOutOfRangeException(nameof(timeZoneOffset), $"Time zone offset must be between 1 and {ColumnConstants.TimeZone_TimeZoneOffset_Length} characters.");
        }

        Code = code;
        IsDaylightSaving = isDaylightSaving;
    }
}
