// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.Tests.UnitTests.Domain;

public sealed class EquityCurveExtensionsTestData
{
    /// <summary>
    /// Gets a collection of test cases containing varied equity curves and their corresponding drawdown analysis results.
    /// </summary>
    public static IEnumerable<object[]> DrawdownAnalysisData => new List<object[]>
    {
        new object[]
        {
            new SortedDictionary<DateOnly, decimal>
            {
                { DateOnly.Parse("2021-01-01"), 1000m },
                { DateOnly.Parse("2021-01-02"), 1020m },
                { DateOnly.Parse("2021-01-03"), 1010m },
                { DateOnly.Parse("2021-01-04"), 1030m },
            },
            new SortedDictionary<DateOnly, decimal>
            {
                { DateOnly.Parse("2021-01-01"), 0m },
                { DateOnly.Parse("2021-01-02"), 0m },
                { DateOnly.Parse("2021-01-03"), -0.0098039215686274509803921569m },
                { DateOnly.Parse("2021-01-04"), 0m },
            },
            -0.0098039215686274509803921569M,
            2
        },
        new object[]
        {
            new SortedDictionary<DateOnly, decimal>
            {
                { DateOnly.Parse("2021-01-01"), 1000m },
                { DateOnly.Parse("2021-01-02"), 980m },
                { DateOnly.Parse("2021-01-03"), 960m },
                { DateOnly.Parse("2021-01-04"), 1020m },
            },
            new SortedDictionary<DateOnly, decimal>
            {
                { DateOnly.Parse("2021-01-01"), 0m },
                { DateOnly.Parse("2021-01-02"), -0.0200000000000000000000000000m },
                { DateOnly.Parse("2021-01-03"), -0.0400000000000000000000000000m },
                { DateOnly.Parse("2021-01-04"), 0m },
            },
            -0.0400000000000000000000000000m,
            3
        }
    };
}
