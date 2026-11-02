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
using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Application.Signals;
using FluentAssertions;

/// <summary>
/// Enumerated byte-identity contract for the date-aware
/// <see cref="IPortfolioConstructionModel.ComputeTargetWeights(IReadOnlyList{Symbol}, decimal[][], DateOnly)"/>
/// overload: for every concrete construction model, a dated call must return results identical to
/// the date-free call on a fixed fixture — date-free models fall through the default interface
/// method, and wrapping decorators forward <c>asOf</c> to a date-free inner model whose result is
/// unchanged. The roster is enumerated explicitly and reconciled against the assembly by
/// reflection (the <see cref="ExhaustiveDiRegistrationTests"/> discipline), so a new model cannot
/// ship without joining this contract.
/// </summary>
public sealed class ConstructionModelAsOfByteIdentityTests
{
    private static readonly List<Symbol> s_assets =
        [new("AAAA"), new("BBBB"), new("CCCC"), new("DDDD")];

    private static readonly decimal[][] s_returns = FixtureReturns();

    private static readonly DateOnly s_anyDate = new(2024, 6, 28);

    /// <summary>
    /// Four positively-correlated return series (shared common factor, per-asset loadings 1.00 to
    /// 1.45) with distinct drifts and idiosyncratic terms, 40 observations each: positive marginal
    /// risk contributions for RiskParity, a well-conditioned covariance for the optimizers, and
    /// enough history for the 10-day momentum lookback in the SignalTilt roster entry.
    /// </summary>
    private static decimal[][] FixtureReturns()
    {
        var returns = new decimal[4][];
        for (var i = 0; i < 4; i++)
        {
            returns[i] = new decimal[40];
            for (var t = 0; t < 40; t++)
            {
                var common = (t % 4) switch
                {
                    0 => 0.010m,
                    1 => -0.006m,
                    2 => 0.008m,
                    _ => -0.004m,
                };
                var idiosyncratic = ((t + (3 * i)) % 5) switch
                {
                    0 => 0.004m,
                    1 => -0.002m,
                    2 => 0.003m,
                    3 => -0.001m,
                    _ => 0.001m,
                };
                returns[i][t] = (0.0005m * (i + 1)) + ((1m + (0.15m * i)) * common) + idiosyncratic;
            }
        }

        return returns;
    }

