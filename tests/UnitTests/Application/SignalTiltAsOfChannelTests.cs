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
using Boutquin.Trading.Application.PositionSizing;
using Boutquin.Trading.Application.Signals;
using Boutquin.Trading.Application.Strategies;
using Boutquin.Trading.Domain.ValueObjects;
using FluentAssertions;

/// <summary>
/// Tests for the as-of / date-indexed auxiliary-series channel: the
/// <see cref="IPortfolioConstructionModel"/> date-aware overload, the
/// <see cref="SignalTiltConstruction"/> <see cref="DatedAuxiliarySeries"/> binding, decorator
/// forwarding, and the rebalance-loop threading in <see cref="ConstructionModelStrategy"/>. The
/// channel exists to close a structural lookahead: a run-long positional auxiliary binding fed every
/// historical rebalance the run-end series tail.
/// </summary>
public sealed class SignalTiltAsOfChannelTests
{
    private static readonly Symbol s_a = new("AAA");
    private static readonly Symbol s_b = new("BBB");
    private static readonly Symbol s_dgs3Mo = new("DGS3MO");

    private static DateOnly D(int day) => new DateOnly(2024, 1, 1).AddDays(day - 1);

    /// <summary>
    /// Two up-trending assets over the 10-day lookback of a small
    /// <see cref="TimeSeriesMomentumSignal"/> (2 months × 5 trading days), asset A low-vol and asset
    /// B high-vol, so both score positive (inverse-vol) and the cross-section tilts toward A.
    /// </summary>
    private static (List<Symbol> Assets, decimal[][] Returns) TsmomUniverse()
    {
        var a = new decimal[10];
        var b = new decimal[10];
        for (var i = 0; i < 10; i++)
        {
            a[i] = i % 2 == 0 ? 0.012m : 0.008m;   // up-trend, low vol
            b[i] = i % 2 == 0 ? 0.030m : -0.005m;  // up-trend, high vol
        }

        return ([s_a, s_b], [a, b]);
    }

    private static TimeSeriesMomentumSignal SmallTsmom() => new(
        lookbackMonths: 2,
        tradingDaysPerMonth: 5,
        volatilityWindow: 2);

    /// <summary>
    /// A daily risk-free-return series whose first 10 observations (through the as-of date) are a
    /// negligible 1bp/day, and whose later observations are an absurd 50%/day: any leak of the
    /// run-end tail into the trailing risk-free window flips every excess-return sign to negative.
    /// </summary>
    private static DatedAuxiliarySeries DatedRiskFree()
    {
        var observations = new List<(DateOnly Date, decimal Value)>(20);
        for (var day = 1; day <= 20; day++)
        {
            observations.Add((D(day), day <= 10 ? 0.0001m : 0.5m));
        }

        return new DatedAuxiliarySeries(new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>
        {
            [s_dgs3Mo] = observations,
        });
    }

    [Fact]
    public void DatedAux_TsmomScores_MatchPlainAuxManuallyTruncatedAtAsOf()
    {
        var (assets, returns) = TsmomUniverse();
        var signal = SmallTsmom();

        var dated = new SignalTiltConstruction(
            new EqualWeightConstruction(), [(signal, 0.1m)], DatedRiskFree());

        // The reference: positional aux the caller truncated to the as-of date by hand (the only
        // correct way to use the positional channel for a historical evaluation date).
        var truncated = new decimal[10];
        Array.Fill(truncated, 0.0001m);
        var plainTruncated = new SignalTiltConstruction(
            new EqualWeightConstruction(),
            [(signal, 0.1m)],
            new Dictionary<Symbol, decimal[]> { [s_dgs3Mo] = truncated });

        var datedWeights = dated.ComputeTargetWeights(assets, returns, D(10));
        var referenceWeights = plainTruncated.ComputeTargetWeights(assets, returns);

        datedWeights.Should().BeEquivalentTo(referenceWeights);
    }

    [Fact]
    public void DatedAux_EliminatesRunEndTailLookahead()
    {
        var (assets, returns) = TsmomUniverse();
        var signal = SmallTsmom();

        var dated = new SignalTiltConstruction(
            new EqualWeightConstruction(), [(signal, 0.1m)], DatedRiskFree());

        // The buggy pattern this channel replaces: the FULL series bound positionally, so the
        // signal's trailing window reads the run-end tail (50%/day) at a historical rebalance.
        var fullTail = new decimal[20];
        for (var i = 0; i < 20; i++)
        {
            fullTail[i] = i < 10 ? 0.0001m : 0.5m;
        }

        var plainFullSeries = new SignalTiltConstruction(
            new EqualWeightConstruction(),
            [(signal, 0.1m)],
            new Dictionary<Symbol, decimal[]> { [s_dgs3Mo] = fullTail });

        var datedWeights = dated.ComputeTargetWeights(assets, returns, D(10));
        var lookaheadWeights = plainFullSeries.ComputeTargetWeights(assets, returns);

        // With the correctly dated risk-free leg both assets clear the hurdle and the low-vol asset
        // is tilted over the equal-weight base.
        datedWeights[s_a].Should().BeGreaterThan(0.5m);
        datedWeights[s_b].Should().BeLessThan(0.5m);

        // Under the leaked run-end hurdle every excess trend is negative: all scores clamp to zero,
        // no dispersion, no tilt — the signal is silently inert and the weights stay at base.
        lookaheadWeights[s_a].Should().Be(0.5m);
        lookaheadWeights[s_b].Should().Be(0.5m);

        datedWeights.Should().NotBeEquivalentTo(lookaheadWeights);
    }

