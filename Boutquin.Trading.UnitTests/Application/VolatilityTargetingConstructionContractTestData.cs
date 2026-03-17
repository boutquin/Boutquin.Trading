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

public static class VolatilityTargetingConstructionContractTestData
{
    private static readonly Asset s_vti = new("VTI");

    /// <summary>
    /// Returns with known ~2% daily vol (annualized ~31.7%).
    /// Target 50% vol, maxLeverage 2.0 → scale factor ~1.58 → weights sum > 1.0.
    /// </summary>
    public static IEnumerable<object[]> ScaleUpCases
    {
        get
        {
            var returns = new decimal[20];
            for (var i = 0; i < 20; i++)
            {
                returns[i] = i % 2 == 0 ? 0.02m : -0.02m;
            }

            yield return [new List<Asset> { s_vti }, new[] { returns }, 0.50m, 2.0m];
        }
    }

    /// <summary>
    /// Returns with ~2% daily vol (annualized ~31.7%).
    /// Target 10% vol, maxLeverage 1.0 → scale factor ~0.316 → weights sum < 1.0.
    /// </summary>
    public static IEnumerable<object[]> ScaleDownCases
    {
        get
        {
            var returns = new decimal[20];
            for (var i = 0; i < 20; i++)
            {
                returns[i] = i % 2 == 0 ? 0.02m : -0.02m;
            }

            yield return [new List<Asset> { s_vti }, new[] { returns }, 0.10m, 1.0m];
        }
    }
}
