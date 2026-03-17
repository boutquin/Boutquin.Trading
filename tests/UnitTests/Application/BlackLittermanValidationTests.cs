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
