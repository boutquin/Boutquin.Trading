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

using System.Text.Json;

using Boutquin.Trading.Application.PortfolioConstruction;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Cross-language / self-consistency verification for LTDC clustering in
/// <see cref="ReturnTiltedHrpConstruction"/> with
/// <see cref="ClusteringMethod.LowerTailDependence"/> (Lohre, Rother &amp; Schäfer 2020).
/// <para>
/// LTDC clustering has no Python reference in this repo (the lower-tail-dependence estimator
/// lives in Boutquin.Numerics). The vector
/// <c>tests/Verification/vectors/construction_ltdc_hrp.json</c> is therefore a
/// <b>self-consistency pin</b>: the C# production path computes the LTDC-clustered weights on a
/// fixed 3-asset panel and the JSON records them. A regression in either the LTDC distance build
/// or the Numerics estimator surfaces as a mismatch here.
/// </para>
/// </summary>
public sealed class LtdcHrpCrossLanguageTests : CrossLanguageVerificationBase
{
    private static decimal[][] GetJaggedArray(JsonElement element)
    {
        var outer = element.EnumerateArray().ToArray();
        var result = new decimal[outer.Length][];
        for (var i = 0; i < outer.Length; i++)
        {
            result[i] = outer[i].EnumerateArray()
                .Select(e => (decimal)e.GetDouble())
                .ToArray();
        }

        return result;
    }

    private static decimal[] GetWeights(JsonElement element) =>
        element.EnumerateArray()
            .Select(e => (decimal)e.GetDouble())
            .ToArray();

    private static IReadOnlyList<Symbol> MakeAssets(int n) =>
        Enumerable.Range(0, n)
            .Select(i => new Symbol($"ASSET{i}"))
            .ToList();

    [Fact]
    public void LtdcHrp_MatchesSelfConsistencyVector()
    {
        var doc = LoadVector("construction_ltdc_hrp");
        var c = doc.RootElement.GetProperty("cases").GetProperty("four_asset_asymmetric_tail");
        var returns = GetJaggedArray(c.GetProperty("returns"));
        var expected = GetWeights(c.GetProperty("ltdc_weights"));
        var assets = MakeAssets(returns.Length);

        var model = new ReturnTiltedHrpConstruction(
            clusteringMethod: ClusteringMethod.LowerTailDependence);
        var actual = model.ComputeTargetWeights(assets, returns);

        // LTDC routes through Math.Sqrt/Math.Log and the Numerics tail estimator (double precision),
        // so PrecisionNumeric (1e-6) is the appropriate tier.
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            AssertWithinTolerance(actual[assets[i]], expected[i], PrecisionNumeric,
                $"LTDC HRP weight[{i}]: ");
        }

        // Weights are a valid simplex point.
        AssertWithinTolerance(actual.Values.Sum(), 1m, PrecisionNumeric, "LTDC HRP weights sum: ");
        foreach (var (asset, w) in actual)
        {
            Assert.True(w >= -PrecisionNumeric, $"LTDC HRP weight for {asset.Ticker} = {w} is negative");
        }
    }
}
