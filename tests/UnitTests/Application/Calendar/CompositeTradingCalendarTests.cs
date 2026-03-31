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

namespace Boutquin.Trading.Tests.UnitTests.Application.Calendar;

using Boutquin.Trading.Application.Calendar;
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Interfaces;
using FluentAssertions;

/// <summary>
/// Tests for the composite trading calendar that combines multiple market calendars.
/// </summary>
public sealed class CompositeTradingCalendarTests
{
    private static readonly UsTradingCalendar s_usCalendar = new();
    private static readonly CanadianTradingCalendar s_caCalendar = new();

    [Fact]
    public void AnyMode_UsHoliday_StillTradingDay()
    {
        // Jul 4 2025 (Friday): US closed, TSX open -> composite Any says open
        var calendar = new CompositeTradingCalendar(
            new ITradingCalendar[] { s_usCalendar, s_caCalendar },
            CalendarCompositionMode.Any);

        var date = new DateOnly(2025, 7, 4);

        calendar.IsTradingDay(date).Should().BeTrue("in Any mode, one market open is enough");
    }

    [Fact]
    public void AllMode_UsHoliday_NotTradingDay()
    {
        // Jul 4 2025 (Friday): US closed -> composite All says closed
        var calendar = new CompositeTradingCalendar(
            new ITradingCalendar[] { s_usCalendar, s_caCalendar },
            CalendarCompositionMode.All);

        var date = new DateOnly(2025, 7, 4);

        calendar.IsTradingDay(date).Should().BeFalse("in All mode, all markets must be open");
    }

    [Fact]
    public void AnyMode_BothClosed_NotTradingDay()
    {
        // Christmas 2025 (Thursday Dec 25): both US and Canada closed
        var calendar = new CompositeTradingCalendar(
            new ITradingCalendar[] { s_usCalendar, s_caCalendar },
            CalendarCompositionMode.Any);

        var date = new DateOnly(2025, 12, 25);

        calendar.IsTradingDay(date).Should().BeFalse("both markets closed means composite is closed in Any mode");
    }

    [Fact]
    public void AllMode_BothOpen_IsTradingDay()
    {
        // 2025-01-08 Wednesday: both US and Canada open
        var calendar = new CompositeTradingCalendar(
            new ITradingCalendar[] { s_usCalendar, s_caCalendar },
            CalendarCompositionMode.All);

        var date = new DateOnly(2025, 1, 8);

        calendar.IsTradingDay(date).Should().BeTrue("both markets open means composite is open in All mode");
    }

    [Fact]
    public void NextTradingDay_AllMode_SkipsPartialHoliday()
    {
        // Jul 3 2025 (Thursday) is a trading day for both.
        // Jul 4 2025 (Friday): US closed, TSX open -> All mode skips Jul 4
        // Jul 5-6 = weekend. Next All-open day is Jul 7 (Monday)
        var calendar = new CompositeTradingCalendar(
            new ITradingCalendar[] { s_usCalendar, s_caCalendar },
            CalendarCompositionMode.All);

        var date = new DateOnly(2025, 7, 3);

        calendar.NextTradingDay(date).Should().Be(new DateOnly(2025, 7, 7));
    }

    [Fact]
    public void TradingDaysPerYear_ReturnsMaxOfConstituents()
    {
        var calendar = new CompositeTradingCalendar(
            new ITradingCalendar[] { s_usCalendar, s_caCalendar },
            CalendarCompositionMode.Any);

        // Any mode: more days are open, so TradingDaysPerYear should be max
        calendar.TradingDaysPerYear.Should().Be(252);
    }

    [Fact]
    public void TradingDaysPerYear_AllMode_ReturnsMinOfConstituents()
    {
        var calendar = new CompositeTradingCalendar(
            new ITradingCalendar[] { s_usCalendar, s_caCalendar },
            CalendarCompositionMode.All);

        // All mode: fewer days are open, so TradingDaysPerYear should be min
        calendar.TradingDaysPerYear.Should().Be(250);
    }
}