    [Fact]
    public void DatedAux_EquityCarry_TiltDirectionFollowsAsOfDate()
    {
        var assets = new List<Symbol> { s_a, s_b };
        var returns = new[] { new[] { 0.01m }, new[] { 0.01m } };

        var carry = new CarrySignal(new Dictionary<Symbol, CarryAssetClass>
        {
            [s_a] = CarryAssetClass.Equity,
            [s_b] = CarryAssetClass.Equity,
        });

        // Per-asset trailing income yields: A dominates early, B dominates late. One bound
        // instance must rank them differently at the two evaluation dates.
        var dated = new DatedAuxiliarySeries(new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>
        {
            [s_a] = [(D(1), 0.05m), (D(15), 0.01m)],
            [s_b] = [(D(1), 0.02m), (D(15), 0.04m)],
        });

        var tilt = new SignalTiltConstruction(new EqualWeightConstruction(), [(carry, 0.1m)], dated);

        var early = tilt.ComputeTargetWeights(assets, returns, D(10));
        var late = tilt.ComputeTargetWeights(assets, returns, D(20));

        early[s_a].Should().BeGreaterThan(early[s_b], "at the early date A's income yield (5%) dominates B's (2%)");
        late[s_a].Should().BeLessThan(late[s_b], "at the late date A's income yield (1%) trails B's (4%)");
    }

    [Fact]
    public void PositionalAux_DateAwareOverload_IsByteIdenticalToDateFreeOverload()
    {
        var (assets, returns) = TsmomUniverse();
        var aux = new Dictionary<Symbol, decimal[]> { [s_dgs3Mo] = Enumerable.Repeat(0.0001m, 10).ToArray() };

        var tilt = new SignalTiltConstruction(new EqualWeightConstruction(), [(SmallTsmom(), 0.1m)], aux);

        var dateFree = tilt.ComputeTargetWeights(assets, returns);
        var dateAware = tilt.ComputeTargetWeights(assets, returns, D(10));

        dateAware.Should().BeEquivalentTo(dateFree,
            "a positional-series instance has no dates to slice by; the caller owns alignment and the overloads coincide");
    }

    [Fact]
    public void BaseModel_DefaultInterfaceMethod_ForwardsToDateFreeOverload()
    {
        var (assets, returns) = TsmomUniverse();
        IPortfolioConstructionModel model = new EqualWeightConstruction();

        var dateAware = model.ComputeTargetWeights(assets, returns, D(10));

        dateAware.Should().BeEquivalentTo(model.ComputeTargetWeights(assets, returns));
    }

    [Fact]
    public void DatedAux_DateFreeOverload_ThrowsInsteadOfReadingUnslicedTail()
    {
        var (assets, returns) = TsmomUniverse();
        var tilt = new SignalTiltConstruction(
            new EqualWeightConstruction(), [(SmallTsmom(), 0.1m)], DatedRiskFree());

        var act = () => tilt.ComputeTargetWeights(assets, returns);

        act.Should().Throw<InvalidOperationException>().WithMessage("*date-aware*asOf*");
    }

