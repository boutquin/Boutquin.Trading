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
namespace Boutquin.Trading.UnitTests.Domain;

using DataAccess.Entities;
using DataAccess.Extensions;

/// <summary>
/// Contains unit tests for the <see cref="ExchangeExtensions"/> methods.
/// </summary>
public sealed class ExchangeExtensionsTests
{
    /// <summary>
    /// Tests the <see cref="ExchangeExtensions.IsOpen(Exchange, DateTime)/> method with various scenarios.
    /// </summary>
    /// <param name="exchange">The exchange instance to test.</param>
    /// <param name="dateTime">The date and time to check if the exchange is open.</param>
    /// <param name="expectedResult">The expected result of the method.</param>
    [Theory]
    [MemberData(nameof(ExchangeExtensionsTestData.IsExchangeOpenData), MemberType = typeof(ExchangeExtensionsTestData))]
    public void IsExchangeOpen_WithVariousScenarios_ReturnsExpectedResult(
        Exchange exchange,
        DateOnly dateTime,
        bool expectedResult)
    {
        // Act
        var result = exchange.IsOpen(dateTime);

        // Assert
        result.Should().Be(expectedResult);
    }

    /// <summary>
    /// Tests the <see cref="ExchangeExtensions.GetClosingTime(Exchange, DateTime, int)"/> method with various scenarios.
    /// </summary>
    /// <param name="exchange">The exchange instance to test.</param>
    /// <param name="date">The date to get the exchange closing time.</param>
    /// <param name="closedMinutes">The number of minutes the exchange is closed early.</param>
    /// <param name="expectedResult">The expected result of the method.</param>
    [Theory]
    [MemberData(nameof(ExchangeExtensionsTestData.GetExchangeClosingTimeData), MemberType = typeof(ExchangeExtensionsTestData))]
    public void GetExchangeClosingTime_WithVariousScenarios_ReturnsExpectedResult(
        Exchange exchange,
        DateTime date,
        int closedMinutes,
        DateTime? expectedResult)
    {
        // Act
        var result = exchange.GetClosingTime(date, closedMinutes);

        // Assert
        result.Should().Be(expectedResult);
    }
}
