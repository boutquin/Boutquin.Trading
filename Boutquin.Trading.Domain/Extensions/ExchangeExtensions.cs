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
using Boutquin.Trading.Domain.Entities;

namespace Boutquin.Trading.Domain.Extensions;

/// <summary>
/// Provides a set of extension methods for the <see cref="Exchange"/> class.
/// </summary>
public static class ExchangeExtensions
{
    /// <summary>
    /// Determines if the exchange is open on a given day.
    /// </summary>
    /// <param name="exchange">The exchange to check.</param>
    /// <param name="day">The day to check if the exchange is open.</param>
    /// <returns>True if the exchange is open on the given day, otherwise false.</returns>
    /// <example>
    /// <code>
    /// var exchange = new Exchange { ... };
    /// DateOnly day = DateOnly.FromDateTime(DateTime.Now);
    /// bool isOpen = exchange.IsOpen(day);
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">Thrown when the exchange is null.</exception>
    public static bool IsOpen(
        this Exchange exchange, 
        DateOnly day)
    {
        Guard.AgainstNull(exchange, nameof(exchange));

        var weekDay = day.DayOfWeek;
        var schedule = exchange.ExchangeSchedules.FirstOrDefault(es => es.DayOfWeek == weekDay);

        if (schedule == null)
        {
            return false;
        }

        var isHoliday = exchange.ExchangeHolidays.Any(eh => eh.HolidayDate == day);

        return !isHoliday;
    }

    /// <summary>
    /// Determines the closing time of the exchange on a given day, considering the minutes before closing.
    /// </summary>
    /// <param name="exchange">The exchange to check.</param>
    /// <param name="day">The day to check the exchange closing time.</param>
    /// <param name="minutesBeforeClosing">Optional. The number of minutes before the actual closing time. Default is 0.</param>
    /// <returns>The closing time of the exchange on the given day, or null if the exchange is not open on the given day.</returns>
    /// <example>
    /// <code>
    /// var exchange = new Exchange { ... };
    /// DateTime day = DateTime.Now;
    /// int minutesBeforeClosing = 30;
    /// DateTime? closingTime = exchange.GetClosingTime(day, minutesBeforeClosing);
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">Thrown when the exchange is null.</exception>
    public static DateTime? GetClosingTime(
        this Exchange exchange, 
        DateTime day, 
        int minutesBeforeClosing = 0)
    {
        Guard.AgainstNull(exchange, nameof(exchange));

        if (!exchange.IsOpen(DateOnly.FromDateTime(day)))
        {
            return null;
        }

        var weekDay = day.DayOfWeek;
        var schedule = exchange.ExchangeSchedules.FirstOrDefault(es => es.DayOfWeek == weekDay);

        if (schedule == null)
        {
            return null;
        }

        return day.Add(schedule.CloseTime).AddMinutes(-minutesBeforeClosing);
    }
}
