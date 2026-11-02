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
using Boutquin.Trading.Application.Signals;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Fidelity and contract tests for <see cref="CarrySignal"/> — the Koijen, Moskowitz, Pedersen &amp;
/// Vrugt (2018) carry signal, operationalized long-only with a per-asset-class carry model (bond =
/// yield + roll-down; equity/REIT = trailing income yield; FX-hedged = short-rate differential). Asserts
/// the defining reference-planned properties: for a flat-price bond with a flat curve carry equals the
/// yield; a higher-income-yield equity with an identical price path scores strictly higher; the
/// missing-series omission contract per asset class; the FX degenerate guard; and that the signal is
/// wired live through <see cref="SignalTiltConstruction"/>.
/// </summary>
public sealed class CarrySignalTests
{
    private static Symbol Sym(string ticker) => new(ticker);

    private static decimal[] Flat(int length) => new decimal[length]; // all-zero returns (flat price)

    // ── Fidelity A: flat-price bond with a flat curve => carry equals the yield ──────────────────────
    // Under a constant price and a flat term structure (DGS10 == DGS2 == Y) the roll-down term vanishes
    // and carry = the current yield (Koijen et al. eq. 13). The stub returns an empty dictionary, so the
    // score is ABSENT — that is the RED.
    [Fact]
    public void ComputeScores_FlatCurveBond_CarryEqualsYield()
    {
        var bond = Sym("BND");
        var assets = new[] { bond };
        var classes = new Dictionary<Symbol, CarryAssetClass> { [bond] = CarryAssetClass.Bond };

        const decimal yield = 0.04m;
        var aux = new Dictionary<Symbol, decimal[]>
        {
            [Sym("DGS10")] = [yield], // flat curve: long point == short point == the yield
            [Sym("DGS2")] = [yield],
        };

        var signal = new CarrySignal(classes);
        var scores = signal.ComputeScores(assets, [Flat(5)], aux);

        Assert.True(scores.ContainsKey(bond), "the bond must be scored");
        Assert.True(Math.Abs(scores[bond] - yield) < 1e-12m,
            $"flat-price bond with a flat curve must score carry == yield ({yield}); got {scores.GetValueOrDefault(bond)}");
    }

    // ── Fidelity B: equity carry == trailing income yield; higher yield => strictly higher score ─────
    // Two equity ETFs with IDENTICAL (flat) price paths but different per-asset trailing income yields.
    // Equity carry = the income yield (Koijen et al. eq. 8), so the higher-yield asset must score strictly
    // higher — carry is monotone increasing in yield. The stub/bond-only impl omits equities, so both are
    // ABSENT — that is the RED.
    [Fact]
    public void ComputeScores_EquityCarry_EqualsIncomeYield_HigherYieldScoresHigher()
    {
        var hi = Sym("EQ_HI");
        var lo = Sym("EQ_LO");
        var assets = new[] { hi, lo };
        var classes = new Dictionary<Symbol, CarryAssetClass>
        {
            [hi] = CarryAssetClass.Equity,
            [lo] = CarryAssetClass.Equity,
        };

        const decimal hiYield = 0.05m;
        const decimal loYield = 0.02m;
        var aux = new Dictionary<Symbol, decimal[]>
        {
            [hi] = [hiYield], // per-asset trailing income yield keyed by the asset's own symbol
            [lo] = [loYield],
        };

        var signal = new CarrySignal(classes);
        var scores = signal.ComputeScores(assets, [Flat(5), Flat(5)], aux);

        Assert.True(Math.Abs(scores[hi] - hiYield) < 1e-12m, "equity carry must equal the trailing income yield");
        Assert.True(Math.Abs(scores[lo] - loYield) < 1e-12m, "equity carry must equal the trailing income yield");
        Assert.True(scores[hi] > scores[lo],
            "the higher-income-yield equity (identical price path) must score strictly higher — carry is monotone in yield");
    }

    // ── FX-hedged: carry == (r_foreign − r_local) / (1 + r_local); wider differential scores higher ──
    // The foreign short rate is per-asset; the local risk-free rate (DGS3MO) is market-wide (Koijen et al.
    // eq. 7). Two FX sleeves with different foreign rates over the same local rate: the wider differential
    // scores strictly higher. The bond/equity impl omits FX, so both are ABSENT — that is the RED.
    [Fact]
    public void ComputeScores_FxHedgedCarry_EqualsRateDifferential_WiderScoresHigher()
    {
        var wide = Sym("FX_WIDE");
        var narrow = Sym("FX_NARROW");
        var assets = new[] { wide, narrow };
        var classes = new Dictionary<Symbol, CarryAssetClass>
        {
            [wide] = CarryAssetClass.FxHedged,
            [narrow] = CarryAssetClass.FxHedged,
        };

        const decimal wideForeign = 0.06m;
        const decimal narrowForeign = 0.03m;
        const decimal local = 0.01m;
        var aux = new Dictionary<Symbol, decimal[]>
        {
            [wide] = [wideForeign],   // per-asset foreign short rate
            [narrow] = [narrowForeign],
            [Sym("DGS3MO")] = [local], // market-wide local risk-free short rate
        };

        var scores = new CarrySignal(classes).ComputeScores(assets, [Flat(5), Flat(5)], aux);

        var expectedWide = (wideForeign - local) / (1m + local);
        var expectedNarrow = (narrowForeign - local) / (1m + local);
        Assert.True(Math.Abs(scores[wide] - expectedWide) < 1e-12m, "FX carry must equal (r_foreign − r_local)/(1 + r_local)");
        Assert.True(Math.Abs(scores[narrow] - expectedNarrow) < 1e-12m, "FX carry must equal (r_foreign − r_local)/(1 + r_local)");
        Assert.True(scores[wide] > scores[narrow], "the wider rate differential must score strictly higher");
    }

