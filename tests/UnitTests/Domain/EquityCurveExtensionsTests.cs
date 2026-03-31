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

namespace Boutquin.Trading.Tests.UnitTests.Domain;

/// <summary>
/// Contains unit tests for the <see cref="EquityCurveExtensions"/> methods.
/// </summary>
public sealed class EquityCurveExtensionsTests
{
    private const decimal Precision = 1e-12m;

    /// <summary>
    /// Tests the <see cref="EquityCurveExtensions.CalculateDrawdownsAndMaxDrawdownInfo"/> method with various valid input data
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

    [Theory]
    [MemberData(nameof(ExtendedMetricsTestData.MonthlyReturnsData), MemberType = typeof(ExtendedMetricsTestData))]
    public void MonthlyReturns_ShouldReturnCorrectValues(
        SortedDictionary<DateOnly, decimal> equityCurve,
        SortedDictionary<(int, int), decimal> expectedReturns)
    {
        var actual = equityCurve.MonthlyReturns();
        actual.Should().HaveCount(expectedReturns.Count);
        foreach (var (key, value) in expectedReturns)
        {
            actual.Should().ContainKey(key);
            actual[key].Should().BeApproximately(value, Precision);
        }
    }

    [Theory]
    [MemberData(nameof(ExtendedMetricsTestData.AnnualReturnsData), MemberType = typeof(ExtendedMetricsTestData))]
    public void AnnualReturns_ShouldReturnCorrectValues(
        SortedDictionary<DateOnly, decimal> equityCurve,
        SortedDictionary<int, decimal> expectedReturns)
    {
        var actual = equityCurve.AnnualReturns();
        actual.Should().HaveCount(expectedReturns.Count);
        foreach (var (key, value) in expectedReturns)
        {
            actual.Should().ContainKey(key);
            actual[key].Should().BeApproximately(value, Precision);
        }
    }

    [Fact]
    public void MonthlyReturns_SingleDay_ReturnsEmpty()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2023, 1, 15), 1000m }
        };
        var result = equityCurve.MonthlyReturns();
        result.Should().BeEmpty();
    }

    [Fact]
    public void AnnualReturns_SingleYear_ReturnsEmpty()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2023, 1, 1), 1000m },
            { new DateOnly(2023, 12, 31), 1100m }
        };
        var result = equityCurve.AnnualReturns();
        result.Should().BeEmpty();
    }
}

