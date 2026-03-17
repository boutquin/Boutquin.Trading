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

public sealed class BlackLittermanDefensiveCopyTests
{
    [Fact]
    public void Constructor_MutatingOriginalArray_DoesNotAffectModel()
    {
        var assets = new List<Asset> { new("A"), new("B") };
        var equilibriumWeights = new[] { 0.5m, 0.5m };

        var returns = new[]
        {
            new decimal[] { 0.01m, 0.02m, -0.01m, 0.03m, 0.00m },
            new decimal[] { 0.02m, -0.01m, 0.01m, 0.00m, 0.03m },
        };

        var model = new BlackLittermanConstruction(equilibriumWeights);

        // Get weights before mutation
        var weightsBefore = model.ComputeTargetWeights(assets, returns);

        // Mutate the original array after construction
        equilibriumWeights[0] = 1.0m;
        equilibriumWeights[1] = 0.0m;

        // Get weights after mutation — should match "before" if defensive copy was made
        var weightsAfter = model.ComputeTargetWeights(assets, returns);

        weightsAfter[assets[0]].Should().BeApproximately(weightsBefore[assets[0]], 1e-12m,
            "mutating the original equilibrium weights array should not affect the model");
        weightsAfter[assets[1]].Should().BeApproximately(weightsBefore[assets[1]], 1e-12m,
            "mutating the original equilibrium weights array should not affect the model");
    }
}
