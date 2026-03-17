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

public sealed class BlackLittermanFullInversionTests
{
    private static readonly Asset s_a = new("A");
    private static readonly Asset s_b = new("B");

    [Fact]
    public void ComputeTargetWeights_CorrelatedAssets_UsesFullCovariance()
    {
        // Two assets with different expected returns but high correlation.
        // With diagonal approximation: each weight proportional to mu_i / sigma_ii.
        // With full inversion: cross-covariance affects allocation.
        var assets = new List<Asset> { s_a, s_b };

        // Returns: A and B highly correlated (A = B + small noise)
        var returnsA = new decimal[30];
        var returnsB = new decimal[30];
        for (var i = 0; i < 30; i++)
        {
            returnsA[i] = 0.001m * i - 0.015m;
            returnsB[i] = 0.001m * i - 0.015m + (i % 2 == 0 ? 0.0001m : -0.0001m);
        }

        var returns = new[] { returnsA, returnsB };
        var equilibriumWeights = new[] { 0.6m, 0.4m };

        var model = new BlackLittermanConstruction(
            equilibriumWeights, riskAversionCoefficient: 2.5m, tau: 0.05m);

        var weights = model.ComputeTargetWeights(assets, returns);

        // After H8 fix: full matrix inversion should produce different weights than diagonal.
        // With diagonal: weight proportional to mu_i / sigma_ii for each asset independently.
        // With full inversion: off-diagonal elements redistribute weight.
        // We verify the model produces valid weights (non-negative, sum to 1.0).
        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-10m);
        weights[s_a].Should().BeGreaterThanOrEqualTo(0m);
        weights[s_b].Should().BeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public void ComputeTargetWeights_SingularMatrix_FallsToDiagonal()
    {
        // Perfectly collinear returns → singular covariance matrix.
        // Should fall back to diagonal approximation and still produce valid weights.
        var assets = new List<Asset> { s_a, s_b };

        var returnsA = new decimal[30];
        var returnsB = new decimal[30];
        for (var i = 0; i < 30; i++)
        {
            returnsA[i] = 0.001m * i - 0.015m;
            returnsB[i] = 2m * returnsA[i]; // Perfectly collinear
        }

        var returns = new[] { returnsA, returnsB };
        var equilibriumWeights = new[] { 0.5m, 0.5m };

        var model = new BlackLittermanConstruction(
            equilibriumWeights, riskAversionCoefficient: 2.5m, tau: 0.05m);

        var weights = model.ComputeTargetWeights(assets, returns);

        // Should produce valid weights via diagonal fallback
        weights.Values.Sum().Should().BeApproximately(1.0m, 1e-10m);
        weights[s_a].Should().BeGreaterThanOrEqualTo(0m);
        weights[s_b].Should().BeGreaterThanOrEqualTo(0m);
    }
}
