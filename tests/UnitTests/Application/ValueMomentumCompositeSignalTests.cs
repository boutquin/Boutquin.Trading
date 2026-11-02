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

using Boutquin.Trading.Application.Signals;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Fidelity and contract tests for <see cref="ValueMomentumCompositeSignal"/> — the Asness, Moskowitz &amp;
/// Pedersen (2013) 50/50 value-momentum COMBO (eq. 3), operationalized as
/// <c>0.5·z(value) + 0.5·z(momentum)</c> over the assets scored by both legs. Asserts the defining
/// reference-planned §M-4 properties: composite = weighted sum of the two cross-sectional z-scores
/// (genuinely reusing M-2's <see cref="CrossSectionalMomentumSignal"/> for the momentum leg),
/// value/momentum anti-correlation on a loser-loser panel (the paper's ≈ −0.48 proxy), the
/// variance-reduction (diversification) property, the either-leg-omits omission rule, the zero-variance
/// z-score guard, configurable weights, and the empty-input contract.
/// </summary>
public sealed class ValueMomentumCompositeSignalTests
{
    // Compact params so a 20-day series satisfies both legs (value needs value_lb·dpm = 20; momentum
    // needs mom_lb·dpm = 12).
    private const int ValueLb = 5;
    private const int MomLb = 3;
    private const int MomSkip = 1;
    private const int Dpm = 4;
    private const int Len = ValueLb * Dpm; // 20

    private static Symbol Sym(string ticker) => new(ticker);

    private static decimal[] Constant(decimal dailyReturn, int length)
    {
        var series = new decimal[length];
        for (var i = 0; i < length; i++)
        {
            series[i] = dailyReturn;
        }

        return series;
    }

    private static (LongHorizonReversalValueSignal Value, CrossSectionalMomentumSignal Momentum) Legs()
        => (new LongHorizonReversalValueSignal(ValueLb, Dpm),
            new CrossSectionalMomentumSignal(MomLb, MomSkip, Dpm));

    // Population cross-sectional z-scores (divide-by-N) — matches SignalTiltConstruction / the composite.
    private static decimal[] ZScorePopulation(decimal[] vals)
    {
        var n = vals.Length;
        var mean = vals.Sum() / n;
        var variance = vals.Sum(v => (v - mean) * (v - mean)) / n;
        var sigma = (decimal)Math.Sqrt((double)variance);
        return vals.Select(v => (v - mean) / sigma).ToArray();
    }

    private static decimal PopulationVariance(decimal[] vals)
    {
        var n = vals.Length;
        var mean = vals.Sum() / n;
        return vals.Sum(v => (v - mean) * (v - mean)) / n;
    }

    // ── Fidelity + reuse: composite == valueWeight·z(value) + momentumWeight·z(momentum), using the
    // injected legs (proves the composite genuinely delegates to M-2's momentum signal). ────────────
    [Fact]
    public void ComputeScores_EqualsWeightedZScores_ReusingInjectedLegs()
    {
        var assets = new[] { Sym("A"), Sym("B"), Sym("C") };
        var returns = new[]
        {
            Constant(0.02m, Len),
            Constant(0.005m, Len),
            Constant(-0.01m, Len),
        };

        var (value, momentum) = Legs();
        var composite = new ValueMomentumCompositeSignal(value, momentum, 0.5m, 0.5m);
        var scores = composite.ComputeScores(assets, returns);

        // Independently compute the expected composite from the SAME injected legs' outputs.
        var valueRaw = value.ComputeScores(assets, returns);
        var momRaw = momentum.ComputeScores(assets, returns);
        var zVal = ZScorePopulation(assets.Select(a => valueRaw[a]).ToArray());
        var zMom = ZScorePopulation(assets.Select(a => momRaw[a]).ToArray());

        Assert.Equal(3, scores.Count);
        for (var i = 0; i < assets.Length; i++)
        {
            var expected = (0.5m * zVal[i]) + (0.5m * zMom[i]);
            Assert.True(Math.Abs(scores[assets[i]] - expected) < 1e-12m,
                $"composite[{assets[i]}] must equal 0.5·z(value)+0.5·z(momentum) of the injected legs");
        }
    }

