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

public static class BrinsonFachlerValidationTestData
{
    private static readonly Asset s_a = new("A");
    private static readonly Asset s_x = new("X");

    public static IEnumerable<object[]> MissingKeyCases
    {
        get
        {
            // assetNames contains "X" but portfolioReturns dict doesn't have it
            var assetNames = new List<Asset> { s_a, s_x };
            var portfolioWeights = new Dictionary<Asset, decimal> { [s_a] = 0.5m, [s_x] = 0.5m };
            var benchmarkWeights = new Dictionary<Asset, decimal> { [s_a] = 0.5m, [s_x] = 0.5m };
            var portfolioReturns = new Dictionary<Asset, decimal> { [s_a] = 0.05m }; // Missing "X"
            var benchmarkReturns = new Dictionary<Asset, decimal> { [s_a] = 0.03m, [s_x] = 0.02m };

            yield return [assetNames, portfolioWeights, benchmarkWeights, portfolioReturns, benchmarkReturns];
        }
    }
}
