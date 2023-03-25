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

public sealed class DecimalArrayExtensionsTestData
{
    /// <summary>
    /// Gets an array of test data and their corresponding result for the <see cref="DecimalExtensions.AnnualizedReturn"/> method.
    /// </summary>
    public static IEnumerable<object[]> AnnualizedReturnData => new List<object[]>
    {
        new object[]{ new decimal[] { 0.01m, -0.02m, 0.03m, 0.04m, -0.05m }, 252, 0.43991742034859m },
        new object[]{ new decimal[] { 0.02m, -0.03m, 0.01m, -0.05m, 0.04m, 0.02m, -0.01m, 0.03m }, 252, 1.30658087675057m },
        new object[]{ new decimal[] { 0.005m, 0.015m, -0.01m, 0.02m, -0.015m, 0.01m }, 365, 3.4295408134865m },
        new object[]{ new decimal[] { 0.01m, 0.015m, 0.02m, 0.025m, 0.03m }, 260, 170.132009610452m }
    };

    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays and their corresponding Sharpe Ratio values.
    /// </summary>w decimal[]
    public static IEnumerable<object[]> SharpeRatioData => new List<object[]>
    {
        new object[] { new decimal[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m }, 0m, 0.289345693302247559290018236m },
        new object[] { new decimal[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m }, 0.001m, 0.2411214110852062994083485302m },
        new object[] { new decimal[] { 0.05m, -0.02m, 0.03m, -0.01m, 0.01m }, 0m, 0.4190581774617470139184156818m }
    };

    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays and their corresponding Annualized Sharpe Ratio values.
    /// </summary>
    public static IEnumerable<object[]> AnnualizedSharpeRatioData => new List<object[]>
    {
        new object[] { new decimal[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m }, 0m, 4.5932204844318738515833167967m },
        new object[] { new decimal[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m }, 0.001m, 3.8276837370265615429860973298m },
        new object[] { new decimal[] { 0.05m, -0.02m, 0.03m, -0.01m, 0.01m }, 0m, 6.6523423345905119303163281909m }
    };

    /// <summary>
    /// Test data for the TestSortinoRatio method.
    /// </summary>
    public static IEnumerable<object[]> SortinoRatioData =>
        new List<object[]>
        {
            new object[] { new decimal[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m }, 0m, 0.6M },
            new object[] { new decimal[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m}, 0.001m, 0.4716141736903387242187952006m }
        };

    /// <summary>
    /// Test data for the TestAnnualizedSortinoRatio method.
    /// </summary>
    public static IEnumerable<object[]> AnnualizedSortinoRatioData =>
        new List<object[]>
        {
            new object[] { new decimal[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m }, 0m, 252, 9.52470471983250m },
            new object[] { new decimal[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m }, 0.001m, 252, 7.4866429101471228181206753726m }
        };

    /// <summary>
    /// Test data for the TestDownsideDeviation method.
    /// </summary>
    public static IEnumerable<object[]> DownsideDeviationData =>
        new List<object[]>
        {
            new object[] { new decimal[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m }, 0, 0.01m },
            new object[] { new decimal[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m }, 0.001m, 0.0106018866245589m }
        };

    /// <summary>
    /// A collection of test cases for the EquityCurve.Compute method.
    /// Each test case includes an array of daily returns, an initial investment value,
    /// and an expected equity curve array.
    /// </summary>
    public static IEnumerable<object[]> EquityCurveData => new List<object[]>
        {
            new object[]
            {
                new decimal[] { 0.02m, -0.01m, 0.03m },
                1000m,
                new decimal[] { 1000m, 1020m, 1009.8000m, 1040.094000m }
            },
            new object[]
            {
                new decimal[] { 0.05m, 0.03m, -0.02m, -0.04m },
                2000m,
                new decimal[] { 2000m, 2100m, 2163m, 2119.740000m, 2034.95040000m }
            },
            new object[]
            {
                new decimal[] { 0m, 0m, 0m },
                3000m,
                new decimal[] { 3000m, 3000m, 3000m, 3000m }
            },
            new object[]
            {
                new decimal[] { -0.02m, 0.01m, 0.05m },
                1000m,
                new decimal[] { 1000m, 980m, 989.80m, 1039.29m }
            }
        };
}
