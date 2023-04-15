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
/// Represents an exchange schedule.
/// </summary>
public sealed class ExchangeSchedule
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExchangeSchedule"/> class.
    /// </summary>
    /// <param name="exchangeCode">The ISO 10383 market identifier code of the exchange.</param>
    /// <param name="dayOfWeek">The day of the week for the schedule.</param>
    /// <param name="openTime">The opening time of the exchange.</param>
    /// <param name="closeTime">The closing time of the exchange.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="exchangeCode"/> or <paramref name="dayOfWeek"/> are not defined in 
    /// their respective enumerations or when the <paramref name="openTime"/> and 
    /// <paramref name="closeTime"/> are not valid time values.
    /// </exception>
    public ExchangeSchedule(
        ExchangeCode exchangeCode,
        DayOfWeek dayOfWeek,
        TimeSpan openTime,
        TimeSpan closeTime)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(() => exchangeCode);
        Guard.AgainstUndefinedEnumValue(() => dayOfWeek);
        Guard.AgainstOutOfRange(() => openTime, TimeSpan.Zero, TimeSpan.FromHours(24));
        Guard.AgainstOutOfRange(() => closeTime, TimeSpan.Zero, TimeSpan.FromHours(24));

        ExchangeCode = exchangeCode;
        DayOfWeek = dayOfWeek;
        OpenTime = openTime;
        CloseTime = closeTime;
    }

    /// <summary>
    /// The identifier of the exchange schedule.
    /// </summary>
    private int _id; // Private key for EF

    /// <summary>
    /// Gets ISO 10383 market identifier code of the exchange.
    /// </summary>
    public ExchangeCode ExchangeCode { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the day of the week for the schedule.
    /// </summary>
    public DayOfWeek DayOfWeek { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the opening time of the exchange.
    /// </summary>
    public TimeSpan OpenTime { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the closing time of the exchange.
    /// </summary>
    public TimeSpan CloseTime { get; private set; } // Setter is for EF

    /// <summary>
    /// The name of the primary key column in the ExchangeSchedule table.
    /// </summary>
    public const string ExchangeSchedule_Key_Name = nameof(_id);
}
