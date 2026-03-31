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
/// Provides test data for the <see cref="R2DomainReviewFixesTests"/> class.
/// </summary>
public sealed class R2DomainReviewFixesTestData
{
    /// <summary>
    /// R2D-01: CAGR should return raw decimal, not percentage.
    /// 3-year equivalent series: 5 daily returns with known CAGR.
    /// Returns: [0.01, 0.02, -0.01, 0.03, -0.02] → cumulative = 1.01*1.02*0.99*1.03*0.98 = 1.029485...
    /// CAGR = (1.029485...)^(252/5) - 1 ≈ 3.3256... (raw decimal, NOT 332.56%)
    /// </summary>
    public static IEnumerable<object[]> CagrRawDecimalData =>
    [
        [
            new[] { 0.01m, 0.02m, -0.01m, 0.03m, -0.02m },
            3.3256367192912200m // was 332.56... when multiplied by 100
        ]
    ];

    /// <summary>
    /// R2D-02: DownsideDeviation with all returns above risk-free rate should return 0.
    /// </summary>
    public static IEnumerable<object[]> DownsideDeviationZeroData =>
    [
        [new[] { 0.05m, 0.03m, 0.04m, 0.02m, 0.06m }, 0.01m, 0m]
    ];

    /// <summary>
    /// R2D-02: DownsideDeviation with some returns below risk-free rate returns positive.
    /// </summary>
    public static IEnumerable<object[]> DownsideDeviationPositiveData =>
    [
        [new[] { 0.05m, -0.02m, 0.03m, -0.01m, 0.04m }, 0.01m]
    ];

    /// <summary>
    /// R2D-03: SortinoRatio with all returns above risk-free (zero downside deviation).
    /// </summary>
    public static IEnumerable<object[]> SortinoRatioZeroDownsideData =>
    [
        [new[] { 0.05m, 0.03m, 0.04m, 0.02m, 0.06m }, 0.01m]
    ];

    /// <summary>
    /// R2D-03: SortinoRatio with normal (non-zero) downside deviation.
    /// </summary>
    public static IEnumerable<object[]> SortinoRatioNormalData =>
    [
        [new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m }, 0m]
    ];
}
