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

namespace Boutquin.Trading.UnitTests;
using Boutquin.Trading.Domain.Entities;
using Boutquin.Trading.Domain.Enums;

/// <summary>
/// Provides test data for the <see cref="ExchangeExtensions"/> class.
/// </summary>
public static class ExchangeExtensionsTestData
{
    /// <summary>
    /// Gets test data for the <see cref="ExchangeExtensions.IsExchangeOpen"/> method.
    /// </summary>
    public static IEnumerable<object[]> IsExchangeOpenData
    {
        get
        {
            var exchange = new Exchange();
            exchange.ExchangeSchedules.Add(new ExchangeSchedule(ExchangeCode.XNYS, DayOfWeek.Monday, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0)));
            exchange.ExchangeHolidays.Add(new ExchangeHoliday(ExchangeCode.XNYS, DateOnly.FromDateTime(new DateTime(2023, 1, 1)), "New Year's Day"));

            // Test case 1: Normal trading day, exchange should be open.
            yield return new object[]
            {
                exchange,
                DateOnly.FromDateTime(new DateTime(2023, 1, 2, 12, 0, 0)), // Monday, 12:00 PM
                true
            };

            // Test case 2: Exchange closed on holiday.
            yield return new object[]
            {
                exchange,
                DateOnly.FromDateTime(new DateTime(2023, 1, 1, 12, 0, 0)), // Sunday (holiday), 12:00 PM
                false
            };

            // ... Add more test cases.
        }
    }

    /// <summary>
    /// Gets test data for the <see cref="ExchangeExtensions.GetExchangeClosingTime"/> method.
    /// </summary>
    public static IEnumerable<object[]> GetExchangeClosingTimeData
    {
        get
        {
            var exchange = new Exchange();
            exchange.ExchangeSchedules.Add(new ExchangeSchedule(ExchangeCode.XNYS, DayOfWeek.Monday, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0)));
            exchange.ExchangeHolidays.Add(new ExchangeHoliday(ExchangeCode.XNYS, DateOnly.FromDateTime(new DateTime(2023, 1, 1)), "New Year's Day"));

            // Test case 1: Normal trading day, should return the closing time.
            yield return new object[]
            {
                exchange,
                new DateTime(2023, 1, 2), // Monday
                0, // No additional closed minutes
                new DateTime(2023, 1, 2, 17, 0, 0) // Expected closing time
            };

            // Test case 2: Exchange closed early by 30 minutes.
            yield return new object[]
            {
                exchange,
                new DateTime(2023, 1, 2), // Monday
                30, // Closed 30 minutes early
                new DateTime(2023, 1, 2, 16, 30, 0) // Expected closing time
            };

            // Test case 3: Exchange closed on holiday, should return null.
            yield return new object[]
            {
                exchange,
                new DateTime(2023, 1, 1), // Sunday (holiday)
                0, // No additional closed minutes
                null // Exchange closed on holiday
            };

            // ... Add more test cases.
        }
    }
}
