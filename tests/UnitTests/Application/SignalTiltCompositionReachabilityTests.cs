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

/// <summary>
/// M-9 anti-dormancy reachability pin: proves the factor-premia <b>signal layer is reachable end-to-end
/// through the production construction-model entry point</b> — the exact composition the
/// <c>src/Examples</c> Signal-Tilt example runs. A <see cref="SignalTiltConstruction"/> wraps a base
/// model with real cross-sectional signals (<see cref="CrossSectionalMomentumSignal"/> +
/// <see cref="CarrySignal"/>) and constructor-bound auxiliary series; calling the production
/// <see cref="IPortfolioConstructionModel.ComputeTargetWeights(IReadOnlyList{Symbol}, decimal[][])"/> must yield weights <i>tilted away</i>
/// from the untilted base — demonstrating the layer is wired, not dormant scaffolding.
/// </summary>
public sealed class SignalTiltCompositionReachabilityTests
{
    private static readonly Symbol s_spy = new("SPY");   // equity sleeve
    private static readonly Symbol s_tlt = new("TLT");   // bond sleeve

    /// <summary>
    /// Builds the same signal-tilt composition the Examples entry runs and drives it through the
    /// production <see cref="IPortfolioConstructionModel.ComputeTargetWeights(IReadOnlyList{Symbol}, decimal[][])"/> path. Asserts the
    /// composed decorator produces a valid, tilted allocation (differs from the equal-weight base and
    /// remains a normalized long-only weight vector) — the signal layer is reachable, not inert.
    /// </summary>
    [Fact]
    public void SignalTiltComposition_OverBaseModel_ProducesTiltedWeightsThroughProductionEntry()
    {
        var assets = new[] { s_spy, s_tlt };

        // Deterministic return histories long enough for the 6-month cross-sectional momentum lookback,
        // with divergent drift so momentum ranks the two assets differently.
        var returns = new[]
        {
            BuildTrendingReturns(length: 200, perStepDrift: 0.0010m, seed: 1),
            BuildTrendingReturns(length: 200, perStepDrift: -0.0005m, seed: 2),
        };

        // Auxiliary (non-price) series — the carry signal's real inputs, constructor-bound in the
        // positional single-evaluation form (multi-rebalance backtests bind a DatedAuxiliarySeries
        // and go through the date-aware overload instead).
        var auxiliarySeries = new Dictionary<Symbol, decimal[]>
        {
            [s_spy] = [0.018m],           // SPY trailing income (dividend) yield — equity carry
            [new Symbol("DGS10")] = [0.045m], // FRED 10y — bond long curve point
            [new Symbol("DGS2")] = [0.043m],  // FRED 2y  — bond short curve point
        };

        var baseModel = new EqualWeightConstruction();

        var momentum = new CrossSectionalMomentumSignal(lookbackMonths: 6, skipMonths: 1);
        var carry = new CarrySignal(new Dictionary<Symbol, CarryAssetClass>
        {
            [s_spy] = CarryAssetClass.Equity,
            [s_tlt] = CarryAssetClass.Bond,
        });

        var composed = new SignalTiltConstruction(
            baseModel,
            [(momentum, 0.20m), (carry, 0.20m)],
            auxiliarySeries);

        // The untilted reference — the base model's own allocation for the same universe.
        var baseWeights = baseModel.ComputeTargetWeights(assets, returns);

        // Production entry point: exactly what ConstructionModelStrategy calls each rebalance.
        var tilted = composed.ComputeTargetWeights(assets, returns);

        // Reachability: the composition ran and returned a full, valid long-only allocation.
        tilted.Should().ContainKeys(s_spy, s_tlt);
        tilted.Values.Sum().Should().BeApproximately(1m, 1e-9m);
        tilted.Values.Should().OnlyContain(w => w >= 0m);

        // Anti-dormancy: the signal layer actually moved the allocation off the equal-weight base —
        // the tilt is applied, not silently dropped.
        tilted[s_spy].Should().NotBe(baseWeights[s_spy]);
    }

    /// <summary>
    /// A deterministic monotone-drift return series: each step is <paramref name="perStepDrift"/> plus a
    /// small bounded pseudo-random wobble, giving the momentum signal a stable cross-sectional ranking.
    /// </summary>
    private static decimal[] BuildTrendingReturns(int length, decimal perStepDrift, int seed)
    {
        var rng = new Random(seed);
        var series = new decimal[length];
        for (var i = 0; i < length; i++)
        {
            series[i] = perStepDrift + (decimal)((rng.NextDouble() - 0.5) * 0.002);
        }

        return series;
    }
}
