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

namespace Boutquin.Trading.Domain.Interfaces;

/// <summary>
/// Defines a trading calendar for a specific market or composite of markets.
/// </summary>
public interface ITradingCalendar
{
    /// <summary>
    /// Determines whether the given date is a trading day.
    /// </summary>
    /// <param name="date">The date to check.</param>
    /// <returns><c>true</c> if the market is open on the given date; otherwise, <c>false</c>.</returns>
    bool IsTradingDay(DateOnly date);

    /// <summary>
    /// Returns the next trading day strictly after the given date.
    /// </summary>
    /// <param name="date">The reference date.</param>
    /// <returns>The next date on which the market is open.</returns>
    DateOnly NextTradingDay(DateOnly date);

    /// <summary>
    /// Returns the most recent trading day strictly before the given date.
    /// </summary>
    /// <param name="date">The reference date.</param>
    /// <returns>The previous date on which the market was open.</returns>
    DateOnly PreviousTradingDay(DateOnly date);

    /// <summary>
    /// Returns all trading days in the inclusive range [start, end].
    /// </summary>
    /// <param name="start">The start date (inclusive).</param>
    /// <param name="end">The end date (inclusive).</param>
    /// <returns>An ordered list of trading days in the range.</returns>
    IReadOnlyList<DateOnly> TradingDaysBetween(DateOnly start, DateOnly end);

    /// <summary>
    /// Gets the conventional number of trading days per year for this market.
    /// </summary>
    int TradingDaysPerYear { get; }
}
