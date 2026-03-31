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
/// Tests for the Canadian (TSX) trading calendar.
/// </summary>
public sealed class CanadianTradingCalendarTests
{
    private readonly CanadianTradingCalendar _calendar = new();

    [Fact]
    public void IsTradingDay_VictoriaDay_ReturnsFalse()
    {
        // Victoria Day 2025: Monday before May 25 = May 19
        var date = new DateOnly(2025, 5, 19);

        _calendar.IsTradingDay(date).Should().BeFalse("Victoria Day is a TSX holiday");
    }

    [Fact]
    public void IsTradingDay_CanadaDay_ReturnsFalse()
    {
        // 2025-07-01 is a Tuesday
        var date = new DateOnly(2025, 7, 1);

        _calendar.IsTradingDay(date).Should().BeFalse("Canada Day is a TSX holiday");
    }

    [Fact]
    public void IsTradingDay_July4th_ReturnsTrue()
    {
        // 2025-07-04 is a Friday — US holiday, but TSX is open
        var date = new DateOnly(2025, 7, 4);

        _calendar.IsTradingDay(date).Should().BeTrue("July 4th is a US holiday, not a TSX holiday");
    }

    [Fact]
    public void IsTradingDay_ThanksgivingOctober_ReturnsFalse()
    {
        // Canadian Thanksgiving 2025: 2nd Monday of October = Oct 13
        var date = new DateOnly(2025, 10, 13);

        _calendar.IsTradingDay(date).Should().BeFalse("Canadian Thanksgiving is a TSX holiday");
    }

    [Fact]
    public void IsTradingDay_BoxingDay_ReturnsFalse()
    {
        // 2025-12-26 is a Friday
        var date = new DateOnly(2025, 12, 26);

        _calendar.IsTradingDay(date).Should().BeFalse("Boxing Day is a TSX holiday");
    }

    [Fact]
    public void IsTradingDay_FamilyDay_ReturnsFalse()
    {
        // Family Day 2025 is Feb 17 (3rd Monday of February) — TSX holiday
        var date = new DateOnly(2025, 2, 17);

        _calendar.IsTradingDay(date).Should().BeFalse("Family Day is a TSX holiday");
    }

    [Fact]
    public void TradingDaysBetween_FullYear2024_Returns252()
    {
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 12, 31);

        var days = _calendar.TradingDaysBetween(start, end);

        // 262 weekdays - 10 holidays (New Year's, Family Day, Good Friday, Victoria Day,
        // Canada Day, Civic Holiday, Labour Day, Thanksgiving, Christmas, Boxing Day)
        days.Should().HaveCount(252, "TSX has 252 trading days in 2024");
    }

    [Fact]
    public void TradingDaysPerYear_Returns250()
    {
        _calendar.TradingDaysPerYear.Should().Be(250);
    }

    [Fact]
    public void IsTradingDay_CivicHoliday_ReturnsFalse()
    {
        // Civic Holiday 2025: 1st Monday of August = Aug 4
        var date = new DateOnly(2025, 8, 4);

        _calendar.IsTradingDay(date).Should().BeFalse("Civic Holiday is a TSX holiday");
    }

    [Fact]
    public void IsTradingDay_LabourDay_ReturnsFalse()
    {
        // Labour Day 2025: 1st Monday of September = Sep 1
        var date = new DateOnly(2025, 9, 1);

        _calendar.IsTradingDay(date).Should().BeFalse("Labour Day is a TSX holiday");
    }

    [Fact]
    public void IsTradingDay_ChristmasOnSaturday_FridayObserved()
    {
        // 2021-12-25 is Saturday -> Friday Dec 24 observed
        var friday = new DateOnly(2021, 12, 24);

        _calendar.IsTradingDay(friday).Should().BeFalse("Christmas on Saturday is observed on Friday");
    }

    [Fact]
    public void IsTradingDay_BoxingDayOnSunday_MondayObserved()
    {
        // 2021-12-26 is Sunday -> Monday Dec 27 observed
        var monday = new DateOnly(2021, 12, 27);

        _calendar.IsTradingDay(monday).Should().BeFalse("Boxing Day on Sunday is observed on Monday");
    }

    [Fact]
    public void IsTradingDay_CanadaDayOnSaturday_MondayObserved()
    {
        // 2023-07-01 is Saturday -> Monday Jul 3 observed
        var monday = new DateOnly(2023, 7, 3);

        _calendar.IsTradingDay(monday).Should().BeFalse("Canada Day on Saturday is observed on Monday");
    }
}
