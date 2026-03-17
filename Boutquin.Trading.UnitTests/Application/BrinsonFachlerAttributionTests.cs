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
using FluentAssertions;

/// <summary>
/// Tests for Brinson-Fachler return attribution model.
/// </summary>
public sealed class BrinsonFachlerAttributionTests
{
    private const decimal Precision = 1e-10m;

    // --- RP3-01 Test: Same weights as benchmark → zero allocation effect ---

    [Fact]
    public void Attribute_SameWeightsAsBenchmark_ShouldHaveZeroAllocationEffect()
    {
        // Portfolio and benchmark have identical weights but different returns
        var assetNames = new Asset[] { new("Equity"), new("Bonds") };
        var portfolioWeights = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.6m, [new Asset("Bonds")] = 0.4m };
        var benchmarkWeights = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.6m, [new Asset("Bonds")] = 0.4m };
        var portfolioReturns = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.10m, [new Asset("Bonds")] = 0.03m };
        var benchmarkReturns = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.08m, [new Asset("Bonds")] = 0.02m };

        var result = BrinsonFachlerAttributor.Attribute(
            assetNames, portfolioWeights, benchmarkWeights, portfolioReturns, benchmarkReturns);

        result.AllocationEffect.Should().BeApproximately(0m, Precision,
            "Same weights → zero allocation effect");
    }

    // --- RP3-01 Test: Same selection as benchmark → zero selection effect ---

    [Fact]
    public void Attribute_SameReturnsAsBenchmark_ShouldHaveZeroSelectionEffect()
    {
        // Portfolio and benchmark have identical returns but different weights
        var assetNames = new Asset[] { new("Equity"), new("Bonds") };
        var portfolioWeights = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.7m, [new Asset("Bonds")] = 0.3m };
        var benchmarkWeights = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.6m, [new Asset("Bonds")] = 0.4m };
        var portfolioReturns = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.08m, [new Asset("Bonds")] = 0.02m };
        var benchmarkReturns = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.08m, [new Asset("Bonds")] = 0.02m };

        var result = BrinsonFachlerAttributor.Attribute(
            assetNames, portfolioWeights, benchmarkWeights, portfolioReturns, benchmarkReturns);

        result.SelectionEffect.Should().BeApproximately(0m, Precision,
            "Same returns → zero selection effect");
    }

    // --- RP3-01 Test: Effects sum to total active return ---

    [Fact]
    public void Attribute_EffectsSumToTotalActiveReturn()
    {
        var assetNames = new Asset[] { new("Equity"), new("Bonds"), new("Commodities") };
        var portfolioWeights = new Dictionary<Asset, decimal>
        { [new Asset("Equity")] = 0.5m, [new Asset("Bonds")] = 0.3m, [new Asset("Commodities")] = 0.2m };
        var benchmarkWeights = new Dictionary<Asset, decimal>
        { [new Asset("Equity")] = 0.6m, [new Asset("Bonds")] = 0.3m, [new Asset("Commodities")] = 0.1m };
        var portfolioReturns = new Dictionary<Asset, decimal>
        { [new Asset("Equity")] = 0.12m, [new Asset("Bonds")] = 0.04m, [new Asset("Commodities")] = 0.08m };
        var benchmarkReturns = new Dictionary<Asset, decimal>
        { [new Asset("Equity")] = 0.10m, [new Asset("Bonds")] = 0.03m, [new Asset("Commodities")] = 0.05m };

        var result = BrinsonFachlerAttributor.Attribute(
            assetNames, portfolioWeights, benchmarkWeights, portfolioReturns, benchmarkReturns);

        var sumOfEffects = result.AllocationEffect + result.SelectionEffect + result.InteractionEffect;
        sumOfEffects.Should().BeApproximately(result.TotalActiveReturn, Precision,
            "Allocation + Selection + Interaction must equal total active return");
    }

    // --- RP3-01 Test: Verify per-asset effects are populated ---

    [Fact]
    public void Attribute_ShouldPopulatePerAssetEffects()
    {
        var assetNames = new Asset[] { new("Equity"), new("Bonds") };
        var portfolioWeights = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.7m, [new Asset("Bonds")] = 0.3m };
        var benchmarkWeights = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.6m, [new Asset("Bonds")] = 0.4m };
        var portfolioReturns = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.10m, [new Asset("Bonds")] = 0.03m };
        var benchmarkReturns = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.08m, [new Asset("Bonds")] = 0.02m };

        var result = BrinsonFachlerAttributor.Attribute(
            assetNames, portfolioWeights, benchmarkWeights, portfolioReturns, benchmarkReturns);

        result.AssetAllocationEffects.Should().HaveCount(2);
        result.AssetSelectionEffects.Should().HaveCount(2);
        result.AssetInteractionEffects.Should().HaveCount(2);
    }

    // --- RP3-01 Test: Hand-calculated Brinson-Fachler example ---

    [Fact]
    public void Attribute_HandCalculatedExample_ShouldMatchExpected()
    {
        // Two-sector example:
        // Benchmark: Equity 60% @ 8%, Bonds 40% @ 2%  → Benchmark return = 0.048 + 0.008 = 0.056
        // Portfolio: Equity 70% @ 10%, Bonds 30% @ 3%  → Portfolio return = 0.070 + 0.009 = 0.079
        // Active return = 0.079 - 0.056 = 0.023
        //
        // Allocation effect per sector:
        //   Equity: (Wp - Wb) * (Rb_sector - Rb_total) = (0.7-0.6) * (0.08 - 0.056) = 0.1 * 0.024 = 0.0024
        //   Bonds:  (Wp - Wb) * (Rb_sector - Rb_total) = (0.3-0.4) * (0.02 - 0.056) = -0.1 * -0.036 = 0.0036
        //   Total allocation = 0.006
        //
        // Selection effect per sector:
        //   Equity: Wb * (Rp_sector - Rb_sector) = 0.6 * (0.10 - 0.08) = 0.012
        //   Bonds:  Wb * (Rp_sector - Rb_sector) = 0.4 * (0.03 - 0.02) = 0.004
        //   Total selection = 0.016
        //
        // Interaction effect per sector:
        //   Equity: (Wp - Wb) * (Rp_sector - Rb_sector) = 0.1 * 0.02 = 0.002
        //   Bonds:  (Wp - Wb) * (Rp_sector - Rb_sector) = -0.1 * 0.01 = -0.001
        //   Total interaction = 0.001
        //
        // Check: 0.006 + 0.016 + 0.001 = 0.023 ✓

        var assetNames = new Asset[] { new("Equity"), new("Bonds") };
        var portfolioWeights = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.7m, [new Asset("Bonds")] = 0.3m };
        var benchmarkWeights = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.6m, [new Asset("Bonds")] = 0.4m };
        var portfolioReturns = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.10m, [new Asset("Bonds")] = 0.03m };
        var benchmarkReturns = new Dictionary<Asset, decimal> { [new Asset("Equity")] = 0.08m, [new Asset("Bonds")] = 0.02m };

        var result = BrinsonFachlerAttributor.Attribute(
            assetNames, portfolioWeights, benchmarkWeights, portfolioReturns, benchmarkReturns);

        result.TotalActiveReturn.Should().BeApproximately(0.023m, Precision);
        result.AllocationEffect.Should().BeApproximately(0.006m, Precision);
        result.SelectionEffect.Should().BeApproximately(0.016m, Precision);
        result.InteractionEffect.Should().BeApproximately(0.001m, Precision);
    }

    // --- RP3-01 Test: Empty assets → empty result ---

    [Fact]
    public void Attribute_EmptyAssets_ShouldReturnZeroEffects()
    {
        var result = BrinsonFachlerAttributor.Attribute(
            Array.Empty<Asset>(),
            new Dictionary<Asset, decimal>(),
            new Dictionary<Asset, decimal>(),
            new Dictionary<Asset, decimal>(),
            new Dictionary<Asset, decimal>());

        result.AllocationEffect.Should().Be(0m);
        result.SelectionEffect.Should().Be(0m);
        result.InteractionEffect.Should().Be(0m);
        result.TotalActiveReturn.Should().Be(0m);
    }
}
