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

using Domain.Enums;
using Domain.Interfaces;

/// <summary>
/// Combines multiple trading calendars. In <see cref="CalendarCompositionMode.Any"/> mode,
/// a day is a trading day if any constituent market is open. In <see cref="CalendarCompositionMode.All"/> mode,
/// a day is a trading day only if all constituent markets are open.
/// </summary>
public sealed class CompositeTradingCalendar : ITradingCalendar
{
    private readonly IReadOnlyList<ITradingCalendar> _calendars;
    private readonly CalendarCompositionMode _mode;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeTradingCalendar"/> class.
    /// </summary>
    /// <param name="calendars">The constituent trading calendars.</param>
    /// <param name="mode">The composition mode (Any or All).</param>
    public CompositeTradingCalendar(
        IEnumerable<ITradingCalendar> calendars,
        CalendarCompositionMode mode)
    {
        ArgumentNullException.ThrowIfNull(calendars);

        _calendars = calendars.ToList();
        _mode = mode;

        if (_calendars.Count == 0)
        {
            throw new ArgumentException("At least one calendar must be provided.", nameof(calendars));
        }
    }

    /// <inheritdoc />
    public int TradingDaysPerYear => _mode switch
    {
        CalendarCompositionMode.Any => _calendars.Max(c => c.TradingDaysPerYear),
        CalendarCompositionMode.All => _calendars.Min(c => c.TradingDaysPerYear),
        _ => throw new ArgumentOutOfRangeException(nameof(_mode)),
    };

    /// <inheritdoc />
    public bool IsTradingDay(DateOnly date)
    {
        return _mode switch
        {
            CalendarCompositionMode.Any => _calendars.Any(c => c.IsTradingDay(date)),
            CalendarCompositionMode.All => _calendars.All(c => c.IsTradingDay(date)),
            _ => throw new ArgumentOutOfRangeException(nameof(_mode)),
        };
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
}