    // ── Fidelity: on a loser-loser panel, value & momentum scores anti-correlate (the ≈ −0.48 proxy). ─
    [Fact]
    public void ComputeScores_LoserLoserPanel_ValueAndMomentumAntiCorrelate()
    {
        var assets = new[] { Sym("A"), Sym("B"), Sym("C"), Sym("D"), Sym("E") };
        // Monotone-increasing constant returns: value (=−5y return) decreases, momentum (12-1) increases.
        var returns = new[]
        {
            Constant(-0.02m, Len),
            Constant(-0.01m, Len),
            Constant(0.005m, Len),
            Constant(0.01m, Len),
            Constant(0.02m, Len),
        };

        var (value, momentum) = Legs();
        var valueScores = assets.Select(a => value.ComputeScores(assets, returns)[a]).ToArray();
        var momScores = assets.Select(a => momentum.ComputeScores(assets, returns)[a]).ToArray();

        var zVal = ZScorePopulation(valueScores);
        var zMom = ZScorePopulation(momScores);
        // For z-scored vectors, Pearson correlation == mean(zVal·zMom).
        var correlation = zVal.Zip(zMom, (a, b) => a * b).Sum() / zVal.Length;

        Assert.True(correlation < 0m,
            $"value & momentum must anti-correlate on the loser-loser panel; got corr={correlation}");
    }

    // ── Fidelity: variance-reduction — Var(composite) strictly below the mean of the component z-vars. ─
    [Fact]
    public void ComputeScores_VarianceReduction_CompositeVarStrictlyBelowMeanComponentVar()
    {
        var assets = new[] { Sym("A"), Sym("B"), Sym("C"), Sym("D"), Sym("E") };
        var returns = new[]
        {
            Constant(-0.02m, Len),
            Constant(-0.01m, Len),
            Constant(0.005m, Len),
            Constant(0.01m, Len),
            Constant(0.02m, Len),
        };

        var (value, momentum) = Legs();
        var composite = new ValueMomentumCompositeSignal(value, momentum, 0.5m, 0.5m);
        var scores = composite.ComputeScores(assets, returns);

        var compositeVals = assets.Select(a => scores[a]).ToArray();
        var varComposite = PopulationVariance(compositeVals);

        // The two z-scored components each have population variance exactly 1 => mean is 1.
        var zVal = ZScorePopulation(assets.Select(a => value.ComputeScores(assets, returns)[a]).ToArray());
        var zMom = ZScorePopulation(assets.Select(a => momentum.ComputeScores(assets, returns)[a]).ToArray());
        var varZval = PopulationVariance(zVal);
        var varZmom = PopulationVariance(zMom);
        var meanComponentVar = (varZval + varZmom) / 2m;

        // (1) The reference-planned §M-4 property, verbatim: Var(composite) < mean(Var(value), Var(momentum)).
        Assert.True(varComposite < meanComponentVar,
            $"Var(composite)={varComposite} must be strictly below mean component var={meanComponentVar}");

        // (2) The genuinely-discriminating diversification content: a 50/50 blend of two UNIT-variance
        // legs always has variance <= mean (an identity for corr < 1), so (1) alone is near-vacuous. The
        // real signal of NEGATIVE correlation is that the composite variance falls below the
        // zero-correlation baseline 0.25·(varZv + varZm): var(0.5 zV + 0.5 zM) = 0.25(varZv+varZm) +
        // 0.5·cov, which is below the baseline iff cov < 0. This assertion FAILS if the blend sign is
        // flipped or a single leg is used (cov >= 0), so the test is non-vacuous.
        var zeroCorrelationBaseline = 0.25m * (varZval + varZmom);
        Assert.True(varComposite < zeroCorrelationBaseline,
            $"Var(composite)={varComposite} must fall below the zero-correlation baseline " +
            $"{zeroCorrelationBaseline} (proof the negative correlation actually diversifies)");
    }

