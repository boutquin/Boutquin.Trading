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

using Boutquin.Trading.Application.PortfolioConstruction;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Tests for <see cref="ReturnTiltedHrpConstruction"/>, focused on the M-9 LTDC clustering option
/// (Lohre, Rother &amp; Schäfer 2020 lower-tail-dependence-coefficient clustering).
/// </summary>
public sealed class ReturnTiltedHrpConstructionTests
{
    private const decimal Tolerance = 1e-9m;

    /// <summary>
    /// Builds a 4-asset return panel engineered so Pearson-correlation distance and LTDC distance
    /// disagree on which assets cluster together, which is what makes the LTDC option observable.
    /// <list type="bullet">
    /// <item>A strong shared Gaussian BODY factor drives linear (Pearson) correlation: {0,1} co-move
    /// and {2,3} co-move, so Pearson distance groups (0,1) and (2,3).</item>
    /// <item>Rare isolated joint down-shocks create LOWER-TAIL dependence for {0,2} and {1,3}, too
    /// infrequent to move the linear correlation but enough for LTDC to group (0,2) and (1,3).</item>
    /// </list>
    /// The two metrics therefore produce different dendrogram leaf orders and a different
    /// top-level HRP half-split (mid = N/2 = 2), hence different allocations.
    /// </summary>
    private static decimal[][] BuildAsymmetricTailPanel()
    {
        const int t = 160;
        var rng = new Random(20260628);
        var a0 = new decimal[t];
        var a1 = new decimal[t];
        var a2 = new decimal[t];
        var a3 = new decimal[t];

        double Norm()
        {
            var u1 = 1 - rng.NextDouble();
            var u2 = 1 - rng.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        for (var i = 0; i < t; i++)
        {
            var bodyA = Norm();  // strong shared body factor for 0 & 1 (drives Pearson correlation)
            var bodyB = Norm();  // strong shared body factor for 2 & 3
            var crashAC = rng.NextDouble() < 0.06 ? -Math.Abs(Norm()) * 5 : 0; // joint crash 0 & 2
            var crashBD = rng.NextDouble() < 0.06 ? -Math.Abs(Norm()) * 5 : 0; // joint crash 1 & 3

            a0[i] = (decimal)((0.004 * Norm()) + (0.018 * bodyA) + (0.012 * crashAC));
            a1[i] = (decimal)((0.013 * Norm()) + (0.018 * bodyA) + (0.012 * crashBD));
            a2[i] = (decimal)((0.003 * Norm()) + (0.018 * bodyB) + (0.012 * crashAC));
            a3[i] = (decimal)((0.009 * Norm()) + (0.018 * bodyB) + (0.012 * crashBD));
        }

        return [a0, a1, a2, a3];
    }

    private static IReadOnlyList<Symbol> MakeAssets(int n) =>
        Enumerable.Range(0, n).Select(i => new Symbol($"ASSET{i}")).ToList();

    [Fact]
    public void DefaultClusteringMethod_IsPearsonDistance_PreservesExistingBehavior()
    {
        var returns = BuildAsymmetricTailPanel();
        var assets = MakeAssets(returns.Length);

        // The default constructor and an explicit PearsonDistance must agree exactly —
        // proves the default did not silently change.
        var implicitDefault = new ReturnTiltedHrpConstruction();
        var explicitPearson = new ReturnTiltedHrpConstruction(
            clusteringMethod: ClusteringMethod.PearsonDistance);

        var wImplicit = implicitDefault.ComputeTargetWeights(assets, returns);
        var wExplicit = explicitPearson.ComputeTargetWeights(assets, returns);

        foreach (var asset in assets)
        {
            Assert.True(Math.Abs(wImplicit[asset] - wExplicit[asset]) < Tolerance,
                $"Default clustering must equal explicit PearsonDistance for {asset.Ticker}");
        }
    }

    [Fact]
    public void LtdcClustering_WeightsSumToOne_AndAreNonNegative()
    {
        var returns = BuildAsymmetricTailPanel();
        var assets = MakeAssets(returns.Length);

        var model = new ReturnTiltedHrpConstruction(
            clusteringMethod: ClusteringMethod.LowerTailDependence);
        var weights = model.ComputeTargetWeights(assets, returns);

        Assert.Equal(assets.Count, weights.Count);
        Assert.True(Math.Abs(weights.Values.Sum() - 1m) < 1e-6m,
            $"LTDC weights must sum to 1; got {weights.Values.Sum()}");
        foreach (var (asset, w) in weights)
        {
            Assert.True(w >= -1e-9m, $"LTDC weight for {asset.Ticker} = {w} is negative");
        }
    }

    [Fact]
    public void LtdcClustering_ProducesDifferentWeightsFromPearson_OnAsymmetricTailData()
    {
        // Fidelity proof: if LTDC were merely mapped through to the Pearson code path, the two
        // allocations would be identical. On a panel where the LOWER-TAIL co-crash structure
        // differs from the linear-correlation structure, the two clustering methods place the
        // assets into different cluster orders and therefore allocate differently.
        var returns = BuildAsymmetricTailPanel();
        var assets = MakeAssets(returns.Length);

        var pearson = new ReturnTiltedHrpConstruction(
            clusteringMethod: ClusteringMethod.PearsonDistance);
        var ltdc = new ReturnTiltedHrpConstruction(
            clusteringMethod: ClusteringMethod.LowerTailDependence);

        var wPearson = pearson.ComputeTargetWeights(assets, returns);
        var wLtdc = ltdc.ComputeTargetWeights(assets, returns);

        var maxDiff = assets.Max(a => Math.Abs(wPearson[a] - wLtdc[a]));
        Assert.True(maxDiff > 1e-6m,
            $"LTDC clustering must produce different weights from Pearson clustering; maxDiff={maxDiff}");
    }
}
