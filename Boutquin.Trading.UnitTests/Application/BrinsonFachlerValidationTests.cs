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

using Boutquin.Trading.Application.Analytics;

public sealed class BrinsonFachlerValidationTests
{
    private static readonly Asset s_a = new("A");

    [Fact]
    public void Attribute_NullAssetNames_ThrowsArgumentNullException()
    {
        var dict = new Dictionary<Asset, decimal> { [s_a] = 0.5m };
        var act = () => BrinsonFachlerAttributor.Attribute(null!, dict, dict, dict, dict);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Attribute_NullPortfolioWeights_ThrowsArgumentNullException()
    {
        var names = new List<Asset> { s_a };
        var dict = new Dictionary<Asset, decimal> { [s_a] = 0.5m };
        var act = () => BrinsonFachlerAttributor.Attribute(names, null!, dict, dict, dict);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(BrinsonFachlerValidationTestData.MissingKeyCases),
        MemberType = typeof(BrinsonFachlerValidationTestData))]
    public void Attribute_MissingKey_ThrowsArgumentException(
        IReadOnlyList<Asset> assetNames,
        IReadOnlyDictionary<Asset, decimal> portfolioWeights,
        IReadOnlyDictionary<Asset, decimal> benchmarkWeights,
        IReadOnlyDictionary<Asset, decimal> portfolioReturns,
        IReadOnlyDictionary<Asset, decimal> benchmarkReturns)
    {
        var act = () => BrinsonFachlerAttributor.Attribute(
            assetNames, portfolioWeights, benchmarkWeights, portfolioReturns, benchmarkReturns);

        act.Should().Throw<ArgumentException>();
    }
}
