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
using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Extensions;

using static Boutquin.Trading.Domain.Extensions.DecimalArrayExtensions;

using ExceptionMessages = Boutquin.Domain.Exceptions.ExceptionMessages;

namespace Boutquin.Trading.UnitTests;
public sealed class DecimalArrayExtensionsTests
{
    private const decimal Precision = 1e-12m;

    /// <summary>
    /// Tests the <see cref="DecimalArrayExtensions.AnnualizedReturn(decimal[], int)"/> method with various valid input data 
    /// and verifies if the correct Annualized Return is returned.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns for the portfolio.</param>
    /// <param name="tradingDaysPerYear">The number of trading days in a year.</param>
    /// <param name="expectedResult">The expected annualized return.</param>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.AnnualizedReturnData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void AnnualizedReturn_ShouldCalculateCorrectly(
        decimal[] dailyReturns,
        int tradingDaysPerYear,
        decimal expectedResult)
    {
        // Act
        var actualAnnualizedReturn = dailyReturns.AnnualizedReturn(tradingDaysPerYear);

        // Assert
        actualAnnualizedReturn.Should().BeApproximately(expectedResult, Precision);
    }

    /// <summary>
    /// Tests the <see cref="DecimalArrayExtensions.SharpeRatio(decimal[], decimal)"/> method with various valid input data 
    /// and verifies if the correct Sharpe Ratio is returned.
    /// </summary>
    /// <param name="dailyReturns">The input array of daily returns.</param>
    /// <param name="riskFreeRate">The daily risk-free rate.</param>
    /// <param name="expectedResult">The expected Sharpe Ratio value.</param>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.SharpeRatioData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void SharpeRatio_ShouldReturnCorrectResult(
        decimal[] dailyReturns, 
        decimal riskFreeRate, 
        decimal expectedResult)
    {
        // Act
        var actualResult = dailyReturns.SharpeRatio(riskFreeRate);

        // Assert
        actualResult.Should().BeApproximately(expectedResult, Precision);
    }

    /// <summary>
    /// Tests the <see cref="DecimalArrayExtensions.AnnualizedSharpeRatio(decimal[], decimal, int)"/> method with various valid input data 
    /// and verifies if the correct Annualized Sharpe Ratio is returned.
    /// </summary>
    /// <param name="dailyReturns">The input array of daily returns.</param>
    /// <param name="riskFreeRate">The daily risk-free rate.</param>
    /// <param name="expectedResult">The expected Annualized Sharpe Ratio value.</param>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.AnnualizedSharpeRatioData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void AnnualizedSharpeRatio_ShouldReturnCorrectResult(
        decimal[] dailyReturns, 
        decimal riskFreeRate, 
        decimal expectedResult)
    {
        // Act
        var actualResult = dailyReturns.AnnualizedSharpeRatio(riskFreeRate);

        // Assert
        actualResult.Should().BeApproximately(expectedResult, Precision);
    }

    /// <summary>
    /// Tests the <see cref="DecimalArrayExtensions.SortinoRatio(decimal[], decimal)"/> method with various valid input data 
    /// and verifies if the correct Sortino Ratio is returned.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns as doubles.</param>
    /// <param name="riskFreeRate">The risk-free rate.</param>
    /// <param name="expectedResult">The expected Sortino ratio.</param>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.SortinoRatioData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void SortinoRatio_ShouldReturnCorrectResult(
        decimal[] dailyReturns, 
        decimal riskFreeRate, 
        decimal expectedResult)
    {
        // Act
        var actualResult = dailyReturns.SortinoRatio(riskFreeRate);

        // Assert
        actualResult.Should().BeApproximately(expectedResult, Precision);
    }

    /// <summary>
    /// Tests the <see cref="DecimalArrayExtensions.AnnualizedSortinoRatio(decimal[], decimal, int)"/> method with various valid input data 
    /// and verifies if the correct Annualized Sortino Ratio is returned.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns as doubles.</param>
    /// <param name="riskFreeRate">The risk-free rate.</param>
    /// <param name="tradingDaysPerYear">The number of trading days per year.</param>
    /// <param name="expectedResult">The expected annualized Sortino ratio.</param>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.AnnualizedSortinoRatioData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void AnnualizedSortinoRatio_ShouldReturnCorrectResult(
        decimal[] dailyReturns, 
        decimal riskFreeRate, 
        int tradingDaysPerYear, 
        decimal expectedResult)
    {
        // Act
        var actualResult = dailyReturns.AnnualizedSortinoRatio(riskFreeRate, tradingDaysPerYear);

        // Assert
        actualResult.Should().BeApproximately(expectedResult, Precision);
    }

    /// <summary>
    /// Tests the <see cref="DecimalArrayExtensions.DownsideDeviation(decimal[], decimal)"/> method with various valid input data 
    /// and verifies if the correct Downside Deviation is returned.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns as doubles.</param>
    /// <param name="riskFreeRate">The risk-free rate.</param>
    /// <param name="expectedResult">The expected downside deviation.</param>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.DownsideDeviationData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void DownsideDeviation_ShouldReturnCorrectResult(
        decimal[] dailyReturns, 
        decimal riskFreeRate, 
        decimal expectedResult)
    {
        // Act
        var actualResult = dailyReturns.DownsideDeviation(riskFreeRate);

        // Assert
        actualResult.Should().BeApproximately(expectedResult, Precision);
    }

    /// <summary>
    /// Tests the <see cref="DecimalArrayExtensions.EquityCurve(decimal[], decimal)"/> method with various valid input data 
    /// and verifies if the correct EquityCurve is returned.
    /// </summary>
    /// <param name="dailyReturns">An array of daily returns for the test case.</param>
    /// <param name="initialInvestment">The initial investment value for the test case.</param>
    /// <param name="expectedEquityCurve">The expected equity curve array for the test case.</param>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.EquityCurveData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void EquityCurve_ShouldReturnCorrectResult(
        decimal[] dailyReturns, 
        decimal initialInvestment, 
        decimal[] expectedEquityCurve)
    {
        // Act
        var actualEquityCurve = dailyReturns.EquityCurve(initialInvestment);

        // Assert
        actualEquityCurve.Should().BeEquivalentTo(expectedEquityCurve, options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Tests the <see cref="NegativeTradingDaysPerYearException" /> for all extension methods 
    /// with negative trading days per year.
    /// </summary>
    [Fact]
    public void AllMethods_ShouldThrowNegativeTradingDaysPerYearException_WhenTradingDaysPerYearIsNegative()
    {
        // Arrange
        var dailyReturns = new decimal[] { 0.01m, 0.02m };
        var riskFreeRate = 0.0m;
        var tradingDaysPerYear = -1;
        var exceptionType = typeof(NegativeTradingDaysPerYearException);
        var exceptionMessage = Domain.Exceptions.ExceptionMessages.NegativeTradingDaysPerYear;

        // Act & Assert
        Assert.Throws(exceptionType, () => dailyReturns.AnnualizedReturn(tradingDaysPerYear)).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.AnnualizedSharpeRatio(riskFreeRate, tradingDaysPerYear)).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.AnnualizedSortinoRatio(riskFreeRate, tradingDaysPerYear)).Message.Should().Be(exceptionMessage);
    }

    /// <summary>
    /// Tests the <see cref="InsufficientDataException" /> for all extension methods 
    /// that require <see cref="Boutquin.Domain.Extensions.DecimalArrayExtensions.CalculationType.Sample" /> calculations 
    /// with an input array containing only one element.
    /// </summary>
    [Fact]
    public void AllMethods_ShouldThrowInsufficientDataForSampleCalculation_WhenArrayHasOneElement()
    {
        // Arrange
        var dailyReturns = new decimal[] { 0.01m };
        var exceptionType = typeof(InsufficientDataException);
        var exceptionMessage = ExceptionMessages.InsufficientDataForSampleCalculation;

        // Act & Assert
        Assert.Throws(exceptionType, () => dailyReturns.SharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.AnnualizedSharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.SortinoRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.AnnualizedSortinoRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.DownsideDeviation()).Message.Should().Be(exceptionMessage);
    }

    /// <summary>
    /// Tests the <see cref="EmptyOrNullArrayException" /> for all extension methods with null input arrays.
    /// </summary>
    [Fact]
    public void AllMethods_ShouldThrowEmptyOrNullArrayException_WhenArrayIsNull()
    {
        // Arrange
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        decimal[] dailyReturns = null;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
        var exceptionType = typeof(EmptyOrNullArrayException);
        var exceptionMessage = ExceptionMessages.EmptyOrNullArray;

        // Act & Assert
#pragma warning disable CS8604 // Possible null reference argument.
        Assert.Throws(exceptionType, () => dailyReturns.AnnualizedReturn()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.SharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.AnnualizedSharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.SortinoRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.AnnualizedSortinoRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.DownsideDeviation()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.EquityCurve()).Message.Should().Be(exceptionMessage);
#pragma warning restore CS8604 // Possible null reference argument.
    }

    /// <summary>
    /// Tests the <see cref="EmptyOrNullArrayException" /> for all extension methods with empty input arrays.
    /// </summary>
    [Fact]
    public void AllMethods_ShouldThrowEmptyOrNullArrayException_WhenArrayIsEmpty()
    {
        // Arrange
        var dailyReturns = Array.Empty<decimal>();
        var exceptionType = typeof(EmptyOrNullArrayException);
        var exceptionMessage = ExceptionMessages.EmptyOrNullArray;

        // Act & Assert
        Assert.Throws(exceptionType, () => dailyReturns.AnnualizedReturn()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.SharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.AnnualizedSharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.SortinoRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.AnnualizedSortinoRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.DownsideDeviation()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => dailyReturns.EquityCurve()).Message.Should().Be(exceptionMessage);
    }
}