    [Fact]
    public void TacticalOverlay_DateAwareOverload_ForwardsAsOfToDatedInnerTilt()
    {
        var (assets, returns) = TsmomUniverse();
        var innerTilt = new SignalTiltConstruction(
            new EqualWeightConstruction(), [(SmallTsmom(), 0.1m)], DatedRiskFree());

        var noTilts = new Dictionary<Symbol, decimal>();
        var overlay = new TacticalOverlayConstruction(
            innerTilt,
            new Dictionary<EconomicRegime, IReadOnlyDictionary<Symbol, decimal>>
            {
                [EconomicRegime.RisingGrowthRisingInflation] = noTilts,
                [EconomicRegime.RisingGrowthFallingInflation] = noTilts,
                [EconomicRegime.FallingGrowthRisingInflation] = noTilts,
                [EconomicRegime.FallingGrowthFallingInflation] = noTilts,
            },
            EconomicRegime.RisingGrowthRisingInflation);

        // Date-aware outer call reaches the dated inner tilt with the evaluation date; a no-tilt
        // overlay is weight-preserving, so the result equals the inner date-aware result.
        var outer = overlay.ComputeTargetWeights(assets, returns, D(10));
        outer.Should().BeEquivalentTo(innerTilt.ComputeTargetWeights(assets, returns, D(10)));

        // The date-free outer call has no date to forward — the dated inner instance fails loud.
        var act = () => overlay.ComputeTargetWeights(assets, returns);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TurnoverPenalized_DateAwareOverload_ForwardsAsOfToDatedInnerTilt()
    {
        var (assets, returns) = TsmomUniverse();
        var innerTilt = new SignalTiltConstruction(
            new EqualWeightConstruction(), [(SmallTsmom(), 0.1m)], DatedRiskFree());

        var penalized = new TurnoverPenalizedConstruction(innerTilt, lambda: 0m);

        var outer = penalized.ComputeTargetWeights(assets, returns, D(10));

        outer.Should().BeEquivalentTo(innerTilt.ComputeTargetWeights(assets, returns, D(10)));
    }

    /// <summary>
    /// Hand-written (not mocked) model implementing the date-aware overload, recording every
    /// evaluation date it receives and counting date-free calls, so the strategy's rebalance-loop
    /// threading is observable.
    /// </summary>
    private sealed class AsOfSpyModel : IPortfolioConstructionModel
    {
        public List<DateOnly> ReceivedAsOfDates { get; } = [];

        public int DateFreeCalls { get; private set; }

        public IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeights(
            IReadOnlyList<Symbol> assets,
            decimal[][] returns)
        {
            DateFreeCalls++;
            return EqualWeights(assets);
        }

        public IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeights(
            IReadOnlyList<Symbol> assets,
            decimal[][] returns,
            DateOnly asOf)
        {
            ReceivedAsOfDates.Add(asOf);
            return EqualWeights(assets);
        }

        private static Dictionary<Symbol, decimal> EqualWeights(IReadOnlyList<Symbol> assets)
            => assets.ToDictionary(a => a, _ => 1m / assets.Count);
    }

    [Fact]
    public void ConstructionModelStrategy_PassesRebalanceTimestampAsEvaluationDate()
    {
        var spy = new AsOfSpyModel();
        var strategy = new ConstructionModelStrategy(
            "AsOf Threading Test",
            new Dictionary<Symbol, Currency> { [s_a] = Currency.USD, [s_b] = Currency.USD },
            new SortedDictionary<Currency, decimal> { [Currency.USD] = 100_000m },
            new ClosePriceOrderPriceCalculationStrategy(),
            new DynamicWeightPositionSizer(Currency.USD),
            spy,
            RebalancingFrequency.Monthly,
            lookbackWindow: 5);

        var marketData = new Dictionary<DateOnly, SortedDictionary<Symbol, Bar>>();
        var fxRates = new Dictionary<DateOnly, SortedDictionary<Currency, decimal>>();
        var start = new DateOnly(2024, 1, 2);
        var priceA = 100m;
        var priceB = 50m;
        for (var i = 0; i < 70; i++)
        {
            var date = start.AddDays(i);
            priceA *= 1.001m;
            priceB *= 0.999m;
            marketData[date] = new SortedDictionary<Symbol, Bar>
            {
                [s_a] = new(date, priceA, priceA, priceA, priceA, priceA, 1_000),
                [s_b] = new(date, priceB, priceB, priceB, priceB, priceB, 1_000),
            };
            fxRates[date] = new SortedDictionary<Currency, decimal> { [Currency.USD] = 1m };
        }

        // First call: a single day of history — insufficient data, the model is not invoked.
        strategy.GenerateSignals(start, Currency.USD, marketData, fxRates);

        // Two later scheduled rebalances with sufficient history invoke the model date-aware; each
        // must receive its own timestamp (a single observed date cannot distinguish correct
        // threading from a once-captured constant — the defect class this channel closes).
        var firstRebalanceDate = new DateOnly(2024, 2, 5);
        strategy.GenerateSignals(firstRebalanceDate, Currency.USD, marketData, fxRates);
        var secondRebalanceDate = new DateOnly(2024, 3, 5);
        strategy.GenerateSignals(secondRebalanceDate, Currency.USD, marketData, fxRates);

        spy.ReceivedAsOfDates.Should().Equal(
            [firstRebalanceDate, secondRebalanceDate],
            "each rebalance passes its own timestamp — the evaluation date the model must not see past");
        spy.ReceivedAsOfDates.Distinct().Should().HaveCount(2, "the observed dates must be distinct across rebalances");
        spy.DateFreeCalls.Should().Be(0, "the rebalance loop always uses the date-aware entry point");
    }
}
