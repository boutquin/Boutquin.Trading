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

using Boutquin.Trading.Application.PortfolioConstruction;

public sealed class BlackLittermanValidationTests
{
    [Fact]
    public void ComputeTargetWeights_NoViews_ReturnsEquilibriumWeights()
    {
        // When no views are provided, BlackLitterman should return equilibrium weights directly
        // without round-tripping through matrix inversion (CLAUDE.md convention).
        var eqWeights = new[] { 0.6m, 0.4m };
        var model = new BlackLittermanConstruction(equilibriumWeights: eqWeights);

        var assets = new List<Asset> { new("AAPL"), new("MSFT") };
        var returns = new[]
        {
            new decimal[] { 0.01m, 0.02m, -0.01m, 0.015m },
            new decimal[] { 0.02m, -0.01m, 0.01m, 0.005m },
        };

        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(2);
        weights[new Asset("AAPL")].Should().Be(0.6m);
        weights[new Asset("MSFT")].Should().Be(0.4m);
    }

    [Fact]
    public void ComputeTargetWeights_NoViews_ThreeAssets_ReturnsEquilibriumWeightsExactly()
    {
        // Verify with 3 assets that equilibrium weights are returned exactly (no numerical drift)
        var eqWeights = new[] { 0.5m, 0.3m, 0.2m };
        var model = new BlackLittermanConstruction(equilibriumWeights: eqWeights);

        var assets = new List<Asset> { new("A"), new("B"), new("C") };
        var returns = new[]
        {
            new decimal[] { 0.01m, 0.02m, -0.01m },
            new decimal[] { 0.02m, -0.01m, 0.01m },
            new decimal[] { 0.00m, 0.01m, 0.02m },
        };

        var weights = model.ComputeTargetWeights(assets, returns);

        weights.Should().HaveCount(3);
        weights[new Asset("A")].Should().Be(0.5m);
        weights[new Asset("B")].Should().Be(0.3m);
        weights[new Asset("C")].Should().Be(0.2m);
    }

    [Fact]
    public void ComputeTargetWeights_EquilibriumWeightsLengthMismatch_ThrowsArgumentException()
    {
        // 3-asset list but 2-element equilibrium weights
        var assets = new List<Asset> { new("A"), new("B"), new("C") };
        var equilibriumWeights = new[] { 0.5m, 0.5m }; // Mismatch: 2 vs 3

        var returns = new[]
        {
            new decimal[] { 0.01m, 0.02m, -0.01m },
            new decimal[] { 0.02m, -0.01m, 0.01m },
            new decimal[] { 0.00m, 0.01m, 0.02m },
        };

        var model = new BlackLittermanConstruction(equilibriumWeights);

        var act = () => model.ComputeTargetWeights(assets, returns);

        act.Should().Throw<ArgumentException>();
    }
}
