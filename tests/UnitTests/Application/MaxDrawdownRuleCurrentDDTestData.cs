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

public static class MaxDrawdownRuleCurrentDDTestData
{
    /// <summary>
    /// Equity: 100 → 80 → 100 (recovered). maxDD=0.15. Current DD = 0%. Should ALLOW.
    /// </summary>
    public static IEnumerable<object[]> AfterRecoveryCases
    {
        get
        {
            yield return
            [
                new SortedDictionary<DateOnly, decimal>
                {
                    [new DateOnly(2026, 1, 1)] = 100m,
                    [new DateOnly(2026, 1, 2)] = 80m,
                    [new DateOnly(2026, 1, 3)] = 100m,
                },
                0.15m,
                true // expected: allowed
            ];
        }
    }

    /// <summary>
    /// Equity: 100 → 80. maxDD=0.15. Current DD = 20%. Should REJECT.
    /// </summary>
    public static IEnumerable<object[]> InCurrentDrawdownCases
    {
        get
        {
            yield return
            [
                new SortedDictionary<DateOnly, decimal>
                {
                    [new DateOnly(2026, 1, 1)] = 100m,
                    [new DateOnly(2026, 1, 2)] = 80m,
                },
                0.15m,
                false // expected: rejected
            ];
        }
    }

    /// <summary>
    /// Equity: 100 → 90. maxDD=0.15. Current DD = 10%. Should ALLOW.
    /// </summary>
    public static IEnumerable<object[]> WithinThresholdCases
    {
        get
        {
            yield return
            [
                new SortedDictionary<DateOnly, decimal>
                {
                    [new DateOnly(2026, 1, 1)] = 100m,
                    [new DateOnly(2026, 1, 2)] = 90m,
                },
                0.15m,
                true // expected: allowed
            ];
        }
    }
}
