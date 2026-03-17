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

namespace Boutquin.Trading.Tests.UnitTests.Application;

public static class CorrelationAnalyzerValidationTestData
{
    private static readonly Asset s_a = new("A");
    private static readonly Asset s_b = new("B");

    public static IEnumerable<object[]> DimensionMismatch_ReturnsVsAssets
    {
        get
        {
            // 2 assets but 3 return series
            yield return
            [
                new List<Asset> { s_a, s_b },
                new[] { new[] { 0.01m, 0.02m }, new[] { 0.01m, 0.02m }, new[] { 0.01m, 0.02m } },
                new[] { 0.5m, 0.5m }
            ];
        }
    }

    public static IEnumerable<object[]> DimensionMismatch_WeightsVsAssets
    {
        get
        {
            // 2 assets but 3 weights
            yield return
            [
                new List<Asset> { s_a, s_b },
                new[] { new[] { 0.01m, 0.02m }, new[] { 0.01m, 0.02m } },
                new[] { 0.3m, 0.3m, 0.4m }
            ];
        }
    }

    public static IEnumerable<object[]> InsufficientObservations
    {
        get
        {
            // 2 assets with only 1 observation each (need >= 2 for N-1 divisor)
            yield return
            [
                new List<Asset> { s_a, s_b },
                new[] { new[] { 0.01m }, new[] { 0.02m } },
                new[] { 0.5m, 0.5m }
            ];
        }
    }
}