    // ── Wiring (anti-inertness): the signal drives a real tilt through SignalTiltConstruction ────────
    // Composed into the production decorator over an equal-weight base with the carry aux bound on the
    // decorator (the ComputeTargetWeights path has no aux channel — it is constructor-bound). The
    // higher-carry equity must end strictly HEAVIER than the lower-carry one. With strength 0 (an inert
    // signal) the decorator-identity holds and the two weights are equal — that non-vacuousness is what
    // makes this a reachability pin, not a tautology.
    [Fact]
    public void ComputeScores_WiredThroughSignalTiltConstruction_TiltsTowardHigherCarryAsset()
    {
        var hi = Sym("EQ_HI");
        var lo = Sym("EQ_LO");
        var assets = new[] { hi, lo };
        var classes = new Dictionary<Symbol, CarryAssetClass>
        {
            [hi] = CarryAssetClass.Equity,
            [lo] = CarryAssetClass.Equity,
        };
        var aux = new Dictionary<Symbol, decimal[]>
        {
            [hi] = [0.06m],
            [lo] = [0.02m],
        };

        var signal = new CarrySignal(classes);
        var baseModel = new EqualWeightConstruction();

        var tilted = new SignalTiltConstruction(baseModel, [(signal, 0.10m)], aux);
        var weights = tilted.ComputeTargetWeights(assets, [Flat(5), Flat(5)]);

        Assert.True(weights[hi] > weights[lo],
            "EQ_HI (higher carry) must be weighted strictly above EQ_LO — the signal must be wired, not inert");

        // Non-vacuousness: at strength 0 the decorator-identity restores the equal-weight base.
        var inert = new SignalTiltConstruction(baseModel, [(signal, 0m)], aux);
        var inertWeights = inert.ComputeTargetWeights(assets, [Flat(5), Flat(5)]);
        Assert.True(Math.Abs(inertWeights[hi] - inertWeights[lo]) < 1e-12m,
            "with strength 0 the carry tilt is inert and the base equal weights survive");
    }

    // ── Missing-series omission: a bond missing a market-wide curve point is omitted ─────────────────
    [Fact]
    public void ComputeScores_BondMissingCurvePoint_OmitsBond()
    {
        var bond = Sym("BND");
        var classes = new Dictionary<Symbol, CarryAssetClass> { [bond] = CarryAssetClass.Bond };
        // Only DGS10 present; DGS2 (the short curve point) is absent => the bond cannot be scored.
        var aux = new Dictionary<Symbol, decimal[]> { [Sym("DGS10")] = [0.04m] };

        var scores = new CarrySignal(classes).ComputeScores([bond], [Flat(5)], aux);

        Assert.False(scores.ContainsKey(bond), "a bond missing a curve point must be omitted (absent key)");
        Assert.Empty(scores);
    }

    // ── Missing-series omission: an equity missing its per-asset income yield is omitted ─────────────
    [Fact]
    public void ComputeScores_EquityMissingIncomeYield_OmitsEquity()
    {
        var eq = Sym("SPY");
        var classes = new Dictionary<Symbol, CarryAssetClass> { [eq] = CarryAssetClass.Equity };

        var scores = new CarrySignal(classes).ComputeScores([eq], [Flat(5)], auxiliarySeries: null);

        Assert.False(scores.ContainsKey(eq), "an equity missing its income-yield series must be omitted");
    }

    // ── Asset-class dispatch: an asset absent from the class map is omitted ──────────────────────────
    [Fact]
    public void ComputeScores_AssetNotInClassMap_IsOmitted()
    {
        var known = Sym("SPY");
        var unknown = Sym("MYSTERY");
        var assets = new[] { known, unknown };
        var classes = new Dictionary<Symbol, CarryAssetClass> { [known] = CarryAssetClass.Equity };
        var aux = new Dictionary<Symbol, decimal[]> { [known] = [0.03m], [unknown] = [0.09m] };

        var scores = new CarrySignal(classes).ComputeScores(assets, [Flat(5), Flat(5)], aux);

        Assert.True(scores.ContainsKey(known), "the mapped asset must be scored");
        Assert.False(scores.ContainsKey(unknown), "an unmapped asset has no carry model and must be omitted");
    }

