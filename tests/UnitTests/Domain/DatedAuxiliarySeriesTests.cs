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

namespace Boutquin.Trading.Tests.UnitTests.Domain;

using Boutquin.Trading.Domain.ValueObjects;
using FluentAssertions;

/// <summary>
/// Tests for <see cref="DatedAuxiliarySeries"/> — the date-indexed auxiliary-series carrier whose
/// <see cref="DatedAuxiliarySeries.SliceUpTo"/> is the structural defense against auxiliary-series
/// lookahead in multi-rebalance backtests.
/// </summary>
public sealed class DatedAuxiliarySeriesTests
{
    private static readonly Symbol s_dgs10 = new("DGS10");
    private static readonly Symbol s_spy = new("SPY");

    private static DateOnly D(int day) => new(2024, 1, day);

    [Fact]
    public void Constructor_WithNullSeries_ThrowsArgumentNullException()
    {
        var act = () => new DatedAuxiliarySeries(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullObservationList_ThrowsNamingTheKey()
    {
        var series = new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>
        {
            [s_dgs10] = null!,
        };

        var act = () => new DatedAuxiliarySeries(series);

        act.Should().Throw<ArgumentException>().WithMessage("*DGS10*null*");
    }

    [Fact]
    public void Constructor_WithOutOfOrderDates_ThrowsNamingTheKey()
    {
        var series = new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>
        {
            [s_dgs10] = [(D(3), 4.0m), (D(2), 4.1m)],
        };

        var act = () => new DatedAuxiliarySeries(series);

        act.Should().Throw<ArgumentException>().WithMessage("*DGS10*strictly ascending*");
    }

    [Fact]
    public void Constructor_WithDuplicateDates_ThrowsNamingTheKey()
    {
        var series = new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>
        {
            [s_dgs10] = [(D(2), 4.0m), (D(2), 4.1m)],
        };

        var act = () => new DatedAuxiliarySeries(series);

        act.Should().Throw<ArgumentException>().WithMessage("*DGS10*strictly ascending*");
    }

    [Fact]
    public void SliceUpTo_IncludesObservationDatedExactlyAsOf()
    {
        var dated = new DatedAuxiliarySeries(new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>
        {
            [s_dgs10] = [(D(1), 1m), (D(2), 2m), (D(3), 3m)],
        });

        var slice = dated.SliceUpTo(D(2));

        slice[s_dgs10].Should().Equal(1m, 2m);
    }

    [Fact]
    public void SliceUpTo_ExcludesObservationsAfterAsOf_TailIsMostRecentAtAsOf()
    {
        var dated = new DatedAuxiliarySeries(new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>
        {
            [s_dgs10] = [(D(1), 1m), (D(3), 3m), (D(5), 5m), (D(9), 9m)],
        });

        // As-of a date between observations: the tail is the most recent observation AT the date,
        // never a later one — the exact tail-read (series[^1]) contract the signal layer relies on.
        var slice = dated.SliceUpTo(D(6));

        slice[s_dgs10].Should().Equal(1m, 3m, 5m);
        slice[s_dgs10][^1].Should().Be(5m);
    }

    [Fact]
    public void SliceUpTo_KeyWithNoObservationOnOrBeforeAsOf_IsOmitted()
    {
        var dated = new DatedAuxiliarySeries(new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>
        {
            [s_dgs10] = [(D(10), 4.0m)],
            [s_spy] = [(D(1), 0.02m)],
        });

        var slice = dated.SliceUpTo(D(5));

        slice.Should().NotContainKey(s_dgs10, "no DGS10 observation exists on or before the as-of date");
        slice[s_spy].Should().Equal(0.02m);
    }

    [Fact]
    public void SliceUpTo_EmptyObservationList_KeyNeverAppears()
    {
        var dated = new DatedAuxiliarySeries(new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>
        {
            [s_dgs10] = [],
        });

        dated.SliceUpTo(D(31)).Should().BeEmpty();
    }

    [Fact]
    public void SliceUpTo_EmptyDictionary_YieldsEmptyResult()
    {
        var dated = new DatedAuxiliarySeries(
            new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>());

        dated.SliceUpTo(D(1)).Should().BeEmpty();
    }

    [Fact]
    public void FromEconomicSeries_SlicesLikeTheEquivalentTupleConstruction()
    {
        var dated = DatedAuxiliarySeries.FromEconomicSeries(
            new Dictionary<Symbol, SortedDictionary<DateOnly, decimal>>
            {
                [s_dgs10] = new() { [D(3)] = 3m, [D(1)] = 1m, [D(5)] = 5m },
            });

        // SortedDictionary enumerates ascending regardless of insertion order, so the invariant
        // holds by construction and the slice matches the tuple-built equivalent.
        dated.SliceUpTo(D(4))[s_dgs10].Should().Equal(1m, 3m);
    }

    [Fact]
    public void FromEconomicSeries_WithNullSeriesValue_ThrowsNamingTheKey()
    {
        var act = () => DatedAuxiliarySeries.FromEconomicSeries(
            new Dictionary<Symbol, SortedDictionary<DateOnly, decimal>>
            {
                [s_dgs10] = null!,
            });

        act.Should().Throw<ArgumentException>().WithMessage("*DGS10*null*");
    }

    [Fact]
    public void Constructor_TakesDefensiveCopy_LaterMutationOfCallerListDoesNotAffectSlices()
    {
        var mutable = new List<(DateOnly Date, decimal Value)> { (D(1), 1m), (D(2), 2m) };
        var dated = new DatedAuxiliarySeries(new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>
        {
            [s_dgs10] = mutable,
        });

        mutable[1] = (D(2), 999m);
        mutable.Add((D(3), 999m));

        dated.SliceUpTo(D(3))[s_dgs10].Should().Equal(1m, 2m);
    }
}
