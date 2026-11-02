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
using Boutquin.Trading.Application.Signals;
using FluentAssertions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fidelity tests for <see cref="SignalTiltConstruction"/> — the M-1 signal-tilt decorator.
/// </summary>
public sealed class SignalTiltConstructionTests
{
    private static readonly Symbol s_a = new("AAA");
    private static readonly Symbol s_b = new("BBB");
    private static readonly Symbol s_c = new("CCC");

    /// <summary>
    /// A hand-written <see cref="ICrossSectionalSignal"/> returning a fixed per-asset score map.
    /// Assets absent from the map model the insufficient-history omission.
    /// </summary>
    private sealed class StubSignal : ICrossSectionalSignal
    {
        private readonly IReadOnlyDictionary<Symbol, decimal> _scores;

        public StubSignal(IReadOnlyDictionary<Symbol, decimal> scores) => _scores = scores;

        public IReadOnlyDictionary<Symbol, decimal> ComputeScores(
            IReadOnlyList<Symbol> assets,
            decimal[][] returns,
            IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null)
        {
            var result = new Dictionary<Symbol, decimal>();
            foreach (var asset in assets)
            {
                if (_scores.TryGetValue(asset, out var score))
                {
                    result[asset] = score;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// A signal whose <see cref="ICrossSectionalSignal.ComputeScores"/> always throws — models a
    /// degenerate cross-section (e.g. zero-dispersion z-score) that a real signal like
    /// <c>LowVolatilitySignal</c> surfaces as <see cref="Boutquin.Trading.Domain.Exceptions.CalculationException"/>.
    /// </summary>
    private sealed class ThrowingSignal : ICrossSectionalSignal
    {
        public IReadOnlyDictionary<Symbol, decimal> ComputeScores(
            IReadOnlyList<Symbol> assets,
            decimal[][] returns,
            IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null)
            => throw new Boutquin.Trading.Domain.Exceptions.CalculationException(
                "Degenerate cross-section: zero-dispersion z-score is undefined.");
    }

    private static IPortfolioConstructionModel EqualWeightBase(params Symbol[] assets)
    {
        var mock = new Mock<IPortfolioConstructionModel>();
        var w = 1m / assets.Length;
        var weights = assets.ToDictionary(a => a, _ => w);
        mock.Setup(m => m.ComputeTargetWeights(It.IsAny<IReadOnlyList<Symbol>>(), It.IsAny<decimal[][]>()))
            .Returns(weights);
        return mock.Object;
    }

    /// <summary>
    /// Decorator-identity: with every signal strength 0, the decorator output equals the base model's
    /// weights exactly (the defining M-1 fidelity property).
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_WithZeroStrengthSignals_EqualsBaseModelWeights()
    {
        var assets = new List<Symbol> { s_a, s_b, s_c };
        var returns = new[] { new[] { 0.01m }, new[] { 0.02m }, new[] { 0.03m } };
        var baseModel = EqualWeightBase(s_a, s_b, s_c);
        var baseWeights = baseModel.ComputeTargetWeights(assets, returns);

        var signal = new StubSignal(new Dictionary<Symbol, decimal> { [s_a] = 5m, [s_b] = -5m, [s_c] = 0m });
        var decorator = new SignalTiltConstruction(baseModel, [(signal, 0m)]);

        var weights = decorator.ComputeTargetWeights(assets, returns);

        weights.Should().Equal(baseWeights);
    }

    /// <summary>
    /// Decorator-identity holds even for a disabled (strength-0) signal whose scoring would throw over a
    /// degenerate cross-section: a strength-0 signal contributes no tilt, so its scores are never needed
    /// and <see cref="ICrossSectionalSignal.ComputeScores"/> must not be invoked. The decorator returns
    /// the base model's weights rather than propagating the signal's exception.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_WithZeroStrengthThrowingSignal_ReturnsBaseWeightsWithoutThrowing()
    {
        var assets = new List<Symbol> { s_a, s_b, s_c };
        var returns = new[] { new[] { 0.01m }, new[] { 0.02m }, new[] { 0.03m } };
        var baseModel = EqualWeightBase(s_a, s_b, s_c);
        var baseWeights = baseModel.ComputeTargetWeights(assets, returns);

        var decorator = new SignalTiltConstruction(baseModel, [(new ThrowingSignal(), 0m)]);

        var weights = decorator.ComputeTargetWeights(assets, returns);

        weights.Should().Equal(baseWeights);
    }

    /// <summary>
    /// A non-zero-strength signal tilts the weights (higher-scoring asset overweighted, lower-scoring
    /// underweighted), and the post-tilt weights remain non-negative and sum to 1.0 after
    /// clamp + re-normalization.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_WithNonZeroStrength_TiltsAndKeepsWeightsOnSimplex()
    {
        var assets = new List<Symbol> { s_a, s_b, s_c };
        var returns = new[] { new[] { 0.01m }, new[] { 0.02m }, new[] { 0.03m } };
        var baseModel = EqualWeightBase(s_a, s_b, s_c);

        // Scores with cross-sectional dispersion: AAA high, CCC low.
        var signal = new StubSignal(new Dictionary<Symbol, decimal> { [s_a] = 2m, [s_b] = 0m, [s_c] = -2m });
        var decorator = new SignalTiltConstruction(baseModel, [(signal, 0.1m)]);

        var weights = decorator.ComputeTargetWeights(assets, returns);

        weights.Values.Sum().Should().BeApproximately(1m, 1e-12m);
        weights.Values.Should().OnlyContain(w => w >= 0m);
        weights[s_a].Should().BeGreaterThan(1m / 3m);   // high score → overweight vs base
        weights[s_c].Should().BeLessThan(1m / 3m);      // low score → underweight vs base
        weights[s_a].Should().BeGreaterThan(weights[s_c]);
    }

    /// <summary>
    /// An asset omitted by a signal (insufficient history) receives exactly zero tilt: its base weight
    /// is carried through unchanged when the scored cross-section's tilts net to zero (no
    /// re-normalization drift onto the omitted asset).
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_WithAbsentKey_AppliesExactlyZeroTiltToOmittedAsset()
    {
        var assets = new List<Symbol> { s_a, s_b, s_c };
        var returns = new[] { new[] { 0.01m }, new[] { 0.02m }, new[] { 0.03m } };
        var baseModel = EqualWeightBase(s_a, s_b, s_c);

        // CCC is omitted (absent key); AAA/BBB scores are symmetric so their tilts net to zero.
        var signal = new StubSignal(new Dictionary<Symbol, decimal> { [s_a] = 1m, [s_b] = -1m });
        var decorator = new SignalTiltConstruction(baseModel, [(signal, 0.1m)]);

        var weights = decorator.ComputeTargetWeights(assets, returns);

        weights[s_c].Should().BeApproximately(1m / 3m, 1e-12m); // omitted → zero tilt, weight unchanged
        weights.Values.Sum().Should().BeApproximately(1m, 1e-12m);
    }

    /// <summary>
    /// The signal-tilt decorator composes under <see cref="TacticalOverlayConstruction"/> in both nesting
    /// directions without double-normalization drift: the composed weights still sum to 1.0 and stay
    /// non-negative.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_NestedWithTacticalOverlay_KeepsWeightsOnSimplex()
    {
        var assets = new List<Symbol> { s_a, s_b, s_c };
        var returns = new[] { new[] { 0.01m }, new[] { 0.02m }, new[] { 0.03m } };
        var baseModel = EqualWeightBase(s_a, s_b, s_c);
        var signal = new StubSignal(new Dictionary<Symbol, decimal> { [s_a] = 2m, [s_b] = 0m, [s_c] = -2m });

        var regimeTilts = new Dictionary<EconomicRegime, IReadOnlyDictionary<Symbol, decimal>>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new Dictionary<Symbol, decimal>
            {
                [s_a] = 0.05m,
                [s_c] = -0.05m,
            },
        };

        // Direction 1: SignalTilt as the base model of TacticalOverlay.
        var signalTilt = new SignalTiltConstruction(baseModel, [(signal, 0.1m)]);
        var overlayOverSignal = new TacticalOverlayConstruction(
            signalTilt, regimeTilts, EconomicRegime.RisingGrowthRisingInflation);

        // Direction 2: TacticalOverlay as the base model of SignalTilt.
        var overlay = new TacticalOverlayConstruction(
            baseModel, regimeTilts, EconomicRegime.RisingGrowthRisingInflation);
        var signalOverOverlay = new SignalTiltConstruction(overlay, [(signal, 0.1m)]);

        var w1 = overlayOverSignal.ComputeTargetWeights(assets, returns);
        var w2 = signalOverOverlay.ComputeTargetWeights(assets, returns);

        w1.Values.Sum().Should().BeApproximately(1m, 1e-12m);
        w1.Values.Should().OnlyContain(w => w >= 0m);
        w2.Values.Sum().Should().BeApproximately(1m, 1e-12m);
        w2.Values.Should().OnlyContain(w => w >= 0m);
    }

    /// <summary>
    /// Each signal's omitted-asset set is logged at Debug per rebalance, so an omission (a data problem
    /// masquerading as "no tilt") is diagnosable without changing the weight contract.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_WhenSignalOmitsAsset_LogsOmissionAtDebug()
    {
        var assets = new List<Symbol> { s_a, s_b, s_c };
        var returns = new[] { new[] { 0.01m }, new[] { 0.02m }, new[] { 0.03m } };
        var baseModel = EqualWeightBase(s_a, s_b, s_c);
        var logger = new CapturingLogger<SignalTiltConstruction>();

        var signal = new StubSignal(new Dictionary<Symbol, decimal> { [s_a] = 1m, [s_b] = -1m }); // omits CCC
        var decorator = new SignalTiltConstruction(baseModel, [(signal, 0.1m)], auxiliarySeries: null, logger: logger);

        _ = decorator.ComputeTargetWeights(assets, returns);

        logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Debug && e.Message.Contains("omitted") && e.Message.Contains("CCC"));
    }

    [Fact]
    public void ComputeTargetWeights_DegenerateAndShortHistoryOmissions_UseDistinctLogRecords()
    {
        const int lookbackDays = 10;
        var flat = new Symbol("FLAT");
        var shortHistory = new Symbol("SHORT");
        var good1 = new Symbol("GOOD1");
        var good2 = new Symbol("GOOD2");
        var assets = new List<Symbol> { flat, shortHistory, good1, good2 };
        var returns = new[]
        {
            Enumerable.Repeat(0.001m, lookbackDays).ToArray(),
            Enumerable.Repeat(0.001m, lookbackDays - 1).ToArray(),
            Enumerable.Range(0, lookbackDays).Select(i => i % 2 == 0 ? -0.002m : 0.004m).ToArray(),
            Enumerable.Range(0, lookbackDays).Select(i => i % 2 == 0 ? -0.003m : 0.006m).ToArray(),
        };
        var signalLogger = new CapturingLogger<TimeSeriesMomentumSignal>();
        var decoratorLogger = new CapturingLogger<SignalTiltConstruction>();
        var signal = new TimeSeriesMomentumSignal(signalLogger, 2, 5, 4, 252, "DGS3MO");
        var decorator = new SignalTiltConstruction(
            EqualWeightBase([.. assets]), [(signal, 0.1m)], auxiliarySeries: null, logger: decoratorLogger);

        _ = decorator.ComputeTargetWeights(assets, returns);

        var warning = Assert.Single(signalLogger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("FLAT", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SHORT", warning.Message, StringComparison.Ordinal);
        var debug = Assert.Single(decoratorLogger.Entries, entry => entry.Level == LogLevel.Debug);
        Assert.Contains("FLAT", debug.Message, StringComparison.Ordinal);
        Assert.Contains("SHORT", debug.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputeTargetWeights_WhenEveryAssetIsDegenerate_ReturnsBaseWeights()
    {
        const int lookbackDays = 10;
        var assets = new List<Symbol> { s_a, s_b, s_c };
        var returns = assets.Select(_ => Enumerable.Repeat(0.001m, lookbackDays).ToArray()).ToArray();
        var baseModel = EqualWeightBase(s_a, s_b, s_c);
        var baseWeights = baseModel.ComputeTargetWeights(assets, returns);
        var signal = new TimeSeriesMomentumSignal(2, 5, 4, 252, "DGS3MO");
        var decorator = new SignalTiltConstruction(baseModel, [(signal, 0.1m)]);

        var weights = decorator.ComputeTargetWeights(assets, returns);

        weights.Should().Equal(baseWeights);
    }

    /// <summary>
    /// Degenerate: an empty asset list yields an empty dictionary (never null), before any base-model or
    /// signal work.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_WithEmptyAssetList_ReturnsEmptyDictionary()
    {
        var baseModel = EqualWeightBase(s_a);
        var signal = new StubSignal(new Dictionary<Symbol, decimal> { [s_a] = 1m });
        var decorator = new SignalTiltConstruction(baseModel, [(signal, 0.1m)]);

        var weights = decorator.ComputeTargetWeights([], []);

        weights.Should().NotBeNull();
        weights.Should().BeEmpty();
    }

    /// <summary>
    /// Degenerate: a signal whose scored cross-section has zero dispersion (all scores equal) contributes
    /// no tilt — the decorator does not throw and returns the base weights unchanged.
    /// </summary>
    [Fact]
    public void ComputeTargetWeights_WithZeroDispersionScores_AppliesNoTilt()
    {
        var assets = new List<Symbol> { s_a, s_b, s_c };
        var returns = new[] { new[] { 0.01m }, new[] { 0.02m }, new[] { 0.03m } };
        var baseModel = EqualWeightBase(s_a, s_b, s_c);
        var baseWeights = baseModel.ComputeTargetWeights(assets, returns);

        // All scored assets share the same score ⇒ σ = 0 ⇒ no ranking information.
        var signal = new StubSignal(new Dictionary<Symbol, decimal> { [s_a] = 7m, [s_b] = 7m, [s_c] = 7m });
        var decorator = new SignalTiltConstruction(baseModel, [(signal, 0.1m)]);

        var weights = decorator.ComputeTargetWeights(assets, returns);

        weights.Should().Equal(baseWeights);
    }
}
