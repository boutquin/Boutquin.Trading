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

using Boutquin.Trading.Application.CovarianceEstimators;
using Boutquin.Trading.Application.DownsideRisk;
using Boutquin.Trading.Application.ExpectedReturns;
using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Domain.ValueObjects;
using FluentAssertions;

/// <summary>
/// Tests for the expected-return estimator mirror of the as-of channel: the
/// <see cref="IExpectedReturnEstimator"/> date-aware overload, the
/// <see cref="YieldAnchoredExpectedReturnEstimator"/> <see cref="DatedAuxiliarySeries"/> binding, and
/// the <c>asOf</c> forwarding by the estimator-consuming construction models
/// (<see cref="MeanVarianceConstruction"/>, <see cref="MeanDownsideRiskConstruction"/>,
/// <see cref="RobustMeanVarianceConstruction"/>). Same defect class as the signal channel: a
/// once-bound positional series fed every historical rebalance the run-end yield.
/// </summary>
public sealed class ExpectedReturnAsOfChannelTests
{
    private static readonly Symbol s_bondA = new("AGG");
    private static readonly Symbol s_bondB = new("TLT");

    private static DateOnly D(int day) => new DateOnly(2024, 1, 1).AddDays(day - 1);

    private static readonly IReadOnlyDictionary<Symbol, ExpectedReturnAssetClass> s_bondClasses =
        new Dictionary<Symbol, ExpectedReturnAssetClass>
        {
            [s_bondA] = ExpectedReturnAssetClass.Bond,
            [s_bondB] = ExpectedReturnAssetClass.Bond,
        };

    /// <summary>
    /// Per-symbol current-yield series: bond A's yield ranks above B's early and below B's late, so
    /// one bound instance must produce different expected-return orderings at the two dates.
    /// </summary>
    private static DatedAuxiliarySeries DatedYields() => new(
        new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>
        {
            [s_bondA] = [(D(1), 0.050m), (D(15), 0.020m)],
            [s_bondB] = [(D(1), 0.030m), (D(15), 0.040m)],
        });

    private static (List<Symbol> Assets, decimal[][] Returns) BondUniverse()
        => ([s_bondA, s_bondB], [[0.001m, 0.002m, 0.001m], [0.002m, 0.001m, 0.002m]]);

    [Fact]
    public void HistoricalMean_DateAwareOverload_IsByteIdenticalToDateFree()
    {
        var (assets, returns) = BondUniverse();
        IExpectedReturnEstimator estimator = new HistoricalMeanExpectedReturnEstimator();

        var dated = estimator.EstimateExpectedReturns(assets, returns, auxiliarySeries: null, D(10));

        dated.Should().Equal(estimator.EstimateExpectedReturns(assets, returns),
            "the default interface method forwards to the date-free overload");
    }

    [Fact]
    public void DatedYields_UseOnlyObservationsAtOrBeforeAsOf()
    {
        var (assets, returns) = BondUniverse();
        var dated = new YieldAnchoredExpectedReturnEstimator(s_bondClasses, DatedYields());

        var means = dated.EstimateExpectedReturns(assets, returns, auxiliarySeries: null, D(10));

        // At D(10) only the D(1) observations are known: A = 5.0%, B = 3.0%. The D(15) revisions
        // (A down to 2.0%, B up to 4.0%) must be invisible — the reference is the hand-truncated
        // positional binding, the only correct positional use for a historical evaluation date.
        var reference = new YieldAnchoredExpectedReturnEstimator(
            s_bondClasses,
            new Dictionary<Symbol, decimal[]>
            {
                [s_bondA] = [0.050m],
                [s_bondB] = [0.030m],
            });

        means.Should().Equal(reference.EstimateExpectedReturns(assets, returns));
        means.Should().Equal(0.050m, 0.030m);
    }

    [Fact]
    public void DatedYields_ExpectedReturnOrdering_FollowsAsOfDate()
    {
        var (assets, returns) = BondUniverse();
        var dated = new YieldAnchoredExpectedReturnEstimator(s_bondClasses, DatedYields());

        var early = dated.EstimateExpectedReturns(assets, returns, auxiliarySeries: null, D(10));
        var late = dated.EstimateExpectedReturns(assets, returns, auxiliarySeries: null, D(20));

        early[0].Should().BeGreaterThan(early[1], "at the early date A's yield (5%) dominates B's (3%)");
        late[0].Should().BeLessThan(late[1], "at the late date A's yield (2%) trails B's (4%)");
    }

    [Fact]
    public void DatedYields_AsOfBeyondLastObservation_UsesFullSeriesWithoutThrowing()
    {
        var (assets, returns) = BondUniverse();
        var dated = new YieldAnchoredExpectedReturnEstimator(s_bondClasses, DatedYields());

        var means = dated.EstimateExpectedReturns(assets, returns, auxiliarySeries: null, D(365));

        // Beyond the last observation the slice is the full series — the latest revisions apply.
        means.Should().Equal(0.020m, 0.040m);
    }

