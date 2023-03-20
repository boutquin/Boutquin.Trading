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

using Boutquin.Trading.Domain.Exceptions;
using static Boutquin.Trading.Domain.Extensions.DecimalArrayExtensions;

namespace Boutquin.Trading.UnitTests;

// DecimalArrayExtensionsTestData.cs
public sealed class DecimalArrayExtensionsTestData
{
    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays and their corresponding average values.
    /// </summary>
    public static IEnumerable<object[]> AverageData => new List<object[]>
    {
        new object[] { new decimal[] { 1m, 2m, 3m, 4m, 5m }, 3m },
        new object[] { new decimal[] { 0m, 0m, 0m, 0m, 0m }, 0m },
        new object[] { new decimal[] { -1m, 1m, -2m, 2m, -3m, 3m }, 0m },
        new object[] { new decimal[] { 100m, 50m, 25m, 12.5m }, 46.875m },
        new object[] { new decimal[] { 0.5m, 0.25m, 0.125m, 0.0625m }, 0.234375m },
        new object[] { new decimal[] { -5m, -10m, -15m, -20m, -25m }, -15m },
        new object[] { new decimal[] { 12345m, 67890m }, 40117.5m }
    };

    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays and their corresponding variance values.
    /// </summary>
    public static IEnumerable<object[]> VarianceData =>
        new List<object[]>
        {
        // CalculationType.Population
        new object[] { new decimal[] { 1m, 2m, 3m, 4m, 5m }, CalculationType.Population, 2m },
        new object[] { new decimal[] { 10m, 20m, 30m, 40m, 50m }, CalculationType.Population, 200m },
        new object[] { new decimal[] { -5m, 0m, 5m, 10m, 15m }, CalculationType.Population, 50m },
        new object[] { new decimal[] { 0.01m, 0.02m, 0.03m, 0.04m, 0.05m }, CalculationType.Population, 0.0002m },
        new object[] { new decimal[] { -1000m, 0m, 1000m }, CalculationType.Population, 666666.6666666666666666667m },
        new object[] { new decimal[] { 100m, 200m, 300m, 400m, 500m, 600m, 700m }, CalculationType.Population, 40000m  },

        // CalculationType.Sample
        new object[] { new decimal[] { 1m, 2m, 3m, 4m, 5m }, CalculationType.Sample, 2.5m },
        new object[] { new decimal[] { 10m, 20m, 30m, 40m, 50m }, CalculationType.Sample, 250m },
        new object[] { new decimal[] { -5m, 0m, 5m, 10m, 15m }, CalculationType.Sample, 62.5m },
        new object[] { new decimal[] { 0.01m, 0.02m, 0.03m, 0.04m, 0.05m }, CalculationType.Sample, 0.00025m },
        new object[] { new decimal[] { -1000m, 0m, 1000m }, CalculationType.Sample, 1000000m },
        new object[] { new decimal[] { 100m, 200m, 300m, 400m, 500m, 600m, 700m }, CalculationType.Sample, 46666.666666666666666666666667m },
        };

    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays and their corresponding standard deviation values.
    /// </summary>
    public static IEnumerable<object[]> StandardDeviationData =>
        new List<object[]>
        {
        // CalculationType.Population
        new object[] { new decimal[] { 1m, 2m, 3m, 4m, 5m }, CalculationType.Population, 1.414213562373095m },
        new object[] { new decimal[] { 10m, 20m, 30m, 40m, 50m }, CalculationType.Population, 14.14213562373095m },
        new object[] { new decimal[] { -5m, 0m, 5m, 10m, 15m }, CalculationType.Population, 7.0710678118654755m },
        new object[] { new decimal[] { 0.01m, 0.02m, 0.03m, 0.04m, 0.05m }, CalculationType.Population, 0.0141421356237m },
        new object[] { new decimal[] { -1000m, 0m, 1000m }, CalculationType.Population, 816.4965809277261m },
        new object[] { new decimal[] { 100m, 200m, 300m, 400m, 500m, 600m, 700m }, CalculationType.Population, 200m },

        // CalculationType.Sample
        new object[] { new decimal[] { 1m, 2m, 3m, 4m, 5m }, CalculationType.Sample, 1.5811388300841898m },
        new object[] { new decimal[] { 10m, 20m, 30m, 40m, 50m }, CalculationType.Sample, 15.811388300841896m },
        new object[] { new decimal[] { -5m, 0m, 5m, 10m, 15m }, CalculationType.Sample, 7.905694150420948m },
        new object[] { new decimal[] { 0.01m, 0.02m, 0.03m, 0.04m, 0.05m }, CalculationType.Sample, 0.015811388300m },
        new object[] { new decimal[] { -1000m, 0m, 1000m }, CalculationType.Sample, 1000m },
        new object[] { new decimal[] { 100m, 200m, 300m, 400m, 500m, 600m, 700m }, CalculationType.Sample, 216.024689946929m },
        };

    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays and their corresponding Sharpe Ratio values.
    /// </summary>
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
    /// Gets a collection of test cases containing empty decimal arrays.
    /// </summary>
    public static IEnumerable<object[]> EmptyArrays => new List<object[]>
    {
        new object[] { Array.Empty<decimal>() }
    };
}