    private static IReadOnlyDictionary<EconomicRegime, IReadOnlyDictionary<Symbol, decimal>> NoRegimeTilts()
    {
        var noTilts = new Dictionary<Symbol, decimal>();
        return new Dictionary<EconomicRegime, IReadOnlyDictionary<Symbol, decimal>>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = noTilts,
            [EconomicRegime.RisingGrowthFallingInflation] = noTilts,
            [EconomicRegime.FallingGrowthRisingInflation] = noTilts,
            [EconomicRegime.FallingGrowthFallingInflation] = noTilts,
        };
    }

    private static IReadOnlyDictionary<EconomicRegime, AssetWeightConstraints> UnconstrainedRegimes()
        => new Dictionary<EconomicRegime, AssetWeightConstraints>
        {
            [EconomicRegime.RisingGrowthRisingInflation] = new(),
            [EconomicRegime.RisingGrowthFallingInflation] = new(),
            [EconomicRegime.FallingGrowthRisingInflation] = new(),
            [EconomicRegime.FallingGrowthFallingInflation] = new(),
        };

    /// <summary>
    /// One factory per concrete model, keyed by type name. Factories (not shared instances) so
    /// stateful models (e.g. <see cref="TurnoverPenalizedConstruction"/>, CVaR's internal VaR
    /// threshold) compare the two overloads from identical fresh state.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<IPortfolioConstructionModel>> s_roster =
        new Dictionary<string, Func<IPortfolioConstructionModel>>
        {
            [nameof(BlackLittermanConstruction)] = () => new BlackLittermanConstruction([0.25m, 0.25m, 0.25m, 0.25m]),
            [nameof(DynamicBlackLittermanConstruction)] = () => new DynamicBlackLittermanConstruction([]),
            [nameof(EntropyPoolingConstruction)] = () => new EntropyPoolingConstruction(),
            [nameof(EqualWeightConstruction)] = () => new EqualWeightConstruction(),
            [nameof(HierarchicalEqualRiskContributionConstruction)] = () => new HierarchicalEqualRiskContributionConstruction(),
            [nameof(HierarchicalRiskParityConstruction)] = () => new HierarchicalRiskParityConstruction(),
            [nameof(InverseVolatilityConstruction)] = () => new InverseVolatilityConstruction(),
            [nameof(MaximumDiversificationConstruction)] = () => new MaximumDiversificationConstruction(),
            [nameof(MeanDownsideRiskConstruction)] = () => new MeanDownsideRiskConstruction(new CVaRRiskMeasure()),
            [nameof(MeanVarianceConstruction)] = () => new MeanVarianceConstruction(),
            [nameof(MinimumVarianceConstruction)] = () => new MinimumVarianceConstruction(),
            [nameof(NestedClusteredOptimizationConstruction)] = () => new NestedClusteredOptimizationConstruction(),
            [nameof(PcaConstrainedConstruction)] = () => new PcaConstrainedConstruction(new EqualWeightConstruction()),
            [nameof(PrincipalComponentRiskParityConstruction)] = () => new PrincipalComponentRiskParityConstruction(),
            [nameof(RegimeWeightConstrainedConstruction)] = () => new RegimeWeightConstrainedConstruction(
                new EqualWeightConstruction(), UnconstrainedRegimes(), EconomicRegime.RisingGrowthRisingInflation),
            [nameof(ReturnTiltedHrpConstruction)] = () => new ReturnTiltedHrpConstruction(),
            [nameof(RiskParityConstruction)] = () => new RiskParityConstruction(),
            [nameof(RobustMeanVarianceConstruction)] = () => new RobustMeanVarianceConstruction(new SampleCovarianceEstimator()),
            [nameof(SignalTiltConstruction)] = () => new SignalTiltConstruction(
                new EqualWeightConstruction(),
                [(new TimeSeriesMomentumSignal(lookbackMonths: 2, tradingDaysPerMonth: 5, volatilityWindow: 2), 0.1m)]),
            [nameof(SparseMinimumVarianceConstruction)] = () => new SparseMinimumVarianceConstruction(),
            [nameof(TacticalOverlayConstruction)] = () => new TacticalOverlayConstruction(
                new EqualWeightConstruction(), NoRegimeTilts(), EconomicRegime.RisingGrowthRisingInflation),
            [nameof(TurnoverPenalizedConstruction)] = () => new TurnoverPenalizedConstruction(new EqualWeightConstruction()),
            [nameof(VolatilityTargetingConstruction)] = () => new VolatilityTargetingConstruction(
                new EqualWeightConstruction(), targetVolatility: 0.10m),
            [nameof(WeightConstrainedConstruction)] = () => new WeightConstrainedConstruction(new EqualWeightConstruction()),
        };

    public static TheoryData<string> RosterModelNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in s_roster.Keys.Order())
            {
                data.Add(name);
            }

            return data;
        }
    }

    [Fact]
    public void Roster_EnumeratesEveryConcreteConstructionModel()
    {
        var discovered = typeof(EqualWeightConstruction).Assembly
            .GetTypes()
            .Where(t => t.Namespace == typeof(EqualWeightConstruction).Namespace
                && t is { IsClass: true, IsAbstract: false }
                && typeof(IPortfolioConstructionModel).IsAssignableFrom(t))
            .Select(t => t.Name);

        discovered.Should().BeEquivalentTo(
            s_roster.Keys,
            "every concrete IPortfolioConstructionModel in the PortfolioConstruction namespace must " +
            "join the as-of byte-identity roster — add a factory for the new model to this test");
    }

    [Theory]
    [MemberData(nameof(RosterModelNames))]
    public void DateAwareOverload_IsByteIdenticalToDateFreeOverload(string modelName)
    {
        var factory = s_roster[modelName];

        var dateFree = factory().ComputeTargetWeights(s_assets, s_returns);
        var dateAware = factory().ComputeTargetWeights(s_assets, s_returns, s_anyDate);

        dateAware.Should().BeEquivalentTo(
            dateFree,
            "a date-free model falls through the default interface method and a wrapping decorator " +
            "forwards asOf to its date-free inner model, so the evaluation date must not change results");
    }
}