    [Fact]
    public void DatedYields_AsOfBeforeFirstObservation_ThrowsNamingTheSymbols()
    {
        var (assets, returns) = BondUniverse();
        var dated = new YieldAnchoredExpectedReturnEstimator(s_bondClasses, DatedYields());

        // Before the first observation the slice of every required series is empty, so no asset is
        // estimable — the positional, fully-populated contract surfaces this as CalculationException
        // naming the symbols, never a silent fallback.
        var act = () => dated.EstimateExpectedReturns(
            assets, returns, auxiliarySeries: null, new DateOnly(2023, 12, 31));

        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>()
            .WithMessage("*AGG*").WithMessage("*TLT*");
    }

    [Fact]
    public void DatedInstance_DateFreeOverload_ThrowsInsteadOfReadingUnslicedTail()
    {
        var (assets, returns) = BondUniverse();
        var dated = new YieldAnchoredExpectedReturnEstimator(s_bondClasses, DatedYields());

        var act = () => dated.EstimateExpectedReturns(assets, returns);

        act.Should().Throw<InvalidOperationException>().WithMessage("*date-aware*asOf*");
    }

    [Fact]
    public void PositionalInstance_DateAwareOverload_IsByteIdenticalToDateFree()
    {
        var (assets, returns) = BondUniverse();
        var positional = new YieldAnchoredExpectedReturnEstimator(
            s_bondClasses,
            new Dictionary<Symbol, decimal[]>
            {
                [s_bondA] = [0.050m],
                [s_bondB] = [0.030m],
            });

        var dated = positional.EstimateExpectedReturns(assets, returns, auxiliarySeries: null, D(10));

        dated.Should().Equal(positional.EstimateExpectedReturns(assets, returns),
            "a positional-series instance has no dates to slice by; the overloads coincide");
    }

    /// <summary>
    /// Hand-written (not mocked) estimator recording every evaluation date it receives and counting
    /// date-free calls, so each model's <c>asOf</c> forwarding is observable.
    /// </summary>
    private sealed class AsOfSpyEstimator : IExpectedReturnEstimator
    {
        public List<DateOnly> ReceivedAsOfDates { get; } = [];

        public int DateFreeCalls { get; private set; }

        public decimal[] EstimateExpectedReturns(
            IReadOnlyList<Symbol> assets,
            decimal[][] returns,
            IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null)
        {
            DateFreeCalls++;
            return Means(assets);
        }

        public decimal[] EstimateExpectedReturns(
            IReadOnlyList<Symbol> assets,
            decimal[][] returns,
            IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries,
            DateOnly asOf)
        {
            ReceivedAsOfDates.Add(asOf);
            return Means(assets);
        }

        private static decimal[] Means(IReadOnlyList<Symbol> assets)
            => [.. assets.Select((_, i) => 0.01m + (0.001m * i))];
    }

    public static IEnumerable<object[]> EstimatorConsumingModels()
    {
        yield return
        [
            (Func<IExpectedReturnEstimator, IPortfolioConstructionModel>)(e =>
                new MeanVarianceConstruction(e)),
            "MeanVariance",
        ];
        yield return
        [
            (Func<IExpectedReturnEstimator, IPortfolioConstructionModel>)(e =>
                new MeanDownsideRiskConstruction(e, new DownsideDeviationRiskMeasure())),
            "MeanDownsideRisk",
        ];
        yield return
        [
            (Func<IExpectedReturnEstimator, IPortfolioConstructionModel>)(e =>
                new RobustMeanVarianceConstruction(e, new SampleCovarianceEstimator())),
            "RobustMeanVariance",
        ];
    }

    [Theory]
    [MemberData(nameof(EstimatorConsumingModels))]
    public void EstimatorConsumingModel_DateAwareOverload_ForwardsAsOfToEstimator(
        Func<IExpectedReturnEstimator, IPortfolioConstructionModel> factory,
        string modelName)
    {
        var (assets, returns) = BondUniverse();
        var spy = new AsOfSpyEstimator();
        var model = factory(spy);

        model.ComputeTargetWeights(assets, returns, D(10));

        spy.ReceivedAsOfDates.Should().ContainSingle(
            $"{modelName}'s date-aware overload must forward the evaluation date to the estimator")
            .Which.Should().Be(D(10));
        spy.DateFreeCalls.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(EstimatorConsumingModels))]
    public void EstimatorConsumingModel_DateFreeOverload_CallsDateFreeEstimator(
        Func<IExpectedReturnEstimator, IPortfolioConstructionModel> factory,
        string modelName)
    {
        var (assets, returns) = BondUniverse();
        var spy = new AsOfSpyEstimator();
        var model = factory(spy);

        model.ComputeTargetWeights(assets, returns);

        spy.DateFreeCalls.Should().Be(1,
            $"{modelName}'s date-free overload never fabricates an evaluation date");
        spy.ReceivedAsOfDates.Should().BeEmpty();
    }

    [Fact]
    public void MeanVariance_WithDatedYieldAnchored_ComputesValidWeightsThroughTheDatedPath()
    {
        var (assets, returns) = BondUniverse();
        var estimator = new YieldAnchoredExpectedReturnEstimator(s_bondClasses, DatedYields());
        var model = new MeanVarianceConstruction(estimator);

        var weights = model.ComputeTargetWeights(assets, returns, D(10));

        weights.Should().HaveCount(2);
        weights.Values.Sum().Should().BeApproximately(1m, 1e-10m);
    }
}
