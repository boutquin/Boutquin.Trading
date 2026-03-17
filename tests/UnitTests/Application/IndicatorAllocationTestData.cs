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

public static class IndicatorAllocationTestData
{
    public static IEnumerable<object[]> SmaCases
    {
        get
        {
            yield return
            [
                new decimal[] { 1m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m, 10m },
                5,
                8.0m // Average of last 5: (6+7+8+9+10)/5 = 8
            ];
        }
    }

    public static IEnumerable<object[]> EmaCases
    {
        get
        {
            // 10 values with period 5. Seed = avg of first 5 = 3.0.
            // EMA multiplier = 2/(5+1) = 1/3.
            yield return
            [
                new decimal[] { 1m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m, 10m },
                5
            ];
        }
    }

    public static IEnumerable<object[]> RealizedVolCases
    {
        get
        {
            // 10 returns with window 5
            yield return
            [
                new decimal[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, -0.03m, 0.01m, 0.00m, -0.01m, 0.02m },
                5
            ];
        }
    }
}
