// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  you may not use this file except in compliance with the License.
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

using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Extensions;

using static Boutquin.Trading.Domain.Extensions.DecimalArrayExtensions;

namespace Boutquin.Trading.UnitTests;
public class DecimalArrayExtensionsTests
{
        /// <summary>
    /// Tests the Average method using test data provided by AverageData.
    /// </summary>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.AverageData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void Average_ShouldCalculateCorrectly(decimal[] values, decimal expected)
    {
        // Act
        var result = values.Average();

        // Assert
        result.Should().BeApproximately(expected, 1e-12m);
    }

    /// <summary>
    /// Tests the Variance method using test data provided by VarianceData.
    /// </summary>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.VarianceData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void Variance_ShouldCalculateCorrectly(decimal[] values, CalculationType calculationType, decimal expected)
    {
        // Act
        var result = values.Variance(calculationType);

        // Assert
        result.Should().BeApproximately(expected, 1e-12m);
    }

    /// <summary>
    /// Tests the StandardDeviation method using test data provided by StandardDeviationData.
    /// </summary>
    [Theory]
    [MemberData(nameof(DecimalArrayExtensionsTestData.StandardDeviationData), MemberType = typeof(DecimalArrayExtensionsTestData))]
    public void StandardDeviation_ShouldCalculateCorrectly(decimal[] values, CalculationType calculationType, decimal expected)
    {
        // Act
        var result = values.StandardDeviation(calculationType);

        // Assert
        result.Should().BeApproximately(expected, 1e-12m);
    }

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
    /// Tests the InsufficientDataForSampleCalculation for all extension methods that require sample calculations with an input array containing only one element.
    /// </summary>
    [Fact]
    public void AllMethods_ShouldThrowInsufficientDataForSampleCalculation_WhenArrayHasOneElement()
    {
        var values = new decimal[] { 0.01m };
        var exceptionType = typeof(InsufficientDataException);
        var exceptionMessage = ExceptionMessages.InsufficientDataForSampleCalculation;

        Assert.Throws(exceptionType, () => values.Variance(CalculationType.Sample)).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.StandardDeviation(CalculationType.Sample)).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.SharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.AnnualizedSharpeRatio()).Message.Should().Be(exceptionMessage);
    }

    /// <summary>
    /// Tests the EmptyOrNullArrayException for all extension methods with null input arrays.
    /// </summary>
    [Fact]
    public void AllMethods_ShouldThrowEmptyOrNullArrayException_WhenArrayIsNull()
    {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        decimal[] values = null;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
        var exceptionType = typeof(EmptyOrNullArrayException);
        var exceptionMessage = ExceptionMessages.EmptyOrNullArray;

#pragma warning disable CS8604 // Possible null reference argument.
        Assert.Throws(exceptionType, () => values.Average()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.Variance()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.StandardDeviation()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.SharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.AnnualizedSharpeRatio()).Message.Should().Be(exceptionMessage);
#pragma warning restore CS8604 // Possible null reference argument.
    }

    /// <summary>
    /// Tests the EmptyOrNullArrayException for all extension methods with empty input arrays.
    /// </summary>
    [Fact]
    public void AllMethods_ShouldThrowEmptyOrNullArrayException_WhenArrayIsEmpty()
    {
        var values = Array.Empty<decimal>();
        var exceptionType = typeof(EmptyOrNullArrayException);
        var exceptionMessage = ExceptionMessages.EmptyOrNullArray;

        Assert.Throws(exceptionType, () => values.Average()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.Variance()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.StandardDeviation()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.SharpeRatio()).Message.Should().Be(exceptionMessage);
        Assert.Throws(exceptionType, () => values.AnnualizedSharpeRatio()).Message.Should().Be(exceptionMessage);
    }
}