    // ── Omission rule: an asset omitted by EITHER leg is omitted from the composite. ────────────────
    [Fact]
    public void ComputeScores_EitherLegOmits_AssetOmittedFromComposite()
    {
        var assets = new[] { Sym("A"), Sym("B"), Sym("SHORT") };
        var returns = new[]
        {
            Constant(0.01m, Len),      // scored by both
            Constant(0.015m, Len),     // scored by both
            Constant(0.02m, Len - 1),  // 19 < value-required 20 => value omits => composite omits
        };

        var composite = new ValueMomentumCompositeSignal(ValueLb, MomLb, MomSkip, Dpm);
        var scores = composite.ComputeScores(assets, returns);

        Assert.Equal(2, scores.Count);
        Assert.True(scores.ContainsKey(assets[0]), "A must be scored");
        Assert.True(scores.ContainsKey(assets[1]), "B must be scored");
        Assert.False(scores.ContainsKey(assets[2]),
            "SHORT must be omitted from the composite (its value leg omitted it)");
    }

    // ── Degenerate: zero cross-sectional variance in a z-scored leg => CalculationException. ────────
    [Fact]
    public void ComputeScores_ZeroCrossSectionalVariance_ThrowsCalculationException()
    {
        var assets = new[] { Sym("A"), Sym("B") };
        // Identical returns => identical value & momentum scores => zero cross-sectional variance.
        var returns = new[] { Constant(0.01m, Len), Constant(0.01m, Len) };

        var composite = new ValueMomentumCompositeSignal(ValueLb, MomLb, MomSkip, Dpm);

        var compatible = Assert.ThrowsAny<Boutquin.Trading.Domain.Exceptions.CalculationException>(
            () => composite.ComputeScores(assets, returns));
        var discriminated = Assert.Throws<Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException>(
            () => composite.ComputeScores(assets, returns));

        Assert.Contains("zero cross-sectional variance", compatible.Message, StringComparison.Ordinal);
        Assert.Contains("zero cross-sectional variance", discriminated.Message, StringComparison.Ordinal);
    }

    // ── Configurable weights: momentumWeight = 0 => composite == valueWeight·z(value). ─────────────
    [Fact]
    public void ComputeScores_MomentumWeightZero_ComposesPureValue()
    {
        var assets = new[] { Sym("A"), Sym("B"), Sym("C") };
        var returns = new[]
        {
            Constant(0.02m, Len),
            Constant(0.005m, Len),
            Constant(-0.01m, Len),
        };

        var (value, momentum) = Legs();
        var composite = new ValueMomentumCompositeSignal(value, momentum, valueWeight: 1.0m, momentumWeight: 0.0m);
        var scores = composite.ComputeScores(assets, returns);

        var zVal = ZScorePopulation(assets.Select(a => value.ComputeScores(assets, returns)[a]).ToArray());
        for (var i = 0; i < assets.Length; i++)
        {
            Assert.True(Math.Abs(scores[assets[i]] - zVal[i]) < 1e-12m,
                $"with momentumWeight=0 the composite must equal z(value) for {assets[i]}");
        }
    }

    // ── Contract: empty asset list => empty dictionary (never null). ───────────────────────────────
    [Fact]
    public void ComputeScores_EmptyAssets_ReturnsEmptyDictionary()
    {
        var composite = new ValueMomentumCompositeSignal(ValueLb, MomLb, MomSkip, Dpm);
        var scores = composite.ComputeScores([], []);

        Assert.NotNull(scores);
        Assert.Empty(scores);
    }

    // ── Contract: mismatched lengths surfaced (via the legs' positional-alignment guard). ──────────
    [Fact]
    public void ComputeScores_MismatchedLengths_Throws()
    {
        var assets = new[] { Sym("A"), Sym("B") };
        var returns = new[] { Constant(0.01m, Len) };

        var composite = new ValueMomentumCompositeSignal(ValueLb, MomLb, MomSkip, Dpm);
        Assert.Throws<ArgumentException>(() => composite.ComputeScores(assets, returns));
    }

    // ── Guard: negative weight rejected at construction. ───────────────────────────────────────────
    [Fact]
    public void Constructor_NegativeWeight_Throws()
    {
        var (value, momentum) = Legs();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ValueMomentumCompositeSignal(value, momentum, valueWeight: -0.1m, momentumWeight: 0.5m));
    }
}
