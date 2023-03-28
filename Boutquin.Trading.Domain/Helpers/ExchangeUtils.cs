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

namespace Boutquin.Trading.Domain.Helpers;

/// <summary>
/// The ExchangeUtils class contains utility methods for working with Exchange, ExchangeSchedule, and ExchangeHoliday entities.
/// These methods can be used to determine if an exchange is open or closed on a given day and to calculate the closing time of an exchange for a particular day.
/// </summary>
public static class ExchangeUtils
{
    /// <summary>
    /// Determines if the exchange is open on a given day.
    /// </summary>
    /// <param name="exchange">The exchange to check.</param>
    /// <param name="exchangeSchedules">The list of exchange schedules associated with the exchange.</param>
    /// <param name="exchangeHolidays">The list of exchange holidays associated with the exchange.</param>
    /// <param name="day">The day to check if the exchange is open.</param>
    /// <returns>True if the exchange is open on the given day, otherwise false.</returns>
    /// <example>
    /// <code>
    /// var exchange = new Exchange { ... };
    /// var exchangeSchedules = new List&lt;ExchangeSchedule&gt; { ... };
    /// var exchangeHolidays = new List&lt;ExchangeHoliday&gt; { ... };
    /// DateTime day = DateTime.Now;
    /// bool isOpen = ExchangeUtils.IsExchangeOpen(exchange, exchangeSchedules, exchangeHolidays, day);
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">Thrown when any of the input arguments are null.</exception>
    public static bool IsExchangeOpen(Exchange exchange, List<ExchangeSchedule> exchangeSchedules, List<ExchangeHoliday> exchangeHolidays, DateTime day)
    {
        // Validate input arguments
        Guard.AgainstNull(exchange, nameof(exchange));
        Guard.AgainstNull(exchangeSchedules, nameof(exchangeSchedules));
        Guard.AgainstNull(exchangeHolidays, nameof(exchangeHolidays));

        // Get the day of the week for the given day
        var weekDay = day.DayOfWeek;

        // Check if the exchange has a schedule for the given day of the week
        var hasSchedule = exchangeSchedules.Any(es => es.DayOfWeek == weekDay);

        // If there is no schedule for the day, the exchange is closed
        if (!hasSchedule)
        {
            return false;
        }

        // Check if the day is an exchange holiday
        var isHoliday = exchangeHolidays.Any(eh => eh.HolidayDate.Date == day.Date);

        // If the day is a holiday, the exchange is closed
        if (isHoliday)
        {
            return false;
        }

        // If there is a schedule for the day and it's not a holiday, the exchange is open
        return true;
    }

    /// <summary>
    /// Determines the closing time of the exchange for a given day.
    /// </summary>
    /// <param name="exchange">The exchange to check.</param>
    /// <param name="exchangeSchedules">The list of exchange schedules associated with the exchange.</param>
    /// <param name="exchangeHolidays">The list of exchange holidays associated with the exchange.</param>
    /// <param name="day">The day to check the closing time for.</param>
    /// <returns>The closing time of the exchange for the given day or null if the exchange is closed.</returns>
    /// <example>
    /// <code>
    /// var exchange = new Exchange { ... };
    /// var exchangeSchedules = new List&lt;ExchangeSchedule&gt; { ... };
    /// var exchangeHolidays = new List&lt;ExchangeHoliday&gt; { ... };
    /// DateTime day = DateTime.Now;
    /// DateTime? closingTime = ExchangeUtils.GetExchangeClosingTime(exchange, exchangeSchedules, exchangeHolidays, day);
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">Thrown when any of the input arguments are null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no schedule is found for the given day of the week in the provided exchange schedules.</exception>
    public static DateTime? GetExchangeClosingTime(Exchange exchange, List<ExchangeSchedule> exchangeSchedules, List<ExchangeHoliday> exchangeHolidays, DateTime day)
    {
        // Validate input arguments
        Guard.AgainstNull(exchange, nameof(exchange));
        Guard.AgainstNull(exchangeSchedules, nameof(exchangeSchedules));
        Guard.AgainstNull(exchangeHolidays, nameof(exchangeHolidays));

        // Check if the exchange is open on the given day
        if (!IsExchangeOpen(exchange, exchangeSchedules, exchangeHolidays, day))
        {
            return null;
        }

        // Get the day of the week for the given day
        var weekDay = day.DayOfWeek;

        // Find the schedule for the given day of the week
        var schedule = exchangeSchedules.FirstOrDefault(es => es.DayOfWeek == weekDay);

        // If no schedule is found, throw an exception
        if (schedule == null)
        {
            throw new InvalidOperationException($"No schedule found for {weekDay} in the provided exchange schedules.");
        }

        // Calculate the closing time for the given day
        var closingDateTime = day.Date.Add(schedule.CloseTime);
        return closingDateTime;
    }
}
