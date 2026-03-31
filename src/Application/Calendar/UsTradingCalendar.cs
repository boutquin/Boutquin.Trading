// Copyright (c) 2023-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

namespace Boutquin.Trading.Application.Calendar;

using Domain.Interfaces;

/// <summary>
/// NYSE/NASDAQ trading calendar. Implements US market holidays including
/// Saturday→Friday and Sunday→Monday observance rules.
/// </summary>
public sealed class UsTradingCalendar : ITradingCalendar
{
    /// <inheritdoc />
    public int TradingDaysPerYear => 252;

    /// <inheritdoc />
    public bool IsTradingDay(DateOnly date)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        return !IsUsHoliday(date);
    }

    /// <inheritdoc />
    public DateOnly NextTradingDay(DateOnly date)
    {
        var candidate = date.AddDays(1);
        while (!IsTradingDay(candidate))
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    /// <inheritdoc />
    public DateOnly PreviousTradingDay(DateOnly date)
    {
        var candidate = date.AddDays(-1);
        while (!IsTradingDay(candidate))
        {
            candidate = candidate.AddDays(-1);
        }

        return candidate;
    }

    /// <inheritdoc />
    public IReadOnlyList<DateOnly> TradingDaysBetween(DateOnly start, DateOnly end)
    {
        var result = new List<DateOnly>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (IsTradingDay(d))
            {
                result.Add(d);
            }
        }

        return result;
    }

    /// <summary>
    /// Determines whether the given weekday is an observed US market holiday.
    /// </summary>
    private static bool IsUsHoliday(DateOnly date)
    {
        var year = date.Year;
        var month = date.Month;
        var day = date.Day;

        // New Year's Day (Jan 1) — observed
        if (IsObservedHoliday(date, new DateOnly(year, 1, 1)))
        {
            return true;
        }

        // MLK Day — 3rd Monday of January
        if (month == 1 && date.DayOfWeek == DayOfWeek.Monday && NthDayOfWeek(year, 1, DayOfWeek.Monday, 3) == day)
        {
            return true;
        }

        // Presidents' Day — 3rd Monday of February
        if (month == 2 && date.DayOfWeek == DayOfWeek.Monday && NthDayOfWeek(year, 2, DayOfWeek.Monday, 3) == day)
        {
            return true;
        }

        // Good Friday
        if (date == GoodFriday(year))
        {
            return true;
        }

        // Memorial Day — last Monday of May
        if (month == 5 && date.DayOfWeek == DayOfWeek.Monday && LastDayOfWeek(year, 5, DayOfWeek.Monday) == day)
        {
            return true;
        }

        // Juneteenth (Jun 19) — observed, holiday since 2021
        if (year >= 2021 && IsObservedHoliday(date, new DateOnly(year, 6, 19)))
        {
            return true;
        }

        // Independence Day (Jul 4) — observed
        if (IsObservedHoliday(date, new DateOnly(year, 7, 4)))
        {
            return true;
        }

        // Labor Day — 1st Monday of September
        if (month == 9 && date.DayOfWeek == DayOfWeek.Monday && NthDayOfWeek(year, 9, DayOfWeek.Monday, 1) == day)
        {
            return true;
        }

        // Thanksgiving — 4th Thursday of November
        if (month == 11 && date.DayOfWeek == DayOfWeek.Thursday && NthDayOfWeek(year, 11, DayOfWeek.Thursday, 4) == day)
        {
            return true;
        }

        // Christmas (Dec 25) — observed
        if (IsObservedHoliday(date, new DateOnly(year, 12, 25)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if <paramref name="date"/> is the observed date for <paramref name="holiday"/>.
    /// Saturday holidays are observed on Friday; Sunday holidays on Monday.
    /// </summary>
    private static bool IsObservedHoliday(DateOnly date, DateOnly holiday)
    {
        var observed = holiday.DayOfWeek switch
        {
            DayOfWeek.Saturday => holiday.AddDays(-1), // Friday
            DayOfWeek.Sunday => holiday.AddDays(1),    // Monday
            _ => holiday,
        };

        return date == observed;
    }

    /// <summary>
    /// Returns the day-of-month for the Nth occurrence of a given day of week in a month.
    /// </summary>
    private static int NthDayOfWeek(int year, int month, DayOfWeek dayOfWeek, int n)
    {
        var first = new DateOnly(year, month, 1);
        var offset = ((int)dayOfWeek - (int)first.DayOfWeek + 7) % 7;
        return 1 + offset + ((n - 1) * 7);
    }

    /// <summary>
    /// Returns the day-of-month for the last occurrence of a given day of week in a month.
    /// </summary>
    private static int LastDayOfWeek(int year, int month, DayOfWeek dayOfWeek)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var last = new DateOnly(year, month, daysInMonth);
        var offset = ((int)last.DayOfWeek - (int)dayOfWeek + 7) % 7;
        return daysInMonth - offset;
    }

    /// <summary>
    /// Computes the date of Good Friday for a given year using the Anonymous Gregorian algorithm for Easter.
    /// </summary>
    private static DateOnly GoodFriday(int year)
    {
        var easter = ComputeEaster(year);
        return easter.AddDays(-2);
    }

    /// <summary>
    /// Computes Easter Sunday using the Anonymous Gregorian algorithm.
    /// </summary>
    private static DateOnly ComputeEaster(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var month = (h + l - (7 * m) + 114) / 31;
        var day = ((h + l - (7 * m) + 114) % 31) + 1;

        return new DateOnly(year, month, day);
    }
}
