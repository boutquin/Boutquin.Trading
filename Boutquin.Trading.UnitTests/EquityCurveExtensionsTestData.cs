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

public sealed class EquityCurveExtensionsTestData
{
    /// <summary>
    /// Gets a collection of test cases containing varied equity curves and their corresponding drawdown analysis results.
    /// </summary>
    public static IEnumerable<object[]> DrawdownAnalysisData => new List<object[]>
    {
        new object[]
        {
            new SortedDictionary<DateTime, decimal>
            {
                { new DateTime(2021, 1, 1), 1000m },
                { new DateTime(2021, 1, 2), 1020m },
                { new DateTime(2021, 1, 3), 1010m },
                { new DateTime(2021, 1, 4), 1030m },
            },
            new SortedDictionary<DateTime, decimal>
            {
                { new DateTime(2021, 1, 1), 0m },
                { new DateTime(2021, 1, 2), 0m },
                { new DateTime(2021, 1, 3), -0.0098039215686274509803921569m },
                { new DateTime(2021, 1, 4), 0m },
            },
            -0.0098039215686274509803921569M,
            2
        },
        new object[]
        {
            new SortedDictionary<DateTime, decimal>
            {
                { new DateTime(2021, 1, 1), 1000m },
                { new DateTime(2021, 1, 2), 980m },
                { new DateTime(2021, 1, 3), 960m },
                { new DateTime(2021, 1, 4), 1020m },
            },
            new SortedDictionary<DateTime, decimal>
            {
                { new DateTime(2021, 1, 1), 0m },
                { new DateTime(2021, 1, 2), -0.0200000000000000000000000000m },
                { new DateTime(2021, 1, 3), -0.0400000000000000000000000000m },
                { new DateTime(2021, 1, 4), 0m },
            },
            -0.0400000000000000000000000000m,
            3
        }
    };
}
