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

/// <summary>
/// Provides test data for the <see cref="DecimalArrayExtensionsTests"/> class.
/// </summary>
public sealed class DecimalArrayExtensionsTestData
{
    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays and their corresponding daily volatility values.
    /// </summary>
    public static IEnumerable<object[]> VolatilityData =>
    [
        [new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m }, 0.0207364413533277m],
        [new[] { 0.05m, -0.02m, 0.03m, -0.01m, 0.01m }, 0.0286356421265527m],
        [new[] { 0.1m, -0.05m, 0.2m, -0.1m, 0.15m }, 0.129421791055448m]
    ];

    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays and their corresponding annualized volatility values.
    /// </summary>
    public static IEnumerable<object[]> AnnualizedVolatilityData =>
    [
        [new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m }, 0.3291808013842836299501026838m],
        [new[] { 0.05m, -0.02m, 0.03m, -0.01m, 0.01m }, 0.4545767261970181149375773712m],
        [new[] { 0.1m, -0.05m, 0.2m, -0.1m, 0.15m }, 2.0545072401916686621879541000m]
    ];

    /// <summary>
    /// Gets an array of test data and their corresponding result for the <see cref="DecimalExtensions.AnnualizedReturn"/> method.
    /// </summary>
    public static IEnumerable<object[]> AnnualizedReturnData =>
    [
        [new[] { 0.01m, -0.02m, 0.03m, 0.04m, -0.05m }, 252, 0.43991742034859m],
        [new[] { 0.02m, -0.03m, 0.01m, -0.05m, 0.04m, 0.02m, -0.01m, 0.03m }, 252, 1.30658087675057m],
        [new[] { 0.005m, 0.015m, -0.01m, 0.02m, -0.015m, 0.01m }, 365, 3.4295408134865m],
        [new[] { 0.01m, 0.015m, 0.02m, 0.025m, 0.03m }, 260, 170.132009610452m]
    ];

    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays and their corresponding Sharpe Ratio values.
    /// </summary>w decimal[]
    public static IEnumerable<object[]> SharpeRatioData =>
    [
        [new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m }, 0m, 0.289345693302247559290018236m],
        [new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m }, 0.001m, 0.2411214110852062994083485302m],
        [new[] { 0.05m, -0.02m, 0.03m, -0.01m, 0.01m }, 0m, 0.4190581774617470139184156818m]
    ];

    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays and their corresponding Annualized Sharpe Ratio values.
    /// </summary>
    public static IEnumerable<object[]> AnnualizedSharpeRatioData =>
    [
        [new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m }, 0m, 4.5932204844318738515833167967m],
        [new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m }, 0.001m, 3.8276837370265615429860973298m],
        [new[] { 0.05m, -0.02m, 0.03m, -0.01m, 0.01m }, 0m, 6.6523423345905119303163281909m]
    ];

    /// <summary>
    /// Test data for the TestSortinoRatio method.
    /// </summary>
    public static IEnumerable<object[]> SortinoRatioData =>
        [
            [new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m }, 0m, 0.6M],
            [new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m}, 0.001m, 0.4716141736903387242187952006m]
        ];

    /// <summary>
    /// Test data for the TestAnnualizedSortinoRatio method.
    /// </summary>
    public static IEnumerable<object[]> AnnualizedSortinoRatioData =>
        [
            [new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m }, 0m, 252, 9.52470471983250m],
            [new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m }, 0.001m, 252, 7.4866429101471228181206753726m]
        ];

    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays and their corresponding CAGR values.
    /// </summary>
    public static IEnumerable<object[]> CompoundAnnualGrowthRateData =>
    [
        [new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m }, 332.56367192912200m],
        [new[] { 0.05m, -0.02m, 0.03m, -0.01m, 0.01m }, 1764.4127297619500m]
    ];

    /// <summary>
    /// Test data for the TestDownsideDeviation method.
    /// </summary>
    public static IEnumerable<object[]> DownsideDeviationData =>
        [
            [new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m }, 0, 0.01m],
            [new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m }, 0.001m, 0.0106018866245589m]
        ];

    /// <summary>
    /// Gets a collection of test cases containing varied equity curve arrays and their corresponding daily returns arrays.
    /// </summary>
    public static IEnumerable<object[]> DailyReturnsData =>
    [
        [
            new[] { 1000m, 1050m, 1100m, 1070m, 1110m },
            new[] { 0.05m, 0.04761904761904761904761904762m, -0.02727272727272727272727272727m, 0.03738317757009345794392523364m }
        ],
        [
            new[] { 1200m, 1300m, 1250m, 1350m },
            new[] { 0.08333333333333333333333333333m, -0.03846153846153846153846153846m, 0.08m }
        ]
    ];

    /// <summary>
    /// A collection of test cases for the EquityCurve.Compute method.
    /// Each test case includes an array of daily returns, an initial investment value,
    /// and an expected equity curve array.
    /// </summary>
    public static IEnumerable<object[]> EquityCurveData =>
        [
            [
                new[] { 0.02m, -0.01m, 0.03m },
                1000m,
                new[] { 1000m, 1020m, 1009.8000m, 1040.094000m }
            ],
            [
                new[] { 0.05m, 0.03m, -0.02m, -0.04m },
                2000m,
                new[] { 2000m, 2100m, 2163m, 2119.740000m, 2034.95040000m }
            ],
            [
                new[] { 0m, 0m, 0m },
                3000m,
                new[] { 3000m, 3000m, 3000m, 3000m }
            ],
            [
                new[] { -0.02m, 0.01m, 0.05m },
                1000m,
                new[] { 1000m, 980m, 989.80m, 1039.29m }
            ]
        ];

    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays for portfolio and benchmark daily returns
    /// and their corresponding Beta values.
    /// </summary>
    public static IEnumerable<object[]> BetaData =>
    [
        [
            new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m },
            new[] { 0.02m, 0.03m, -0.01m, 0.04m, -0.03m },
            0.7058823529411764705882352941m
        ],
        [
            new[] { 0.05m, -0.02m, 0.03m, -0.01m, 0.01m },
            new[] { 0.03m, -0.01m, 0.02m, -0.005m, 0.005m },
            1.6991150442477876106194690265m
        ],
        [
            new[] { -0.01m, 0.02m, -0.03m, 0.04m, -0.05m },
            new[] { 0.01m, 0.005m, -0.01m, 0.02m, -0.015m },
            2.301204819277108433734939759m
        ]
    ];

    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays for portfolio and benchmark daily returns,
    /// risk-free rate, and Beta values, and their corresponding Alpha values.
    /// </summary>
    public static IEnumerable<object[]> AlphaData =>
    [
        [
            new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m },
            new[] { 0.02m, 0.03m, -0.01m, 0.04m, -0.03m },
            0.001m,
            -0.0013529411764705882352941176m
        ],
        [
            new[] { 0.05m, -0.02m, 0.03m, -0.01m, 0.01m },
            new[] { 0.03m, -0.01m, 0.02m, -0.005m, 0.005m },
            0.002m,
            -0.0001946902654867256637168142m
        ],
        [
            new[] { -0.01m, 0.02m, -0.03m, 0.04m, -0.05m },
            new[] { 0.01m, 0.005m, -0.01m, 0.02m, -0.015m },
            0m,
            -0.0106024096385542168674698795m
        ]
    ];

    /// <summary>
    /// Gets a collection of test cases containing varied decimal arrays representing daily returns and benchmark daily returns, and their corresponding Information Ratio values.
    /// </summary>
    public static IEnumerable<object[]> InformationRatioData =>
    [
        [
            new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m },
            new[] { 0.005m, 0.01m, -0.005m, 0.015m, -0.01m },
            0.289345693302246163941181027m
        ],
        [
            new[] { 0.05m, -0.02m, 0.03m, -0.01m, 0.01m },
            new[] { 0.04m, -0.01m, 0.02m, -0.005m, 0.005m },
            0.2201927530252719998190350754m
        ],
        [
            new[] { 0.015m, 0.03m, -0.015m, 0.045m, -0.03m },
            new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m },
            0.289345693302246163941181027m
        ]
    ];
}
