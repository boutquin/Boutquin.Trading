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

public sealed class ExtendedMetricsTestData
{
    /// <summary>
    /// Standard test daily returns with a mix of positive and negative values.
    /// </summary>
    private static readonly decimal[] s_standardReturns =
        [0.01m, 0.02m, -0.01m, 0.03m, -0.02m, 0.015m, -0.005m, 0.025m, -0.015m, 0.01m];

    /// <summary>
    /// OmegaRatio test data: dailyReturns, threshold, expected.
    /// Omega = sum(max(r-threshold, 0)) / sum(max(threshold-r, 0))
    /// For s_standardReturns with threshold=0:
    /// Gains: 0.01+0.02+0.03+0.015+0.025+0.01 = 0.11
    /// Losses: 0.01+0.02+0.005+0.015 = 0.05
    /// Omega = 0.11 / 0.05 = 2.2
    /// </summary>
    public static IEnumerable<object[]> OmegaRatioData =>
    [
        [s_standardReturns, 0m, 2.2m]
    ];

    /// <summary>
    /// WinRate test data: dailyReturns, expected.
    /// Positive returns in s_standardReturns: 0.01, 0.02, 0.03, 0.015, 0.025, 0.01 = 6 out of 10
    /// WinRate = 6/10 = 0.6
    /// </summary>
    public static IEnumerable<object[]> WinRateData =>
    [
        [s_standardReturns, 0.6m],
        [new[] { 0.01m, 0.02m, 0.03m }, 1.0m],
        [new[] { -0.01m, -0.02m, -0.03m }, 0.0m]
    ];

    /// <summary>
    /// ProfitFactor test data: dailyReturns, expected.
    /// GrossProfit = 0.01+0.02+0.03+0.015+0.025+0.01 = 0.11
    /// GrossLoss = |(-0.01)+(-0.02)+(-0.005)+(-0.015)| = 0.05
    /// ProfitFactor = 0.11/0.05 = 2.2
    /// </summary>
    public static IEnumerable<object[]> ProfitFactorData =>
    [
        [s_standardReturns, 2.2m]
    ];

    /// <summary>
    /// Skewness test data: dailyReturns, expected.
    /// For a symmetric distribution, skewness ≈ 0.
    /// Using s_standardReturns, we compute manually or verify the sign/magnitude.
    /// </summary>
    public static IEnumerable<object[]> SkewnessData =>
    [
        // Symmetric returns → skewness ≈ 0
        [new[] { -0.02m, -0.01m, 0m, 0.01m, 0.02m }, 0m]
    ];

    /// <summary>
    /// Kurtosis test data: dailyReturns, expected.
    /// For a uniform-ish distribution, excess kurtosis is negative.
    /// </summary>
    public static IEnumerable<object[]> KurtosisData =>
    [
        // Uniform-like distribution: excess kurtosis is negative
        [new[] { -0.02m, -0.01m, 0m, 0.01m, 0.02m }, -1.2m]
    ];

    /// <summary>
    /// HistoricalVaR test data: dailyReturns, confidenceLevel, expected.
    /// Sorted s_standardReturns: -0.02, -0.015, -0.01, -0.005, 0.01, 0.01, 0.015, 0.02, 0.025, 0.03
    /// VaR at 95%: index = (1-0.95)*(10-1) = 0.45
    /// lower=0 (value=-0.02), upper=1 (value=-0.015)
    /// VaR = -0.02 + 0.45*(-0.015 - (-0.02)) = -0.02 + 0.45*0.005 = -0.02 + 0.00225 = -0.01775
    /// </summary>
    public static IEnumerable<object[]> HistoricalVaRData =>
    [
        [s_standardReturns, 0.95m, -0.01775m]
    ];

    /// <summary>
    /// Data for testing MonthlyReturns on equity curve.
    /// </summary>
    public static IEnumerable<object[]> MonthlyReturnsData =>
    [
        [
            new SortedDictionary<DateOnly, decimal>
            {
                { new DateOnly(2023, 1, 2), 1000m },
                { new DateOnly(2023, 1, 31), 1050m },
                { new DateOnly(2023, 2, 1), 1060m },
                { new DateOnly(2023, 2, 28), 1100m },
                { new DateOnly(2023, 3, 1), 1080m },
                { new DateOnly(2023, 3, 31), 1150m }
            },
            new SortedDictionary<(int, int), decimal>
            {
                // Feb return: 1100/1050 - 1 = 0.047619...
                { (2023, 2), 1100m / 1050m - 1m },
                // Mar return: 1150/1100 - 1 = 0.045454...
                { (2023, 3), 1150m / 1100m - 1m }
            }
        ]
    ];

    /// <summary>
    /// Data for testing AnnualReturns on equity curve.
    /// </summary>
    public static IEnumerable<object[]> AnnualReturnsData =>
    [
        [
            new SortedDictionary<DateOnly, decimal>
            {
                { new DateOnly(2021, 6, 30), 1000m },
                { new DateOnly(2021, 12, 31), 1100m },
                { new DateOnly(2022, 6, 30), 1050m },
                { new DateOnly(2022, 12, 31), 1200m },
                { new DateOnly(2023, 12, 31), 1350m }
            },
            new SortedDictionary<int, decimal>
            {
                // 2022 return: 1200/1100 - 1
                { 2022, 1200m / 1100m - 1m },
                // 2023 return: 1350/1200 - 1
                { 2023, 1350m / 1200m - 1m }
            }
        ]
    ];
}
