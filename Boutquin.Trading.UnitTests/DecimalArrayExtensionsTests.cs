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

using Boutquin.Domain.Exceptions;
using Boutquin.Trading.Domain.Extensions;
using static Boutquin.Trading.Domain.Extensions.DecimalArrayExtensions;

namespace Boutquin.Trading.UnitTests;
public sealed class DecimalArrayExtensionsTests
{
    /// <summary>
    /// Tests the SharpeRatio method with various input arrays and verifies if the correct Sharpe Ratio is returned.
    /// </summary>
    /// <param name="values">The input array of daily returns.</param>
    /// <param name="riskFreeRate">The daily risk-free rate.</param>
    /// <param name="expectedResult">The expected Sharpe Ratio value.</param>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.SharpeRatioData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void SharpeRatio_ShouldReturnCorrectResult(decimal[] values, decimal riskFreeRate, decimal expectedResult)
    {
        // Act
        var result = values.SharpeRatio(riskFreeRate);

        // Assert
        result.Should().BeApproximately(expectedResult, 1e-12m);
    }

    /// <summary>
    /// Tests the AnnualizedSharpeRatio method with various input arrays and verifies if the correct Annualized Sharpe Ratio is returned.
    /// </summary>
    /// <param name="values">The input array of daily returns.</param>
    /// <param name="riskFreeRate">The daily risk-free rate.</param>
    /// <param name="expectedResult">The expected Annualized Sharpe Ratio value.</param>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.AnnualizedSharpeRatioData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void AnnualizedSharpeRatio_ShouldReturnCorrectResult(decimal[] values, decimal riskFreeRate, decimal expectedResult)
    {
        // Act
        var result = values.AnnualizedSharpeRatio(riskFreeRate);

        // Assert
        result.Should().BeApproximately(expectedResult, 1e-12m);
    }

    /// <summary>
    /// Tests the SortinoRatio method with various inputs.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns as doubles.</param>
    /// <param name="riskFreeRate">The risk-free rate.</param>
    /// <param name="expectedSortinoRatio">The expected Sortino ratio.</param>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.SortinoRatioData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void TestSortinoRatio(decimal[] dailyReturns, decimal riskFreeRate, decimal expectedSortinoRatio)
    {
        // Act
        var actualSortinoRatio = dailyReturns.SortinoRatio((decimal)riskFreeRate);

        // Assert
        actualSortinoRatio.Should().BeApproximately((decimal)expectedSortinoRatio, 1e-12m);
    }

    /// <summary>
    /// Tests the AnnualizedSortinoRatio method with various inputs.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns as doubles.</param>
    /// <param name="riskFreeRate">The risk-free rate.</param>
    /// <param name="tradingDaysPerYear">The number of trading days per year.</param>
    /// <param name="expectedAnnualizedSortinoRatio">The expected annualized Sortino ratio.</param>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.AnnualizedSortinoRatioData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void TestAnnualizedSortinoRatio(decimal[] dailyReturns, decimal riskFreeRate, int tradingDaysPerYear, decimal expectedAnnualizedSortinoRatio)
    {
        // Act
        var actualAnnualizedSortinoRatio = dailyReturns.AnnualizedSortinoRatio(riskFreeRate, tradingDaysPerYear);

        // Assert
        actualAnnualizedSortinoRatio.Should().BeApproximately(expectedAnnualizedSortinoRatio, 1e-12m);
    }

    /// <summary>
    /// Tests the DownsideDeviation method with various inputs.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns as doubles.</param>
    /// <param name="riskFreeRate">The risk-free rate.</param>
    /// <param name="expectedDownsideDeviation">The expected downside deviation.</param>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.DownsideDeviationData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void TestDownsideDeviation(decimal[] dailyReturns, decimal riskFreeRate, decimal expectedDownsideDeviation)
    {
        // Act
        var actualDownsideDeviation = dailyReturns.DownsideDeviation(riskFreeRate);

        // Assert
        actualDownsideDeviation.Should().BeApproximately(expectedDownsideDeviation, 1e-12m);
    }

    /// <summary>
    /// Tests the InsufficientDataForSampleCalculation for all extension methods that require sample calculations with an input array containing only one element.
    /// </summary>
    [Fact]
    public void AllMethods_ShouldThrowInsufficientDataForSampleCalculation_WhenArrayHasOneElement()
    {
        // Arrange
        var values = new decimal[] { 0.01m };
        var exceptionType = typeof(InsufficientDataException);
        var exceptionMessage = ExceptionMessages.InsufficientDataForSampleCalculation;

        // Act & Assert
        Assert.Throws(exceptionType, () => values.SharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.AnnualizedSharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.SortinoRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.AnnualizedSortinoRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.DownsideDeviation()).Message.Should().Be(exceptionMessage);
    }

    /// <summary>
    /// Tests the EmptyOrNullArrayException for all extension methods with null input arrays.
    /// </summary>
    [Fact]
    public void AllMethods_ShouldThrowEmptyOrNullArrayException_WhenArrayIsNull()
    {
        // Arrange
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        decimal[] values = null;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
        var exceptionType = typeof(EmptyOrNullArrayException);
        var exceptionMessage = ExceptionMessages.EmptyOrNullArray;

        // Act & Assert
#pragma warning disable CS8604 // Possible null reference argument.
        Assert.Throws(exceptionType, () => values.SharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.AnnualizedSharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.SortinoRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.AnnualizedSortinoRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.DownsideDeviation()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.EquityCurve()).Message.Should().Be(exceptionMessage);
#pragma warning restore CS8604 // Possible null reference argument.
    }

    /// <summary>
    /// Tests the EmptyOrNullArrayException for all extension methods with empty input arrays.
    /// </summary>
    [Fact]
    public void AllMethods_ShouldThrowEmptyOrNullArrayException_WhenArrayIsEmpty()
    {
        // Arrange
        var values = Array.Empty<decimal>();
        var exceptionType = typeof(EmptyOrNullArrayException);
        var exceptionMessage = ExceptionMessages.EmptyOrNullArray;

        // Act & Assert
        Assert.Throws(exceptionType, () => values.SharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.AnnualizedSharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.SortinoRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.AnnualizedSortinoRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.DownsideDeviation()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.EquityCurve()).Message.Should().Be(exceptionMessage);
    }
}
