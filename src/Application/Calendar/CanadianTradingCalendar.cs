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
/// TSX (Toronto Stock Exchange) trading calendar. Implements Canadian market holidays
/// including Saturday→Friday and Sunday→Monday observance rules.
/// </summary>
public sealed class CanadianTradingCalendar : ITradingCalendar
{
    /// <inheritdoc />
    public int TradingDaysPerYear => 250;

    /// <inheritdoc />
    public bool IsTradingDay(DateOnly date)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        return !IsCanadianHoliday(date);
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
    /// Determines whether the given weekday is an observed TSX holiday.
    /// </summary>
    private static bool IsCanadianHoliday(DateOnly date)
    {
        var year = date.Year;
        var month = date.Month;
        var day = date.Day;

        // New Year's Day (Jan 1) — observed
        if (IsObservedHoliday(date, new DateOnly(year, 1, 1)))
        {
            return true;
        }

        // Family Day — 3rd Monday of February
        if (month == 2 && date.DayOfWeek == DayOfWeek.Monday && NthDayOfWeek(year, 2, DayOfWeek.Monday, 3) == day)
        {
            return true;
        }

        // Good Friday
        if (date == GoodFriday(year))
        {
            return true;
        }

        // Victoria Day — Monday before May 25
        if (month == 5 && date.DayOfWeek == DayOfWeek.Monday)
        {
            var may25 = new DateOnly(year, 5, 25);
            var victoriaDay = may25.DayOfWeek == DayOfWeek.Monday
                ? may25
                : may25.AddDays(-((int)may25.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7);
            if (date == victoriaDay)
            {
                return true;
            }
        }

        // Canada Day (Jul 1) — observed
        if (IsObservedHoliday(date, new DateOnly(year, 7, 1)))
        {
            return true;
        }

        // Civic Holiday — 1st Monday of August
        if (month == 8 && date.DayOfWeek == DayOfWeek.Monday && NthDayOfWeek(year, 8, DayOfWeek.Monday, 1) == day)
        {
            return true;
        }

        // Labour Day — 1st Monday of September
        if (month == 9 && date.DayOfWeek == DayOfWeek.Monday && NthDayOfWeek(year, 9, DayOfWeek.Monday, 1) == day)
        {
            return true;
        }

        // Canadian Thanksgiving — 2nd Monday of October
        if (month == 10 && date.DayOfWeek == DayOfWeek.Monday && NthDayOfWeek(year, 10, DayOfWeek.Monday, 2) == day)
        {
            return true;
        }

        // Christmas (Dec 25) and Boxing Day (Dec 26) — with special weekend handling
        if (IsChristmasOrBoxingDayObserved(date, year))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles Christmas + Boxing Day observance as a pair.
    /// When both fall on weekend, Monday + Tuesday are observed.
    /// </summary>
    private static bool IsChristmasOrBoxingDayObserved(DateOnly date, int year)
    {
        var christmas = new DateOnly(year, 12, 25);
        var boxingDay = new DateOnly(year, 12, 26);

        // Saturday Christmas: Fri=Christmas observed, Mon=Boxing Day observed
        if (christmas.DayOfWeek == DayOfWeek.Saturday)
        {
            return date == christmas.AddDays(-1) || date == christmas.AddDays(2);
        }

        // Sunday Christmas: Mon=Christmas observed, Tue=Boxing Day observed
        if (christmas.DayOfWeek == DayOfWeek.Sunday)
        {
            return date == christmas.AddDays(1) || date == christmas.AddDays(2);
        }

        // Friday Christmas: Christmas=Fri, Boxing Day=Sat -> Boxing Day observed Mon
        if (christmas.DayOfWeek == DayOfWeek.Friday)
        {
            return date == christmas || date == christmas.AddDays(3);
        }

        // Normal weekday Christmas: both are on weekdays
        return date == christmas || date == boxingDay;
    }

    /// <summary>
    /// Returns true if <paramref name="date"/> is the observed date for <paramref name="holiday"/>.
    /// Saturday holidays are observed on the following Monday; Sunday holidays on Monday.
    /// </summary>
    private static bool IsObservedHoliday(DateOnly date, DateOnly holiday)
    {
        var observed = holiday.DayOfWeek switch
        {
            DayOfWeek.Saturday => holiday.AddDays(2), // Monday
            DayOfWeek.Sunday => holiday.AddDays(1),   // Monday
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
    /// Computes the date of Good Friday for a given year.
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
