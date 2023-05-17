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
using Boutquin.Trading.Domain.Extensions;

public sealed class EquityCurveExtensionsTests
{
    private const decimal Precision = 1e-12m;

    /// <summary>
    /// Tests the <see cref="EquityCurveExtensions.CalculateDrawdownsAndMaxDrawdownInfo(SortedDictionary{DateOnly,decimal})"/> method with various valid input data
    /// and verifies if the correct drawdown analysis results are returned.
    /// </summary>
    /// <param name="equityCurve">The input SortedDictionary representing the equity curve.</param>
    /// <param name="expectedDrawdowns">The expected SortedDictionary containing the calculated drawdowns.</param>
    /// <param name="expectedMaxDrawdown">The expected maximum drawdown value.</param>
    /// <param name="expectedMaxDrawdownDuration">The expected maximum drawdown duration in days.</param>
    [Theory]
    [MemberData(nameof(EquityCurveExtensionsTestData.DrawdownAnalysisData), MemberType = typeof(EquityCurveExtensionsTestData))]
    public void CalculateDrawdownsAndMaxDrawdownInfo_ShouldReturnCorrectResults(
        SortedDictionary<DateOnly, decimal> equityCurve,
        SortedDictionary<DateOnly, decimal> expectedDrawdowns,
        decimal expectedMaxDrawdown,
        int expectedMaxDrawdownDuration)
    {
        // Act
        var (actualDrawdowns, actualMaxDrawdown, actualMaxDrawdownDuration) = equityCurve.CalculateDrawdownsAndMaxDrawdownInfo();

        // Assert
        actualDrawdowns.Should().BeEquivalentTo(expectedDrawdowns);
        actualMaxDrawdown.Should().BeApproximately(expectedMaxDrawdown, Precision);
        actualMaxDrawdownDuration.Should().Be(expectedMaxDrawdownDuration);
    }
}