    // ── Contract: empty asset list yields an empty dictionary ────────────────────────────────────────
    [Fact]
    public void ComputeScores_EmptyAssetList_ReturnsEmptyDictionary()
    {
        var scores = new CarrySignal(new Dictionary<Symbol, CarryAssetClass>()).ComputeScores([], []);
        Assert.Empty(scores);
    }

    // ── Contract: a null/empty per-asset return series is omitted (insufficient history) ────────────
    [Fact]
    public void ComputeScores_NullOrEmptyReturnSeries_OmitsAsset()
    {
        var a = Sym("A");
        var b = Sym("B");
        var assets = new[] { a, b };
        var classes = new Dictionary<Symbol, CarryAssetClass>
        {
            [a] = CarryAssetClass.Equity,
            [b] = CarryAssetClass.Equity,
        };
        var aux = new Dictionary<Symbol, decimal[]> { [a] = [0.03m], [b] = [0.03m] };

        // b has an empty return series => omitted despite a valid income yield.
        var scores = new CarrySignal(classes).ComputeScores(assets, [Flat(5), []], aux);

        Assert.True(scores.ContainsKey(a));
        Assert.False(scores.ContainsKey(b), "an empty return series is insufficient history — omit");
    }

    // ── Degenerate: FX assets with a local rate ≤ −100% are omitted without affecting other classes ──
    [Fact]
    public void ComputeScores_FxLocalRateBelowMinusOne_OmitsFxAssetsAndScoresOthers()
    {
        var fx1 = Sym("FX1");
        var fx2 = Sym("FX2");
        var bond = Sym("BND");
        var equity = Sym("EQ");
        var classes = new Dictionary<Symbol, CarryAssetClass>
        {
            [fx1] = CarryAssetClass.FxHedged,
            [fx2] = CarryAssetClass.FxHedged,
            [bond] = CarryAssetClass.Bond,
            [equity] = CarryAssetClass.Equity,
        };
        var aux = new Dictionary<Symbol, decimal[]>
        {
            [fx1] = [0.02m],
            [fx2] = [0.03m],
            [Sym("DGS10")] = [0.04m],
            [Sym("DGS2")] = [0.02m],
            [equity] = [0.03m],
            [Sym("DGS3MO")] = [-1.5m], // 1 + r_local = -0.5 <= 0 => degenerate denominator
        };

        var scores = new CarrySignal(classes).ComputeScores(
            [fx1, fx2, bond, equity],
            [Flat(5), Flat(5), Flat(5), Flat(5)],
            aux);

        Assert.DoesNotContain(fx1, scores.Keys);
        Assert.DoesNotContain(fx2, scores.Keys);
        Assert.Contains(bond, scores.Keys);
        Assert.Contains(equity, scores.Keys);
    }

    [Fact]
    public void Constructors_LoggerlessAndNullLoggerInjected_ProduceIdenticalScores()
    {
        var equity = Sym("EQ");
        var classes = new Dictionary<Symbol, CarryAssetClass> { [equity] = CarryAssetClass.Equity };
        var aux = new Dictionary<Symbol, decimal[]> { [equity] = [0.03m] };
        var loggerless = new CarrySignal(classes, 1m, "DGS10", "DGS2", "DGS3MO");
        var injected = new CarrySignal(
            classes, NullLogger<CarrySignal>.Instance, 1m, "DGS10", "DGS2", "DGS3MO");

        Assert.Equal(
            loggerless.ComputeScores([equity], [Flat(5)], aux),
            injected.ComputeScores([equity], [Flat(5)], aux));
    }

    [Fact]
    public void ComputeScores_ThreeDegenerateFxAssets_LogsOneWarningNamingAllAssets()
    {
        var assets = new[] { Sym("FX1"), Sym("FX2"), Sym("FX3") };
        var classes = assets.ToDictionary(asset => asset, _ => CarryAssetClass.FxHedged);
        var aux = assets.ToDictionary(asset => asset, _ => new[] { 0.02m });
        aux[Sym("DGS3MO")] = [-1.0m];
        var logger = new CapturingLogger<CarrySignal>();
        var signal = new CarrySignal(classes, logger, 1m, "DGS10", "DGS2", "DGS3MO");

        _ = signal.ComputeScores(assets, assets.Select(_ => Flat(5)).ToArray(), aux);

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.All(assets, asset => Assert.Contains(asset.ToString(), warning.Message, StringComparison.Ordinal));
        Assert.Contains("non-positive (1 + r_local)", warning.Message, StringComparison.Ordinal);
    }

    // ── Guards: length mismatch and nulls are surfaced ──────────────────────────────────────────────
    [Fact]
    public void ComputeScores_ReturnsLengthMismatch_Throws()
    {
        var classes = new Dictionary<Symbol, CarryAssetClass> { [Sym("A")] = CarryAssetClass.Equity };
        Assert.Throws<ArgumentException>(
            () => new CarrySignal(classes).ComputeScores([Sym("A")], [])); // 1 asset, 0 series
    }

    [Fact]
    public void Constructor_NullAssetClasses_Throws()
        => Assert.Throws<ArgumentNullException>(() => new CarrySignal(null!));
}
