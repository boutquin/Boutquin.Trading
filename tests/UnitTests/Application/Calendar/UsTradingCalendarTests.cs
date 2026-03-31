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
using FluentAssertions;

/// <summary>
/// Tests for the US (NYSE/NASDAQ) trading calendar.
/// </summary>
public sealed class UsTradingCalendarTests
{
    private readonly UsTradingCalendar _calendar = new();

    [Fact]
    public void IsTradingDay_Weekday_ReturnsTrue()
    {
        // 2025-01-08 is a Wednesday (no holiday)
        var date = new DateOnly(2025, 1, 8);

        _calendar.IsTradingDay(date).Should().BeTrue("a normal Wednesday is a trading day");
    }

    [Fact]
    public void IsTradingDay_Saturday_ReturnsFalse()
    {
        // 2025-01-11 is a Saturday
        var date = new DateOnly(2025, 1, 11);

        _calendar.IsTradingDay(date).Should().BeFalse("Saturday is not a trading day");
    }

    [Fact]
    public void IsTradingDay_Christmas_ReturnsFalse()
    {
        // 2025-12-25 is a Thursday
        var date = new DateOnly(2025, 12, 25);

        _calendar.IsTradingDay(date).Should().BeFalse("Christmas is a market holiday");
    }

    [Fact]
    public void IsTradingDay_ChristmasOnSaturday_FridayObserved()
    {
        // 2021-12-25 is a Saturday -> Friday Dec 24 is observed holiday
        var friday = new DateOnly(2021, 12, 24);

        _calendar.IsTradingDay(friday).Should().BeFalse("Christmas on Saturday is observed on Friday");
    }

    [Fact]
    public void IsTradingDay_Juneteenth_ReturnsFalse()
    {
        // 2025-06-19 is a Thursday (Juneteenth, holiday since 2021)
        var date = new DateOnly(2025, 6, 19);

        _calendar.IsTradingDay(date).Should().BeFalse("Juneteenth is a market holiday");
    }

    [Fact]
    public void IsTradingDay_GoodFriday_ReturnsFalse()
    {
        // Good Friday 2025 is April 18
        var date = new DateOnly(2025, 4, 18);

        _calendar.IsTradingDay(date).Should().BeFalse("Good Friday is a market holiday");
    }

    [Fact]
    public void NextTradingDay_Friday_ReturnsMonday()
    {
        // 2025-01-10 is a Friday
        var friday = new DateOnly(2025, 1, 10);

        var next = _calendar.NextTradingDay(friday);

        next.Should().Be(new DateOnly(2025, 1, 13), "next trading day after Friday is Monday");
    }

    [Fact]
    public void NextTradingDay_BeforeLongWeekend_SkipsHolidayToo()
    {
        // 2025-01-17 is Friday before MLK Day (Monday Jan 20)
        var friday = new DateOnly(2025, 1, 17);

        var next = _calendar.NextTradingDay(friday);

        next.Should().Be(new DateOnly(2025, 1, 21), "next trading day skips weekend + MLK Day Monday");
    }

    [Fact]
    public void PreviousTradingDay_Monday_ReturnsFriday()
    {
        // 2025-01-13 is a Monday
        var monday = new DateOnly(2025, 1, 13);

        var prev = _calendar.PreviousTradingDay(monday);

        prev.Should().Be(new DateOnly(2025, 1, 10), "previous trading day before Monday is Friday");
    }

    [Fact]
    public void TradingDaysBetween_FullYear2024_Returns252()
    {
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 12, 31);

        var days = _calendar.TradingDaysBetween(start, end);

        days.Should().HaveCount(252, "NYSE has 252 trading days in 2024");
    }

    [Fact]
    public void TradingDaysBetween_EmptyRange_ReturnsEmpty()
    {
        // Saturday to Sunday — no trading days
        var start = new DateOnly(2025, 1, 11);
        var end = new DateOnly(2025, 1, 12);

        var days = _calendar.TradingDaysBetween(start, end);

        days.Should().BeEmpty("a Saturday-Sunday range has no trading days");
    }

    [Fact]
    public void TradingDaysPerYear_Returns252()
    {
        _calendar.TradingDaysPerYear.Should().Be(252);
    }

    [Fact]
    public void IsTradingDay_NewYearsOnSunday_MondayObserved()
    {
        // 2023-01-01 is Sunday -> Monday Jan 2 is observed
        var monday = new DateOnly(2023, 1, 2);

        _calendar.IsTradingDay(monday).Should().BeFalse("New Year's on Sunday is observed on Monday");
    }

    [Fact]
    public void IsTradingDay_MLKDay_ReturnsFalse()
    {
        // MLK Day 2025 is Monday Jan 20 (3rd Monday of January)
        var date = new DateOnly(2025, 1, 20);

        _calendar.IsTradingDay(date).Should().BeFalse("MLK Day is a market holiday");
    }

    [Fact]
    public void IsTradingDay_PresidentsDay_ReturnsFalse()
    {
        // Presidents' Day 2025 is Monday Feb 17 (3rd Monday of February)
        var date = new DateOnly(2025, 2, 17);

        _calendar.IsTradingDay(date).Should().BeFalse("Presidents' Day is a market holiday");
    }

    [Fact]
    public void IsTradingDay_MemorialDay_ReturnsFalse()
    {
        // Memorial Day 2025 is Monday May 26 (last Monday of May)
        var date = new DateOnly(2025, 5, 26);

        _calendar.IsTradingDay(date).Should().BeFalse("Memorial Day is a market holiday");
    }

    [Fact]
    public void IsTradingDay_LaborDay_ReturnsFalse()
    {
        // Labor Day 2025 is Monday Sep 1 (1st Monday of September)
        var date = new DateOnly(2025, 9, 1);

        _calendar.IsTradingDay(date).Should().BeFalse("Labor Day is a market holiday");
    }

    [Fact]
    public void IsTradingDay_Thanksgiving_ReturnsFalse()
    {
        // Thanksgiving 2025 is Thursday Nov 27 (4th Thursday of November)
        var date = new DateOnly(2025, 11, 27);

        _calendar.IsTradingDay(date).Should().BeFalse("Thanksgiving is a market holiday");
    }

    [Fact]
    public void IsTradingDay_July4thOnSaturday_FridayObserved()
    {
        // 2020-07-04 is Saturday -> Friday Jul 3 is observed
        var friday = new DateOnly(2020, 7, 3);

        _calendar.IsTradingDay(friday).Should().BeFalse("July 4th on Saturday is observed on Friday");
    }
}
